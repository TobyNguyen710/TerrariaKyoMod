using System;
using System.Collections.Generic;
using KyoMod.Common.Players;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
    // Havoc's ultimate: dash a fixed distance toward the cursor, stopped early by solid walls.
    // Enemies swept through during the dash take no direct damage — instead they're marked for 7
    // delayed "ghost" hits, mirroring HavocSkillScythe's follow-up mechanic (same multiplier, more
    // hits). Purely a movement + hit-marking controller; the visual is a separate
    // HavocUltimateSlash spawned once the actual (possibly wall-shortened) travel distance is known.
    public class HavocUltimateDash : ModProjectile
    {
        // Pure logic controller, no visual of its own — HavocUltimateSlash is the actual effect.
        public override string Texture => "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheNoStack";

        private const float DashDistance  = 500f;
        private const int   DashFrames    = 8;
        private const float DashHitRadius = 70f; // how close an enemy needs to be to the player's
                                                  // current position each dash frame to count as
                                                  // "swept" — a per-frame radial check standing in
                                                  // for a continuous sweep, good enough at this speed

        private const int   GhostHits       = 7;
        private const float GhostMultiplier = 0.65f; // same as HavocSkillScythe's ghost hits
        private const int   GhostDelay      = 20;    // frames before the first ghost hit lands
        private const int   GhostRepeat     = 4;      // frames between each subsequent ghost hit

        private struct DelayedHitEntry
        {
            public int NpcIndex;
            public int Timer;
            public int HitsLeft;
        }

        private readonly List<DelayedHitEntry> pendingHits = [];
        private readonly HashSet<int> hitNpcs = [];

        private const int MaxStacksPerDash = 7;

        private bool    initialized       = false;
        private bool    dashEndedHandled  = false;
        private Vector2 direction;
        private float   actualDistance;
        private int     dashTimer = 0;
        private int     stacksGrantedThisDash = 0;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults()
        {
            Projectile.width       = Projectile.height = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly    = false;
            Projectile.hostile     = false;
            Projectile.penetrate   = -1;
            // Generous safety-net lifetime covering the full dash + every possible ghost hit —
            // the projectile actually self-Kills once pendingHits empties, well before this.
            Projectile.timeLeft    = DashFrames + GhostDelay + GhostHits * GhostRepeat + 30;
        }

        public override bool? CanHitNPC(NPC target) => false;
        public override bool CanHitPlayer(Player target) => false;
        public override bool PreDraw(ref Color lightColor) => false;

        // Steps along the intended path in small increments, stopping just before the first solid
        // tile block — the dash never embeds the player in a wall.
        private static float FindClearDistance(Vector2 start, Vector2 dir)
        {
            const float step = 8f;
            float travelled = 0f;
            while (travelled < DashDistance)
            {
                float next = Math.Min(travelled + step, DashDistance);
                Vector2 pos = start + dir * next;
                if (Collision.SolidCollision(pos - new Vector2(10f, 10f), 20, 20))
                    return travelled;
                travelled = next;
            }
            return DashDistance;
        }

        public override void AI()
        {
            if (!initialized)
            {
                initialized    = true;
                direction      = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                actualDistance = FindClearDistance(Owner.Center, direction);

                if (Main.myPlayer == Projectile.owner)
                {
                    SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/ScytheSlash1") { PitchVariance = 0.1f, Volume = 0.8f }, Owner.Center);

                    Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                        Owner.Center + direction * (actualDistance * 0.5f), Vector2.Zero,
                        ModContent.ProjectileType<HavocUltimateSlash>(), 0, 0f, Projectile.owner,
                        direction.X, direction.Y, actualDistance);
                }
            }

            if (dashTimer < DashFrames)
            {
                // Writes position directly instead of setting velocity and letting the engine
                // integrate it — velocity-based movement left a same-tick-vs-next-tick ordering
                // ambiguity that was the likely cause of the dash's speed carrying over as real
                // player momentum afterward. Zeroing velocity every dash tick (not just once at the
                // end) means nothing — gravity, input, whatever — can accumulate into it during the
                // dash at all, so there's nothing left to carry over once it's done.
                Owner.position += direction * (actualDistance / DashFrames);
                Owner.velocity  = Vector2.Zero;
                dashTimer++;
                SweepHits();
            }
            else if (!dashEndedHandled)
            {
                dashEndedHandled = true;
                Owner.velocity   = Vector2.Zero;
            }

            UpdatePendingHits();

            if (dashTimer >= DashFrames && pendingHits.Count == 0)
                Projectile.Kill();
        }

        private void SweepHits()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                // Neither dontTakeDamage nor immortal is filtered here — per NPC.immortal's own doc
                // comment, that flag is specifically what the Target Dummy uses to still register
                // hits/damage numbers while not actually losing life. Excluding either was silently
                // eating every hit against it.
                if (!npc.active || npc.friendly) continue;
                if (hitNpcs.Contains(i)) continue;
                if (Vector2.DistanceSquared(Owner.Center, npc.Center) > DashHitRadius * DashHitRadius) continue;

                hitNpcs.Add(i);
                pendingHits.Add(new DelayedHitEntry
                {
                    NpcIndex = i,
                    Timer    = GhostDelay,
                    HitsLeft = GhostHits,
                });

                if (Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/ScytheImpactHeavy") { PitchVariance = 0.15f, Volume = 0.6f }, npc.Center);
            }
        }

        private void UpdatePendingHits()
        {
            for (int i = pendingHits.Count - 1; i >= 0; i--)
            {
                var entry = pendingHits[i];
                if (entry.Timer > 0)
                {
                    entry.Timer--;
                    pendingHits[i] = entry;
                    continue;
                }

                FireGhostHit(entry.NpcIndex);
                entry.HitsLeft--;
                if (entry.HitsLeft <= 0)
                {
                    pendingHits.RemoveAt(i);
                }
                else
                {
                    entry.Timer = GhostRepeat;
                    pendingHits[i] = entry;
                }
            }
        }

        private void FireGhostHit(int npcIdx)
        {
            NPC npc = Main.npc[npcIdx];
            if (!npc.active) return;

            var kyoPlayer = Owner.GetModPlayer<CryingTidesPlayer>();
            kyoPlayer.AddLiberationEnergy();

            if (stacksGrantedThisDash < MaxStacksPerDash)
            {
                stacksGrantedThisDash++;
                kyoPlayer.AddHavocStack();
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int scaledDamage = (int)(Projectile.damage * GhostMultiplier);
                int dir = Math.Sign(npc.Center.X - Owner.Center.X);
                if (dir == 0) dir = 1;
                bool crit = Main.rand.Next(100) < Owner.GetCritChance(DamageClass.Melee);

                // ApplyDamageToNPC (not SimpleStrikeNPC) — tModLoader's purpose-built helper for
                // dealing damage outside the normal hit pipeline. SimpleStrikeNPC silently failed to
                // register hits on the Target Dummy; this one correctly handles its special display.
                // Returns void, so the actual damage dealt is read back from the life delta.
                var savedSound = npc.HitSound;
                npc.HitSound = null;
                int lifeBefore = npc.life;
                Owner.ApplyDamageToNPC(npc, scaledDamage, 0f, dir, crit);
                npc.HitSound = savedSound;

                int actualDamage = Math.Max(0, lifeBefore - npc.life);
                Owner.addDPS(actualDamage);
            }

            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/ScytheImpactLight") { PitchVariance = 0.2f, Volume = 0.5f }, npc.Center);

                for (int i = 0; i < 10; i++)
                {
                    Dust d = Dust.NewDustDirect(npc.Center, 0, 0, DustID.Crimson);
                    d.velocity  = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                    d.scale     = Main.rand.NextFloat(0.8f, 1.5f);
                    d.noGravity = true;
                }
            }
        }
    }
}
