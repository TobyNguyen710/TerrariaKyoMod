using System;
using System.Collections.Generic;
using KyoMod.Common.Players;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
    public class HavocSkillScythe : ModProjectile
    {
        // ai[0] = spin direction (1 or -1)
        // ai[1] = texture tier at cast (0 / 1 / 2)
        // ai[2] = sword scale from item

        private int   SpinDir     => (int)Projectile.ai[0];
        private int   TextureTier => (int)Projectile.ai[1];
        private float SwordScale  => Projectile.ai[2] > 0f ? Projectile.ai[2] : 1f;

        private const int   Lifetime        = 25;
        private const float SpinSpeed       = 0.45f;
        private const int   FadeFrames      = 7;
        private const float HitRadius       = 22f;
        private const int   GhostHits       = 5;
        private const float GhostMultiplier = 0.65f;
        private const int   GhostDelay      = 48; // 0.8 s

        private struct DelayedHitEntry
        {
            public int   NpcIndex;
            public int   Timer;
            public int   HitsLeft;
            public float DamageMultiplier;
        }

        private readonly List<DelayedHitEntry> pendingHits = [];
        private bool       ghostDelayActive = false;
        private SoundStyle? savedNpcHitSound = null;

        // Afterimage smear: a short ring buffer of past rotations, drawn additively behind the
        // current sprite (oldest = dimmest) — the sprite's own rotation alone doesn't read as
        // "spinning fast" at a glance, this fakes motion blur cheaply without a trail texture.
        private const int SmearCount = 6;
        private readonly Queue<float> rotationHistory = new();

        // Dust spawned at the blade's own tip (not the center) each frame traces a visible circular
        // streak as it spins — the existing center-burst dust reads as generic sparks, this one
        // specifically sells the spin.
        private const float TipDustRadius = 26f;

        // Sprite is drawn facing right by default — flip it when thrown leftward so it doesn't look
        // mirrored/backwards. SpinDir already doubles as the throw direction's sign.
        private SpriteEffects Flip => SpinDir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        public override string Texture => "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheNoStack";

        public override void SetDefaults()
        {
            Projectile.width  = 50;
            Projectile.height = 50;
            Projectile.friendly        = true;
            Projectile.DamageType      = DamageClass.Melee;
            Projectile.penetrate       = -1;
            Projectile.tileCollide     = false;
            Projectile.ignoreWater     = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown  = -1;
            Projectile.timeLeft        = Lifetime;
        }

        private Texture2D GetTexture()
        {
            string path = TextureTier >= 2
                ? "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheMaxStack"
                : TextureTier >= 1
                    ? "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheHalfStack"
                    : "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheNoStack";
            return ModContent.Request<Texture2D>(path).Value;
        }

        private void FireDelayedHitSingle(int npcIdx, float damageMultiplier)
        {
            NPC npc = Main.npc[npcIdx];
            if (!npc.active) return;

            Main.player[Projectile.owner].GetModPlayer<CryingTidesPlayer>().AddLiberationEnergy();

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int scaledDamage = (int)(Projectile.damage * damageMultiplier);
                int dir = Math.Sign(npc.Center.X - Projectile.Center.X);
                if (dir == 0) dir = 1;
                bool crit = Main.rand.Next(100) < Main.player[Projectile.owner].GetCritChance(DamageClass.Melee);

                var savedSound = npc.HitSound;
                npc.HitSound = null;
                int actualDamage = npc.SimpleStrikeNPC(scaledDamage, dir, crit, Projectile.knockBack, DamageClass.Melee);
                npc.HitSound = savedSound;

                Main.player[Projectile.owner].addDPS(actualDamage);
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

                int markIdx = Projectile.NewProjectile(Projectile.GetSource_FromThis(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<HavocSlashMark>(), 0, 0f, Projectile.owner);
                if (markIdx < Main.maxProjectiles)
                {
                    Main.projectile[markIdx].ai[0] = Main.rand.NextFloat(MathHelper.TwoPi);
                    Main.projectile[markIdx].ai[1] = 35f;
                }
            }
        }

        public override void AI()
        {
            // Keep alive until all ghost hits fire
            if (pendingHits.Count > 0)
            {
                if (Projectile.timeLeft <= 1)
                    ghostDelayActive = true;
                if (Projectile.timeLeft <= 2)
                    Projectile.timeLeft = 2;
            }

            for (int i = pendingHits.Count - 1; i >= 0; i--)
            {
                var entry = pendingHits[i];
                if (entry.Timer > 0)
                {
                    entry.Timer--;
                    pendingHits[i] = entry;
                }
                else
                {
                    FireDelayedHitSingle(entry.NpcIndex, entry.DamageMultiplier);
                    entry.HitsLeft--;
                    if (entry.HitsLeft <= 0)
                        pendingHits.RemoveAt(i);
                    else
                    {
                        entry.Timer = 2;
                        pendingHits[i] = entry;
                    }
                }
            }

            if (!ghostDelayActive)
            {
                Projectile.rotation += SpinSpeed * SpinDir;

                rotationHistory.Enqueue(Projectile.rotation);
                if (rotationHistory.Count > SmearCount)
                    rotationHistory.Dequeue();

                // Crimson dust trail (ambient burst around the whole blade)
                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustDirect(Projectile.Center, 8, 8, DustID.Crimson);
                    d.velocity  = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(1f, 3f);
                    d.noGravity = true;
                    d.scale     = Main.rand.NextFloat(0.6f, 1.2f);
                }

                // Tip-traced dust — spawned exactly on the spinning edge so consecutive frames trace
                // an actual visible ring/streak, not just scattered sparks.
                Vector2 tipPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * (TipDustRadius * SwordScale);
                Dust tip = Dust.NewDustDirect(tipPos, 0, 0, DustID.CrimsonTorch);
                tip.velocity  = Vector2.Zero;
                tip.noGravity = true;
                tip.scale     = Main.rand.NextFloat(0.9f, 1.3f);
                tip.fadeIn    = 0.6f;

                // Crimson point light
                float glow = TextureTier >= 2 ? 1.0f : TextureTier >= 1 ? 0.55f : 0.25f;
                Lighting.AddLight(Projectile.Center, glow * 0.85f, glow * 0.10f, glow * 0.06f);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            savedNpcHitSound = target.HitSound;
            target.HitSound  = null;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.HitSound = savedNpcHitSound;
            Main.player[Projectile.owner].GetModPlayer<CryingTidesPlayer>().AddLiberationEnergy();

            pendingHits.Add(new DelayedHitEntry
            {
                NpcIndex         = target.whoAmI,
                Timer            = GhostDelay,
                HitsLeft         = GhostHits,
                DamageMultiplier = GhostMultiplier,
            });

            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/ScytheImpactHeavy") { PitchVariance = 0.15f, Volume = 0.7f }, target.Center);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (ghostDelayActive) return false;

            float radius = HitRadius * SwordScale * 3f;
            Vector2 closest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(Projectile.Center, closest) <= radius * radius;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (ghostDelayActive) return false;

            Texture2D     tex     = GetTexture();
            float         alpha   = Projectile.timeLeft < FadeFrames ? Projectile.timeLeft / (float)FadeFrames : 1f;
            var           origin  = new Vector2(tex.Width * 0.5f, tex.Height * 0.5f);
            float         scale   = SwordScale * 3f * alpha;
            Vector2       drawPos = Projectile.Center - Main.screenPosition;
            SpriteEffects flip    = Flip;

            // Afterimage smear: oldest rotations drawn first (dimmest, furthest back), additive so
            // they glow rather than muddy the sprite's own color — the cheap "spinning fast" cue.
            if (rotationHistory.Count > 0)
            {
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

                int   i     = 0;
                int   count = rotationHistory.Count;
                foreach (float pastRotation in rotationHistory)
                {
                    float t          = (i + 1) / (float)(count + 1); // 0 = oldest, ~1 = newest
                    float smearAlpha = t * t * 0.35f * alpha;
                    Main.spriteBatch.Draw(tex, drawPos, null, Color.Crimson * smearAlpha, pastRotation, origin, scale, flip, 0f);
                    i++;
                }

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            Main.spriteBatch.Draw(tex,
                drawPos,
                null,
                lightColor * alpha,
                Projectile.rotation,
                origin,
                scale,
                flip,
                0f);

            return false;
        }
    }
}
