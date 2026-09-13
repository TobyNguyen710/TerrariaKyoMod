using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Void form's ultimate cast (Bubble->Void direction). Spawned at the cursor by
    // BubblingOblivionPlayer.ProcessTriggers — no travel, it just appears.
    //
    // Lifecycle (design doc §14.1) — deliberately ONE continuous object, not a chain of effects:
    //
    //     arrive -> hold (contact damage) -> collapse -> hand off to the field, whose core IS this
    //     black hole re-emerging smaller
    //
    // The collapse-then-re-emerge beat intentionally mirrors BigBubble's own shrink/hold/bloom sequence,
    // which the user originally justified as *feeling* like a black hole; it's simply more honest here,
    // where it literally is one. The timings deliberately do NOT match BigBubble's — this one is slower
    // and heavier throughout, or the ultimate reads as nothing more than a bigger combo 3.
    //
    // There is no Explode() any more. The ultimate's 10x hit is the field's opening sweep (§14.3), so
    // this projectile's final act is simply to spawn the field and die.
    public class BubblingOblivionVoidBlackHole : ModProjectile
    {
        // Never drawn via its own texture — PreDraw fully overrides with VoidVisuals.DrawBlackHole.
        public override string Texture => "KyoMod/Content/Items/Magic/BubblingOblivion/BubblingOblivion";

        // Phase lengths, in ticks (60 = 1s). Held deliberately longer/slower than BigBubble's 40/25/6.
        // Timeline: arrive+hold ends at 2.8s, collapse runs to 4.0s, field opens there.
        private const int ArriveFrames = 26;     // 0.43s — grows in from nothing
        private const int HoldFrames = 142;      // full size, dealing contact damage; ends at 2.8s
        private const int CollapseFrames = 72;   // 1.2s — shrinks away to nothing, ending at 4.0s

        private int CollapseStart => ArriveFrames + HoldFrames;
        private int TotalFrames => CollapseStart + CollapseFrames;

        // The impact-star flash covers the end of the collapse and lands exactly as the field opens, so
        // the biggest hit in the weapon has a read on it instead of firing from an invisible point.
        // Bigger than BigBubble's by explicit request.
        private const int FlashFrames = 16;
        private int FlashStart => TotalFrames - FlashFrames;

        private const float LingerRadius = 50f;
        private const int LingerHitCooldown = 20;

        private const float MeshScale = 6f;     // visual size relative to LingerRadius
        private const float Shadow01 = 0.12f;
        private const float DiskOuter = 7f;

        private const float CollapseDustRadius = 150f;

        private float shaderSeed;
        private int age;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = (int)(LingerRadius * 2f);
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalFrames + 2;
            Projectile.light = 0.6f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = LingerHitCooldown;
        }

        public override void OnSpawn(IEntitySource source)
        {
            shaderSeed = Main.rand.NextFloat(100f);

            // Purpose-made ultimate sound, so it's played straight — no pitch shifting the way the
            // borrowed BigPop placeholder needed. Deliberately specific to the Bubble->Void direction:
            // Void->Bubble is meant to get its own sound rather than reusing this one, and is currently
            // silent because that direction is still the old bolt-burst placeholder (design doc §12).
            // It also runs long enough to cover the whole cast, which is why the collapse has no second
            // sound cue of its own — see AI().
            if (Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/BubblingOblivion/BO_Ult1") { Volume = 0.85f }, Projectile.Center);
        }

        public override void AI()
        {
            age++;
            Projectile.velocity = Vector2.Zero;

            if (age >= CollapseStart)
            {
                // Collapsing: the hitbox shrinks with the visual, so it genuinely stops touching things
                // as it winds down rather than keeping a full-size hitbox around an invisible point.
                float t = MathHelper.Clamp((age - CollapseStart) / (float)CollapseFrames, 0f, 1f);
                ApplyHitboxScale(1f - t);

                // Inward-pulled particles, literally correct here — BigBubble borrowed this idea from
                // black holes in the first place. Fills what would otherwise be dead time where the
                // cast reads as "gone" rather than "collapsing".
                SpawnCollapseDust(pullSpeed: 5f, chance: 2);
                PulseSingularityLight();

                // No separate detonation sound here on purpose: BO_Ult1 (played at spawn) already runs
                // long enough to cover the collapse and the field opening, so a second cue on top of it
                // just clashes. The impact-star flash still carries the moment visually.
            }
            else
            {
                float pulse = 0.4f + 0.3f * MathF.Sin(age * 0.35f);
                Lighting.AddLight(Projectile.Center, pulse * 0.08f, pulse * 0.45f, pulse * 0.75f);
            }

            if (age >= TotalFrames)
                OpenField();
        }

        // Hands off to the field and dies. The field's own core grows in from nothing on the very next
        // frame, so what the player sees is this black hole surviving its own collapse at a smaller
        // size — not one effect ending and another starting.
        private void OpenField()
        {
            if (Main.myPlayer == Projectile.owner)
            {
                Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<BubblingOblivionVoidField>(), Projectile.damage,
                    Projectile.knockBack, Projectile.owner);
            }

            Projectile.Kill();
        }

        // Vanilla's scale auto-resize only fires once right after SetDefaults(), so a changing hitbox has
        // to be maintained by hand — same gotcha as BigBubble.ApplyScale (see its comment). Recenter
        // afterward, since changing width/height shifts what Center resolves to.
        private void ApplyHitboxScale(float scale)
        {
            Vector2 center = Projectile.Center;
            int size = Math.Max((int)(LingerRadius * 2f * scale), 2);
            Projectile.width = Projectile.height = size;
            Projectile.Center = center;
        }

        // Mirrors BigBubble.SpawnCollapseDust, including its lifetime trick: particles die exactly as
        // they reach the centre rather than flying through and out the far side.
        private void SpawnCollapseDust(float pullSpeed, int chance)
        {
            if (!Main.rand.NextBool(chance))
                return;

            Vector2 offset = Main.rand.NextVector2CircularEdge(CollapseDustRadius, CollapseDustRadius);
            Vector2 velocity = -offset.SafeNormalize(Vector2.Zero) * pullSpeed;
            int lifetime = (int)(CollapseDustRadius / pullSpeed);

            BubblingOblivionParticleSystem.Spawn(Projectile.Center + offset, velocity,
                BubblingOblivionVisuals.CollapseParticleColor, BubblingOblivionVisuals.ParticleScale, lifetime, drag: 1f);
        }

        private void PulseSingularityLight()
        {
            float pulse = 0.35f + 0.25f * MathF.Sin(age * 0.3f);
            Lighting.AddLight(Projectile.Center, pulse * 0.35f, pulse * 0.15f, pulse * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float scale;
            if (age < ArriveFrames)
            {
                float t = MathHelper.Clamp(age / (float)ArriveFrames, 0f, 1f);
                scale = t * t * (3f - 2f * t);
            }
            else if (age < CollapseStart)
            {
                scale = 1f;
            }
            else
            {
                float t = MathHelper.Clamp((age - CollapseStart) / (float)CollapseFrames, 0f, 1f);
                scale = 1f - t * t * (3f - 2f * t); // eases into the collapse, then drops away fast
            }

            if (scale > 0.01f)
            {
                VoidVisuals.DrawBlackHole(Projectile.Center, LingerRadius * MeshScale * scale,
                    Shadow01, DiskOuter, age / 60f, shaderSeed, opacity: 1f);
            }

            if (age >= FlashStart)
            {
                float flashProgress = MathHelper.Clamp((age - FlashStart) / (float)FlashFrames, 0f, 1f);
                BubblingOblivionMeshEffects.DrawImpactStar(Projectile.Center, flashProgress);
            }

            BubblingOblivionDebug.DrawHitbox(Projectile, Color.Cyan * 0.4f);
            return false;
        }
    }
}
