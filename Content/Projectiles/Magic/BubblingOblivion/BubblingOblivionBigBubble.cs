using System;
using System.Collections.Generic;
using KyoMod.Common.Players;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Bubble form's combo-3 special: travels forward a short distance, then plants in place as a
    // lingering, poppable bubble. Popping (currently: a regular bolt touching it — this trigger is
    // explicitly provisional, see design doc) deals a burst of bonus damage. Other big bubbles never
    // pop this one, so multiple can coexist.
    public class BubblingOblivionBigBubble : ModProjectile
    {
        public override string Texture => "KyoMod/Content/Projectiles/Magic/BubblingOblivion/BubblingOblivionBubble4";

        private const int BaseSize = 24;
        private const float ScaleFactor = 8f;
        private const float MaxTargetDistance = 1000f; // safety cap if the cursor snapshot is very far away
        private const int LingerTime = 420; // 7 seconds (was 180 / 3s)
        private const float PopRadius = BaseSize * ScaleFactor; // matches the lingering bubble's own size (192)
        private const float PopDamageMultiplier = 5f;

        // Travels small, then on landing shrinks all the way to nothing, holds at nothing briefly, then
        // blooms back to full ScaleFactor — an arrival payoff instead of flying at full size the whole
        // way, see design doc §12 for the "how would this feel" discussion behind it.
        private const float TravelScale = 2f;
        private const int ShrinkFrames = 40;
        private const int WaitFrames = 25;
        // Gap between the impact-star flash ending and the bloom actually starting — previously the
        // two were locked together (Grow began the instant Wait/Flash ended), which left no room to
        // time the grow SFX against the visual. Held at scale 0 same as the rest of Wait.
        private const int PostFlashPause = 20;
        private const int GrowFrames = 6;
        private int growTimer;
        private int GrowStart => ShrinkFrames + WaitFrames + PostFlashPause;

        // Collapse-phase dust radius — deliberately independent of the shrinking sprite's own current
        // size, since the point is dust visibly converging from further out than the bubble itself
        // (a gravity-well read), not just tracking the sprite's shrinking edge.
        private const float CollapseDustRadius = 90f;

        // Gameplay-visible enemy suck-in, not just the cosmetic collapse dust — a genuine gravity-well
        // pull reinforcing the black-hole read (see design doc's "how does this feel" discussion). Runs
        // the whole Shrink+Wait window (through FlashEnd), not just Shrink, per explicit user direction:
        // "even though our collapse/shrink time is short... make the suck effect last until the wait
        // time finishes." Deliberately weak — "only a weak suck tho since we are not in the void/black
        // hole form yet" — this is Bubble form foreshadowing Void form, not Void form itself.
        private const float SuckRadius = 220f;
        private const float SuckStrength = 0.1f;
        private const float SuckMaxSpeed = 2.35f;
        // A tiny direct position nudge alongside the velocity pull above — many grounded NPCs overwrite
        // their own velocity every tick in their AI (walking toward/away from the player), which fought
        // the velocity-only pull and made it read as basically nonexistent for those enemies. This can't
        // be overwritten the same way.
        private const float SuckPositionNudge = 0.15f;

        // A cross-shaped "hit spark" flash (BubblingOblivionMeshEffects.DrawImpactStar) plays over the
        // last FlashFrames ticks of the Wait phase, ending exactly as Grow begins — punctuates the
        // moment right before the bloom instead of the bloom just starting with no warning.
        private const int FlashFrames = 10;
        private int FlashStart => ShrinkFrames + WaitFrames - FlashFrames;
        private int FlashEnd => ShrinkFrames + WaitFrames;

        // Independent slow spin, randomized per-instance.
        private float rotationSpeed;

        // Per-instance shimmer seed + age (ticks) for BubbleSwirl.fx.
        private float shaderSeed;
        private int age;

        // Recent Center positions during travel (oldest first) — feeds the same trail ribbon used by
        // the regular bolts (BubblingOblivionMeshEffects.DrawTrail). Only sampled while traveling
        // (position is fixed once planted, so there's nothing to trail); cleared in Plant() so a stale
        // trail doesn't linger drawn at the landing spot through the collapse/grow sequence.
        private const int TrailLength = 14;
        private const float TrailMaxWidth = 20f;
        private readonly List<Vector2> trailHistory = new();

        // Slight vertical bob once fully bloomed and lingering — purely a floaty/buoyant feel, not
        // present during Shrink/Wait/Grow so those phases stay visually stable. restCenter is the
        // landing spot (set once in Plant()); the bob offsets from it each tick rather than
        // accumulating, so it never drifts.
        private const float BobAmplitude = 6f;
        private const float BobSpeed = 0.05f;
        private Vector2 restCenter;
        private float bobPhase;

        private SoundStyle? savedNpcHitSound;

        private bool Planted
        {
            get => Projectile.ai[0] == 1f;
            set => Projectile.ai[0] = value ? 1f : 0f;
        }

        private float Traveled
        {
            get => Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        // Distance to the cursor's world position at the moment combo 3 was cast — set by the item right
        // after spawning (see BubblingOblivionBase.Shoot). It travels there and plants, rather than
        // flying a fixed distance.
        public float TargetDistance
        {
            get => Projectile.localAI[0];
            set => Projectile.localAI[0] = MathHelper.Clamp(value, 0f, MaxTargetDistance);
        }

        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults()
        {
            // Native (unscaled) size — vanilla's Projectile.SetDefaults_End automatically multiplies
            // width/height by Projectile.scale once, right after this method returns. Pre-multiplying
            // here too double-applies the scale (was producing a 1536x1536 hitbox: 192 * 8). Starts at
            // TravelScale, not the final ScaleFactor — see the grow-on-plant logic in AI()/ApplyScale.
            Projectile.width = Projectile.height = BaseSize;
            Projectile.scale = TravelScale;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false; // passes through blocks entirely, travel or planted
            Projectile.ignoreWater = true;
            Projectile.light = 0.6f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            rotationSpeed = Main.rand.NextFloat(0.0075f, 0.00875f) * (Main.rand.NextBool() ? 1f : -1f);
            shaderSeed = Main.rand.NextFloat(100f);
            bobPhase = Main.rand.NextFloat(MathHelper.TwoPi);

            // Overrides the item's own knockBack (2, shared with the regular bolts) down to a lighter
            // value specifically for the lingering bubble's ordinary contact damage — balance call: a
            // stationary AoE this weapon leans on wants enemies to stick around near it rather than get
            // scattered away on every tick of contact, which would work against both the eventual pop
            // and the collapse phase's own suck-in pulling them back in.
            Projectile.knockBack = 1f;
        }

        public override void AI()
        {
            age++;
            Projectile.rotation += rotationSpeed;

            if (!Planted)
            {
                Traveled += Projectile.velocity.Length();

                trailHistory.Add(Projectile.Center);
                if (trailHistory.Count > TrailLength)
                    trailHistory.RemoveAt(0);

                if (Traveled >= TargetDistance)
                    Plant();

                if (Main.rand.NextBool(2))
                    BubblingOblivionParticleSystem.SpawnInRect(Projectile.Hitbox, BubblingOblivionVisuals.AmbientParticleColor, BubblingOblivionVisuals.ParticleScale, 24);
                return;
            }

            Projectile.velocity = Vector2.Zero;

            if (growTimer < ShrinkFrames)
            {
                growTimer++;
                float t = growTimer / (float)ShrinkFrames;
                t = t * t * (3f - 2f * t); // smoothstep ease
                ApplyScale(MathHelper.Lerp(TravelScale, 0f, t));
                SpawnCollapseDust(pullSpeed: 5f, chance: 2);
                PullNearbyEnemies();
            }
            else if (growTimer < ShrinkFrames + WaitFrames)
            {
                growTimer++;
                // Holds at scale 0 — ApplyScale still called each tick so the hitbox stays synced
                // (a no-op in practice since it's already 0, but keeps the logic uniform).
                ApplyScale(0f);
                SpawnCollapseDust(pullSpeed: 2.5f, chance: 4);
                PulseSingularityLight();
                PullNearbyEnemies();

                // Grow sound plays right as the impact-star flash ("the cross") starts — timed to the
                // same tick as PreDraw's own FlashStart check, so the sound lands on the visual cue
                // instead of anywhere else in the hold.
                if (growTimer == FlashStart && Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/BubblingOblivion/BO_BubbleGrow") { PitchVariance = 0.1f, Volume = 0.7f }, Projectile.Center);
            }
            else if (growTimer < GrowStart)
            {
                growTimer++;
                // Post-flash pause: still fully collapsed, just holding a beat after the flash before
                // the bloom actually starts — see PostFlashPause's own comment for why this exists.
                ApplyScale(0f);
                PulseSingularityLight();
            }
            else if (growTimer < GrowStart + GrowFrames)
            {
                growTimer++;
                float t = (growTimer - GrowStart) / (float)GrowFrames;
                t = t * t * (3f - 2f * t); // smoothstep ease — a little "pop" to the inflation, not linear
                ApplyScale(MathHelper.Lerp(0f, ScaleFactor, t));
                if (Main.rand.NextBool(2))
                    BubblingOblivionParticleSystem.SpawnInRect(Projectile.Hitbox, BubblingOblivionVisuals.AmbientParticleColor, BubblingOblivionVisuals.ParticleScale, 24);
            }
            else
            {
                // Fully bloomed and lingering — bob every tick (not gated behind the particle roll,
                // which only fires 1/4 of ticks) so the drift itself stays smooth.
                ApplyBob();
                if (Main.rand.NextBool(4))
                    BubblingOblivionParticleSystem.SpawnInRect(Projectile.Hitbox, BubblingOblivionVisuals.AmbientParticleColor, BubblingOblivionVisuals.ParticleScale, 24);
            }

            CheckForPop();
        }

        // Collapse-phase visual: a particle spawned on a ring around the (shrinking/collapsed) bubble
        // that gets pulled INWARD toward its center, instead of the cheerful outward-drifting particles
        // used everywhere else — a deliberate gravity-well/black-hole read for Bubbling Oblivion's
        // namesake "Oblivion" side, foreshadowing Void form (see design doc §12's "how does this feel"
        // discussion: the user's own reasoning for shrink-then-grow was "feels a lot like a black
        // hole/void, isnt it?"). Darkened palette color, not the ambient one — visually distinct from
        // Bubble form's normal look on purpose, since this is meant to read as a glimpse of the darker
        // side underneath.
        private void SpawnCollapseDust(float pullSpeed, int chance)
        {
            if (!Main.rand.NextBool(chance))
                return;

            Vector2 offset = Main.rand.NextVector2CircularEdge(CollapseDustRadius, CollapseDustRadius);
            Vector2 velocity = -offset.SafeNormalize(Vector2.Zero) * pullSpeed;

            // Lifetime timed so the particle dies right as it reaches the collapse point, instead of
            // flying straight through and reappearing on the far side (visible past the midpoint —
            // "it breaks immersion"). NextVector2CircularEdge always picks a point exactly on
            // CollapseDustRadius, so distance/speed gives the exact tick count to close that gap;
            // truncating (not rounding up) guarantees it never overshoots. drag: 1f — stays brisk the
            // whole way instead of slowing down like the ambient particles do, since it's supposed to
            // read as being pulled in, not drifting.
            int lifetime = (int)(CollapseDustRadius / pullSpeed);
            BubblingOblivionParticleSystem.Spawn(Projectile.Center + offset, velocity, BubblingOblivionVisuals.CollapseParticleColor, BubblingOblivionVisuals.ParticleScale, lifetime, drag: 1f);
        }

        // Gameplay-visible gravity-well pull on nearby non-boss enemies during Shrink+Wait — see
        // SuckRadius/SuckStrength's own comment for the reasoning/timing. Excludes npc.boss and
        // anything vanilla itself already treats as boss-like for UI purposes
        // (NPCID.Sets.ShouldBeCountedAsBoss, e.g. pre-fight Queen Slime/Deerclops-style enemies) so a
        // weak cosmetic pull never visibly tugs at something it obviously shouldn't be able to budge.
        private void PullNearbyEnemies()
        {
            foreach (NPC npc in Main.npc)
            {
                if (!npc.active || npc.friendly || npc.dontTakeDamage)
                    continue;
                if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type])
                    continue;
                // Target Dummy is meant to stay fixed in place for damage testing — pulling its hitbox
                // around defeats the point of it (a real bug report: "the suck effect move the dummy
                // hitbox"). Not friendly/dontTakeDamage, so it wasn't already excluded above.
                if (npc.type == NPCID.TargetDummy)
                    continue;

                Vector2 toCenter = Projectile.Center - npc.Center;
                float distance = toCenter.Length();
                if (distance > SuckRadius || distance < 1f)
                    continue;

                // Cap how much THIS pull contributes toward the NPC's existing velocity, rather than
                // clamping its total velocity — an already-fast enemy (e.g. a flying one) shouldn't get
                // forcibly slowed down just because a weak pull is also acting on it.
                Vector2 pullDir = toCenter / distance;
                float towardPull = Vector2.Dot(npc.velocity, pullDir);
                if (towardPull < SuckMaxSpeed)
                    npc.velocity += pullDir * SuckStrength;

                npc.position += pullDir * SuckPositionNudge;
            }
        }

        // A small pulsing light at the collapse point so the spot never reads as fully "nothing" while
        // the sprite itself is at scale 0 — purple-tinted rather than the bubble's usual light color,
        // same "dark side glimpse" reasoning as SpawnCollapseDust.
        private void PulseSingularityLight()
        {
            float pulse = 0.35f + 0.25f * MathF.Sin(age * 0.3f);
            Lighting.AddLight(Projectile.Center, pulse * 0.45f, pulse * 0.1f, pulse * 0.55f);
        }

        // Floaty bob for the fully-bloomed lingering bubble — offsets from restCenter (the landing
        // spot) each tick rather than accumulating an offset onto the previous tick's position, so it
        // can't drift over the bubble's 7-second linger.
        private void ApplyBob()
        {
            float bob = MathF.Sin(age * BobSpeed + bobPhase) * BobAmplitude;
            Projectile.Center = restCenter + new Vector2(0f, bob);
        }

        // Resizing width/height mid-flight doesn't happen automatically — vanilla's scale auto-resize
        // (see SetDefaults' own comment) only fires once, right after SetDefaults() returns. Recenter
        // afterward since changing width/height shifts what Center computes to otherwise.
        private void ApplyScale(float scale)
        {
            Vector2 center = Projectile.Center;
            Projectile.scale = scale;
            Projectile.width = Projectile.height = (int)(BaseSize * scale);
            Projectile.Center = center;
        }

        public override void OnKill(int timeLeft)
        {
            if (!Planted)
                Plant();
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            savedNpcHitSound = target.HitSound;
            target.HitSound = null;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.HitSound = savedNpcHitSound;
            Main.player[Projectile.owner].GetModPlayer<BubblingOblivionPlayer>().AddLiberationEnergy();

            if (Main.netMode == NetmodeID.Server)
                return;

            SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/BubblingOblivion/BO_Bubble_Hit") { PitchVariance = 0.3f, Volume = 0.5f }, target.Center);
        }

        private void Plant()
        {
            Planted = true;
            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = LingerTime;
            Projectile.netUpdate = true;
            trailHistory.Clear();
            restCenter = Projectile.Center;

            // Shrink begins the instant it plants, so the sound plays here rather than inside AI()'s
            // shrink branch (which would need its own "first tick" guard).
            if (Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/BubblingOblivion/BO_BubbleShrink") { PitchVariance = 0.1f, Volume = 0.7f }, Projectile.Center);
        }

        // Only regular bolts pop a planted bubble — two big bubbles never pop each other, so several can
        // be placed to coexist and stack up area coverage (synergizes with Recall, which wants multiple
        // bubbles to be out at once) instead of the newest one just clearing out the others.
        private void CheckForPop()
        {
            int boltType = ModContent.ProjectileType<BubblingOblivionBolt>();
            Rectangle myRect = Projectile.Hitbox;

            foreach (Projectile p in Main.projectile)
            {
                if (!p.active || p.whoAmI == Projectile.whoAmI || p.owner != Projectile.owner)
                    continue;
                if (p.type != boltType)
                    continue;
                if (!myRect.Intersects(p.Hitbox))
                    continue;

                p.Kill();
                Pop();
                return;
            }
        }

        private void Pop()
        {
            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/BubblingOblivion/BO_Bubble_BigPop") { PitchVariance = 0.1f, Volume = 0.7f }, Projectile.Center);
                // Each particle picks a random stop off the full 8-color palette (RandomPopColor) rather
                // than one flat color, so the burst as a whole spans the same color range as the
                // shockwave ring's own marble instead of reading as a single mismatched tone.
                for (int i = 0; i < 20; i++)
                    BubblingOblivionParticleSystem.SpawnInRect(Projectile.Hitbox, BubblingOblivionVisuals.RandomPopColor(), BubblingOblivionVisuals.ParticleScale, 30, driftSpeed: 2f);

                // Telegraph ring: particles placed exactly on the actual pop-damage boundary
                // (PopRadius), pushed outward, so the AoE's real extent reads clearly instead of just a
                // center burst that gives no sense of scale.
                const int ringPoints = 24;
                for (int i = 0; i < ringPoints; i++)
                {
                    float angle = MathHelper.TwoPi * i / ringPoints;
                    Vector2 dir = angle.ToRotationVector2();
                    BubblingOblivionParticleSystem.Spawn(Projectile.Center + dir * PopRadius, dir * 3f, BubblingOblivionVisuals.RandomPopColor(), BubblingOblivionVisuals.ParticleScale, 30);
                }

                // Real shockwave mesh on top of the particle telegraph ring above — a marbled ring
                // expanding out to the same PopRadius, so popping reads as a genuine "cast" moment
                // instead of just a particle burst (design doc §12/§10). Purely cosmetic and per-client
                // (matches the netMode guard this whole block is already inside), so no ai-field sync
                // beyond the local spawn call is needed.
                Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<BubblingOblivionPopShockwave>(), 0, 0f, Projectile.owner, PopRadius);
            }

            int dmg = (int)(Projectile.damage * PopDamageMultiplier);
            foreach (NPC npc in Main.npc)
            {
                if (!npc.active || npc.friendly)
                    continue;
                if (Vector2.Distance(npc.Center, Projectile.Center) > PopRadius)
                    continue;

                int dir = npc.Center.X >= Projectile.Center.X ? 1 : -1;
                bool crit = Main.rand.Next(100) < Owner.GetCritChance(DamageClass.Magic);
                Owner.ApplyDamageToNPC(npc, dmg, 0f, dir, crit);

                // Pop's damage is applied manually (Owner.ApplyDamageToNPC), not via the normal
                // projectile-collision path, so it never reaches OnHitNPC's own energy-gain call above —
                // added here explicitly instead.
                Owner.GetModPlayer<BubblingOblivionPlayer>().AddLiberationEnergy();
            }

            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Drawn first so it sits behind the bubble's own sprite — same trail technique as the
            // regular bolts (BubblingOblivionBolt), just wider to match this projectile's own weight.
            BubblingOblivionMeshEffects.DrawTrail(trailHistory, TrailMaxWidth * Projectile.scale, age / 60f, shaderSeed);

            // Halo/aura — around the sprite, not just on it, same "hero object" treatment as the trail
            // (design doc §12's "boundless to the sprite" reasoning). Sized off the sprite's own current
            // scale so it grows/shrinks/vanishes together with the collapse/bloom sequence instead of
            // staying a fixed size throughout.
            if (Projectile.scale > 0.05f)
            {
                float haloRadius = BaseSize * Projectile.scale * 1.15f;
                BubblingOblivionMeshEffects.DrawHalo(Projectile.Center, haloRadius, age / 60f, shaderSeed);
            }

            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 origin = new Vector2(tex.Width, tex.Height) * 0.5f;
            // Slightly higher swirl frequency than the regular bolts — it's drawn much larger
            // (8x vs 4x), so a matching low frequency would read as one flat blob instead of marbling.
            BubblingOblivionVisuals.DrawSwirl(tex, Projectile.Center - Main.screenPosition, lightColor, Projectile.rotation,
                origin, Projectile.scale, age / 60f, shaderSeed, swirlScale: 9f);

            if (growTimer >= FlashStart && growTimer < FlashEnd)
            {
                float flashProgress = (growTimer - FlashStart) / (float)FlashFrames;
                BubblingOblivionMeshEffects.DrawImpactStar(Projectile.Center, flashProgress);
            }

            BubblingOblivionDebug.DrawHitbox(Projectile, Color.Red * 0.4f);
            return false;
        }
    }
}
