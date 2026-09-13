using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // The Void field (design doc §14), left behind when the black hole collapses. A diamond-shaped area
    // anchored by a miniature black hole at its centre, firing outward sweeps on an interval.
    //
    // The core is deliberately the SAME black hole that collapsed, re-emerging smaller rather than a
    // fresh effect appearing beside a dying one — that continuity is what makes the whole cast read as
    // one object with a story instead of a chain of unrelated VFX.
    //
    // The ultimate's 10x damage is this field's OPENING SWEEP (§14.3), not a separate explosion: the
    // first pulse simply carries a big multiplier. So there is no explosion code here or in the black
    // hole — that hit and every later pulse are the same projectile with a different damage number,
    // which is also why the border is legible from the very first moment (the player watches the big
    // one draw it).
    public class BubblingOblivionVoidField : ModProjectile
    {
        // Never drawn via its own texture — PreDraw fully overrides.
        public override string Texture => "KyoMod/Content/Items/Magic/BubblingOblivion/BubblingOblivion";

        private const int Lifetime = 2400;         // 40s
        public const float FieldRadius = 600f;     // L1 radius: centre to a POINT of the diamond

        private const int PulseInterval = 300;     // 5s between sweeps
        private const int PulseWindup = 84;        // 1.4s of warning before each sweep actually goes out
        private const float OpeningDamageMultiplier = 10f; // §14.3 — the ultimate's 10x IS this sweep
        private const float PulseDamageMultiplier = 0.5f;  // every later sweep

        // Core sizing follows §13.3's advice for a miniature black hole: don't just scale the big one
        // down, re-proportion it. A larger shadow fraction keeps the core readable at this size and a
        // shorter disk stops it sprawling — otherwise the thin lensing detail lands sub-pixel and
        // shimmers instead of reading as a small black hole.
        private const float CoreMeshRadius = 78f;
        private const float CoreShadow01 = 0.25f;
        private const float CoreDiskOuter = 3.4f;

        private const int CoreGrowFrames = 22;     // the "pops back up" beat — the core re-emerging

        private float shaderSeed;
        private int age;

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 2; // the field itself never collides; its pulses do
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.ignoreWater = true;
        }

        public override void OnSpawn(IEntitySource source)
        {
            Projectile.velocity = Vector2.Zero;
            shaderSeed = Main.rand.NextFloat(100f);

            // Fire the opening sweep immediately — this is the ultimate's payoff hit, so it must land on
            // the same frame the black hole finishes collapsing, not a pulse interval later.
            SpawnPulse(OpeningDamageMultiplier);
        }

        public override void AI()
        {
            age++;
            Projectile.velocity = Vector2.Zero;

            // Telegraph: the sound leads the sweep by PulseWindup, so it's a warning the pulse is coming
            // rather than a noise confirming it already hit. The opening 10x sweep deliberately has no
            // windup (it fires the instant the field forms) and no cue of its own, since BO_Ult1 is still
            // playing over it. The trailing condition suppresses a warning that the field wouldn't live
            // long enough to pay off.
            if (age % PulseInterval == PulseInterval - PulseWindup && age + PulseWindup < Lifetime
                && Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/BubblingOblivion/BO_VoidFieldPulse") { Volume = 0.7f }, Projectile.Center);
            }

            if (age > 0 && age % PulseInterval == 0)
                SpawnPulse(PulseDamageMultiplier);

            float pulse = 0.35f + 0.25f * MathF.Sin(age * 0.25f);
            Lighting.AddLight(Projectile.Center, pulse * 0.25f, pulse * 0.18f, pulse * 0.55f);
        }

        private void SpawnPulse(float damageMultiplier)
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            int damage = Math.Max((int)(Projectile.damage * damageMultiplier), 1);
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<BubblingOblivionVoidPulse>(), damage, Projectile.knockBack,
                Projectile.owner, FieldRadius);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Fade the whole field out over its last stretch so it doesn't simply blink out of existence.
            float fadeOut = MathHelper.Clamp((Lifetime - age) / 45f, 0f, 1f);

            // Static border, marking exactly how far the pulses reach. The field needs this: the core
            // alone says where the field is centred but nothing about how far it extends, and a
            // persistent damaging area the player can't see the edge of isn't fair.
            //
            // Drawn as two passes rather than one: a wide band in the disk's main colour for presence,
            // then a narrow near-white line down its middle for a crisp readable edge. Additive blending
            // means they combine into a white-hot centre fading out through lavender, which reads far
            // more clearly at a glance than any single mid-tone would — the border has to stay legible
            // across a 900px span and over whatever terrain happens to be behind it.
            // Slow pulse (period ~2.6s, vs the pulses' own 3s interval) — fast enough to read as alive,
            // slow enough not to compete with the outward sweeps for attention.
            float borderPulse = 0.55f + 0.2f * MathF.Sin(age * 0.04f);
            float borderAlpha = borderPulse * fadeOut;
            VoidVisuals.DrawDiamondRing(Projectile.Center, FieldRadius, 13f, new Color(VoidVisuals.Color0) * borderAlpha);
            VoidVisuals.DrawDiamondRing(Projectile.Center, FieldRadius, 4.5f, new Color(VoidVisuals.Color2) * (borderAlpha * 0.9f));

            // The miniature black hole at the centre — the same object that collapsed, growing back in.
            float growT = MathHelper.Clamp(age / (float)CoreGrowFrames, 0f, 1f);
            growT = growT * growT * (3f - 2f * growT);
            VoidVisuals.DrawBlackHole(Projectile.Center, CoreMeshRadius * growT, CoreShadow01,
                CoreDiskOuter, age / 60f, shaderSeed, fadeOut);

            BubblingOblivionDebug.DrawHitbox(Projectile, Color.Purple * 0.3f);
            return false;
        }
    }
}
