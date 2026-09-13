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

        // The core's breathing cycle (see CoreBreath): pulled tight while a sweep charges, thrown wide
        // as it releases.
        private const float InhaleScale = 0.58f;
        private const float ExhalePeakScale = 1.5f;
        private const int ExhaleFrames = 48;       // 0.8s to settle back after the punch. The snap outward
                                                   // is instant, so this is purely the recovery: a longer
                                                   // settle reads as the core slowly relaxing rather than
                                                   // rubber-banding straight back to rest

        // Floaty bob for the core, same idea as BigBubble's — but applied only to where the core is
        // DRAWN, never to Projectile.Center. The border, the pulse spawn point and the field's whole
        // geometry key off that centre, and none of them should wobble just because the core does.
        private const float CoreBobAmplitude = 4.5f;
        private const float CoreBobSpeed = 0.032f; // ~3.3s per cycle, slower than BigBubble's 2.1s

        private float shaderSeed;
        private float bobPhase;
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
            bobPhase = Main.rand.NextFloat(MathHelper.TwoPi); // random phase, so repeat casts aren't in lockstep

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
            // playing over it.
            //
            // The bound is <= Lifetime, not <, specifically so the FINAL pulse still gets its warning.
            // The final pulse lands on the field's very last tick, which an exclusive bound skipped —
            // leaving exactly one unannounced sweep, and the most punishing one to be surprised by since
            // the player has by then learned every pulse is telegraphed. The sweep itself is its own
            // projectile, so it survives and completes normally even though the field dies that tick.
            if (age % PulseInterval == PulseInterval - PulseWindup && age + PulseWindup <= Lifetime
                && Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/BubblingOblivion/BO_VoidFieldPulse") { Volume = 0.7f }, Projectile.Center);
            }

            if (age > 0 && age % PulseInterval == 0)
                SpawnPulse(PulseDamageMultiplier);

            float pulse = 0.35f + 0.25f * MathF.Sin(age * 0.25f);
            Lighting.AddLight(Projectile.Center, pulse * 0.25f, pulse * 0.18f, pulse * 0.55f);
        }

        // 0 when no sweep is imminent, ramping 0 -> 1 across the windup window just before one fires.
        // Deliberately shared by the audio cue and the visual telegraph, computing "will a sweep actually
        // fire?" the same way for both — if those two conditions were written separately they could
        // disagree, and a field that flashes a warning without firing (or fires without warning) is worse
        // than having no telegraph at all.
        private float WindupProgress()
        {
            if (age <= 0)
                return 0f;

            int intoCycle = age % PulseInterval;
            int windupStart = PulseInterval - PulseWindup;
            if (intoCycle < windupStart)
                return 0f;

            if (age + (PulseInterval - intoCycle) > Lifetime)
                return 0f; // the field won't live long enough to fire this one

            return (intoCycle - windupStart) / (float)PulseWindup;
        }

        // Size multiplier for the core, breathing in time with the pulse cycle: it draws INWARD as the
        // sweep charges, then punches outward as the sweep releases. Shrinking during the windup (rather
        // than swelling) is what makes it read as an inhale — the core gathering itself in, with the
        // converging ring closing on it at the same time — so the release genuinely lands as an exhale.
        private float CoreBreath()
        {
            if (age <= 0)
                return 1f;

            // Exhale: for a short window right after a sweep fires, the core is thrown wide and settles.
            int intoCycle = age % PulseInterval;
            if (intoCycle < ExhaleFrames)
            {
                float t = intoCycle / (float)ExhaleFrames;
                return MathHelper.Lerp(ExhalePeakScale, 1f, t * t * (3f - 2f * t));
            }

            // Inhale: pulled tight as the next sweep charges. WindupProgress() already returns 0 when no
            // sweep is actually coming, so the core simply rests instead of inhaling toward nothing.
            float windupT = WindupProgress();
            if (windupT > 0f)
                return MathHelper.Lerp(1f, InhaleScale, windupT * windupT);

            return 1f;
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

            // Windup telegraph. The 1.4s warning used to be sound only, which is unfair for a damaging
            // ring — with audio down, or in a noisy fight, there was no warning at all.
            //
            // Shown as a thin ring CONVERGING on the core, i.e. the exact inverse of the sweep that
            // follows: an inhale before the exhale. Reusing the diamond keeps it in the same visual
            // language rather than inventing a new symbol, and the opposite direction of travel (plus a
            // much thinner, dimmer band) is what keeps it from being mistaken for the sweep itself.
            float windupT = WindupProgress();
            if (windupT > 0f)
            {
                float convergeRadius = FieldRadius * (1f - windupT * windupT); // accelerates inward
                float convergeAlpha = windupT * fadeOut * 0.9f;                // and brightens as it closes
                VoidVisuals.DrawDiamondRing(Projectile.Center, convergeRadius, 6f,
                    new Color(VoidVisuals.Color2) * convergeAlpha);
            }

            // The miniature black hole at the centre — the same object that collapsed, growing back in.
            // It also breathes with the pulse cycle (see CoreBreath), so the charge reads even for a
            // player watching the core rather than the border.
            float growT = MathHelper.Clamp(age / (float)CoreGrowFrames, 0f, 1f);
            growT = growT * growT * (3f - 2f * growT);
            Vector2 coreDrawPos = Projectile.Center
                + new Vector2(0f, MathF.Sin(age * CoreBobSpeed + bobPhase) * CoreBobAmplitude);
            VoidVisuals.DrawBlackHole(coreDrawPos, CoreMeshRadius * growT * CoreBreath(), CoreShadow01,
                CoreDiskOuter, age / 60f, shaderSeed, fadeOut);

            BubblingOblivionDebug.DrawHitbox(Projectile, Color.Purple * 0.3f);
            return false;
        }
    }
}
