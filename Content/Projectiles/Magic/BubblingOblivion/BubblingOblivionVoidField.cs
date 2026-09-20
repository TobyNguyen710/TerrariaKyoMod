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
        //
        // Sized so the SHADOW can hold an eye. The core is a black hole with an eye inside it, so the
        // black shadow is no longer just a dark centre — it's the room the eye lives in, and it has to be
        // big enough for a lid outline and an iris to be legible inside it without the detail landing
        // sub-pixel. That drives both numbers up together: a bigger mesh, and a bigger fraction of it
        // given over to shadow.
        //
        // These three are one setting in three parts and should be changed together.
        //
        // What actually matters visually is the ratio of black shadow to glowing disk. The shadow reaches
        // CoreShadow01 in UV, the disk reaches CoreShadow01 * CoreDiskOuter, and their product must stay
        // under the ~0.9 mesh-UV ceiling DrawBlackHole documents — past that the disk is sliced flat
        // against the mesh rim. Here: 0.32 * 2.7 = 0.864, so the shadow is ~37% of the lit extent.
        //
        // The shadow's size ON SCREEN is CoreMeshRadius * CoreShadow01, which is the number to hold fixed
        // when rebalancing. Dropping CoreShadow01 to give the disk a larger share would otherwise shrink
        // the eye as a side effect, so the mesh grows to compensate: 216 * 0.32 keeps the shadow at the
        // same ~69px it was at 150 * 0.46, and the extra size goes entirely into the disk sweeping
        // further out. The iris is measured off that shadow, so it is unaffected too.
        //
        // The disk is still tighter than the big black hole's 7, which would sprawl so far that the eye
        // shape stopped reading.
        private const float CoreMeshRadius = 216f;
        private const float CoreShadow01 = 0.32f;
        private const float CoreDiskOuter = 2.7f;

        private const int CoreGrowFrames = 22;     // the "pops back up" beat — the core re-emerging

        // The iris inside that shadow, as a fraction of the shadow's on-screen radius rather than in
        // absolute pixels — so it keeps fitting if the core is ever resized, instead of silently spilling
        // out over the disk. The black hole supplies everything around it, so this is the ONLY piece of
        // the eye actually drawn; it wants a generous dark margin so the shadow still reads as the eye's
        // outer form rather than as a rim around a ring.
        private const float IrisRadiusFrac = 0.34f;
        private const float IrisLineWidth = 4.2f;
        private const float IrisGlareGrowth = 0.35f;     // how much the iris dilates as the sweep releases
        private const float IrisFitFrac = 0.55f;         // iris height as a share of the shadow's, so the
                                                         // closing lids always squash it flat ahead of them
        private const float CoreGlareFlare = 0.12f;      // how much the black hole itself swells on a pulse

        // How squashed the black hole is at rest. This is the EYELID shape: 1 would be a true circular
        // black hole, and pulling it down to well under 1 is what turns the silhouette into an eye. The
        // whole render squashes with it, so the disk's over-the-top arcs flatten into lids rather than
        // staying as halos around a circle.
        private const float CoreSquashY = 0.62f;
        private const float ClosedSlitScale = 0.012f;    // a shut eye is still a visible seam, not nothing

        // The iris stops being drawn before the lids finish closing. It flattens with them for most of
        // the way, but once the shadow is squashed flatter than the iris ring is thick, the ring's own
        // outward normals turn nearly horizontal and it degenerates into a sub-pixel dash that aliases
        // rather than a ring. Retiring it just above that point is invisible in motion — by then the lids
        // have all but met over it anyway.
        private const float IrisFadeOutBelow = 0.35f;

        // Where the iris is allowed to wander, as a share of the shadow it sits in. The iris's own size
        // is subtracted from these at draw time, so what is left is genuine free room and the iris cannot
        // cross the lids however the core is resized.
        private const float GazeInsetX = 0.86f;
        private const float GazeInsetY = 0.80f;

        // Eyes don't drift, they SACCADE: hold still, then flick somewhere new. So the gaze picks a
        // target, snaps toward it fast, and sits there for a random dwell — a smooth wander would read as
        // something floating rather than something looking.
        private const int GazeDwellMin = 30;      // 0.5s
        private const int GazeDwellMax = 110;     // ~1.8s
        private const float GazeSnapSpeed = 0.34f;

        // How pointed the eye's corners are. A plain vertical squash gives an ellipse, whose ends are
        // round — this deepens the squash toward the sides so they pinch into the points of a lens
        // instead. 1 is the ellipse; higher is sharper. It doesn't change the eye's height or width,
        // only how it arrives at its tips.
        private const float CoreTipSharpness = 1.5f;

        // The blink cycle (see EyeOpenness), replacing what used to be a plain in-and-out scale pulse.
        // The eye draws shut over the windup, holds shut for a beat, then snaps open as the sweep fires.
        //
        // Closing is a far stronger telegraph than shrinking was: a sphere getting smaller is ambiguous,
        // whereas an eye squeezing shut and then glaring open has an unmistakable before and after. It's
        // driven by exactly the same WindupProgress() the audio cue uses, so it still cannot disagree
        // with when a sweep actually fires.
        private const float GlarePeakOpenness = 1.5f;
        private const int GlareFrames = 48;        // 0.8s to settle back after the snap open. The opening
                                                   // itself is instant, so this is purely the recovery: a
                                                   // longer settle reads as the eye relaxing rather than
                                                   // rubber-banding straight back to rest
        private const float CloseFraction = 0.85f; // fraction of the windup spent closing; the rest is
                                                   // held shut — the beat of silence before the bang

        // Floaty bob for the core, same idea as BigBubble's — but applied only to where the core is
        // DRAWN, never to Projectile.Center. The border, the pulse spawn point and the field's whole
        // geometry key off that centre, and none of them should wobble just because the core does.
        private const float CoreBobAmplitude = 4.5f;
        private const float CoreBobSpeed = 0.032f; // ~3.3s per cycle, slower than BigBubble's 2.1s

        private float shaderSeed;
        private float bobPhase;
        private int age;

        // Gaze, in normalised units inside a unit disk — scaled to real pixels only at draw time, so it
        // survives any resizing of the core. Purely visual and deliberately NOT synced: clients picking
        // different fixations costs nothing, exactly like shaderSeed and bobPhase above.
        private Vector2 gaze;
        private Vector2 gazeTarget;
        private int gazeTimer;

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

            UpdateGaze();

            float pulse = 0.35f + 0.25f * MathF.Sin(age * 0.25f);
            Lighting.AddLight(Projectile.Center, pulse * 0.25f, pulse * 0.18f, pulse * 0.55f);
        }

        // Picks a new fixation when the dwell runs out, then snaps toward it. The snap is a plain
        // exponential approach rather than a timed move: it covers most of the distance in a few frames
        // and trails off, which is close enough to how an eye actually arrives somewhere.
        private void UpdateGaze()
        {
            if (--gazeTimer <= 0)
            {
                gazeTimer = Main.rand.Next(GazeDwellMin, GazeDwellMax + 1);

                // Targets come from inside a unit DISK, not a box. The socket is far wider than it is
                // tall, so the two axes aren't independent — at full horizontal deflection there is no
                // vertical room left at all. A disk encodes exactly that: pushing one component toward 1
                // forces the other toward 0, so the iris can never slide out through a pointed tip.
                //
                // Radius is left linear instead of sqrt-distributed, which biases fixations toward the
                // centre. That's wanted — an eye that spent equal time in the corners would look frantic.
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                gazeTarget = angle.ToRotationVector2() * Main.rand.NextFloat();
            }

            gaze = Vector2.Lerp(gaze, gazeTarget, GazeSnapSpeed);
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

        // How open the eye is: 1 at rest, 0 fully shut, above 1 while it glares. Drives the lid's height
        // and the iris's visibility; the black hole around it only flares slightly, since the blink is
        // the eye's to perform, not the void's.
        private float EyeOpenness()
        {
            if (age <= 0)
                return 0f;

            // Glare: for a short window right after a sweep fires, the eye is thrown wide and settles.
            int intoCycle = age % PulseInterval;
            if (intoCycle < GlareFrames)
            {
                float t = intoCycle / (float)GlareFrames;
                return MathHelper.Lerp(GlarePeakOpenness, 1f, t * t * (3f - 2f * t));
            }

            // Closing as the next sweep charges. WindupProgress() already returns 0 when no sweep is
            // actually coming, so the eye simply rests open instead of closing on nothing.
            float windupT = WindupProgress();
            if (windupT > 0f)
            {
                // Smoothstepped rather than accelerating, so the lid travels deliberately instead of
                // slamming — this is a slow, menacing close, and the snap belongs entirely to the open.
                float closeT = MathHelper.Clamp(windupT / CloseFraction, 0f, 1f);
                return 1f - closeT * closeT * (3f - 2f * closeT);
            }

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

            // The core — the same black hole that collapsed, grown back in, now with an eye inside it.
            // The eye blinks with the pulse cycle (see EyeOpenness), so the charge reads even for a
            // player watching the core rather than the border.
            //
            // Draw order matters: the black hole first, then the eye over its shadow. The eye is additive
            // glow and the shadow is already black, so it simply lights up from inside the void — there
            // is nothing to mask and nothing dark to paint.
            float growT = MathHelper.Clamp(age / (float)CoreGrowFrames, 0f, 1f);
            growT = growT * growT * (3f - 2f * growT);

            float open = EyeOpenness();

            Vector2 coreDrawPos = Projectile.Center
                + new Vector2(0f, MathF.Sin(age * CoreBobSpeed + bobPhase) * CoreBobAmplitude);

            // The BLACK HOLE is the eyelids. Closing the eye squashes the whole render vertically, so the
            // lids that come together are the disk's own lensed arcs — there is no separate lid drawn,
            // and there is nothing that can drift out of register with the thing it is supposed to be
            // part of. At full close it collapses to a bright horizontal seam.
            //
            // The glare opens it PAST rest toward circular, which is why the squash is capped at 1: a
            // scale above that would stretch the black hole taller than it is wide, and a vertically
            // oval black hole reads as a bug rather than as a wide-open eye.
            float squashY = MathHelper.Clamp(CoreSquashY * open, ClosedSlitScale, 1f);

            float coreRadius = CoreMeshRadius * growT * (1f + CoreGlareFlare * (open - 1f));
            // frontBand: 0. The disk's near side would otherwise rule a bright line straight across the
            // shadow, which reads as a scratch on the lens once the shadow is a pupil. The ultimate's own
            // black hole keeps it (the default), since there it's what makes the ribbon continuous.
            VoidVisuals.DrawBlackHole(coreDrawPos, coreRadius, CoreShadow01, CoreDiskOuter,
                age / 60f, shaderSeed, fadeOut, squashY, frontBand: 0f, tipSharpness: CoreTipSharpness);

            // The iris is measured off the shadow it sits in — including the shadow's SQUASHED height —
            // so the closing lids always flatten it ahead of themselves and it can never be left poking
            // through a shut eye. Additive geometry can't be occluded by anything drawn over it, so
            // fitting it to the lids is the only way it can hide behind them.
            //
            // Width dilates on the glare but never drives the height above it, so the iris stays round
            // when open rather than going oval.
            float shadowHalfWidth = coreRadius * CoreShadow01;
            float shadowHalfHeight = shadowHalfWidth * squashY;
            float irisHalfWidth = shadowHalfWidth * IrisRadiusFrac
                * (1f + IrisGlareGrowth * MathF.Max(open - 1f, 0f));
            float irisHalfHeight = MathF.Min(irisHalfWidth, shadowHalfHeight * IrisFitFrac);

            // Gaze. The normalised offset is scaled by the room actually left over once the iris's own
            // size is taken out of the shadow, so full deflection puts the iris against the lids and
            // never past them. Both shrink as the eye closes — and go to zero, recentring the gaze —
            // which is correct: a shut eye has nowhere to look.
            float gazeRangeX = MathF.Max(shadowHalfWidth * GazeInsetX - irisHalfWidth, 0f);
            float gazeRangeY = MathF.Max(shadowHalfHeight * GazeInsetY - irisHalfHeight, 0f);
            Vector2 irisPos = coreDrawPos + new Vector2(gaze.X * gazeRangeX, gaze.Y * gazeRangeY);

            float irisFade = MathHelper.Clamp(open / IrisFadeOutBelow, 0f, 1f);
            VoidVisuals.DrawIris(irisPos, irisHalfWidth, irisHalfHeight, IrisLineWidth,
                age / 60f, shaderSeed, fadeOut * irisFade);

            BubblingOblivionDebug.DrawHitbox(Projectile, Color.Purple * 0.3f);
            return false;
        }
    }
}
