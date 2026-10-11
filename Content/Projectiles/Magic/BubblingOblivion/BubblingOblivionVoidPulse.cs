using System;
using KyoMod.Common.GlobalNPCs;
using KyoMod.Common.Players;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // One outward sweep of the Void field (design doc §14.2/§14.3): a diamond ring expanding from the
    // field's core out to its border, damaging what it passes through and applying a VoidCollapse stack.
    //
    // The ultimate's 10x hit is NOT a separate explosion — it's the field's very first sweep, spawned by
    // BubblingOblivionVoidField with a big damage value and nothing else different. That's the whole
    // reason this exists as its own projectile rather than being baked into the field: explosion and
    // pulses can never drift apart visually or mechanically, because they're literally the same object.
    public class BubblingOblivionVoidPulse : ModProjectile
    {
        // Never drawn via its own texture — PreDraw fully overrides.
        public override string Texture => "KyoMod/Content/Items/Magic/BubblingOblivion/BubblingOblivion";

        public const int ExpandFrames = 40;         // ~0.67s to travel from the core out to the border. Kept
                                                   // proportional to the field's own radius so the sweep's
                                                   // apparent SPEED stays constant when the field is
                                                   // resized — otherwise a smaller field reads as sluggish
        private const float RingHalfThickness = 38f; // how wide the damaging/visible band is, in world units
        private const int DebuffDuration = 300;    // 5s — comfortably longer than the field's pulse interval,
                                                   // so stacks accumulate instead of lapsing between sweeps

        // A powered sweep is drawn as several nested bands instead of one, staggered so the trailing ones
        // sit further back — a wavefront with depth rather than a thicker line. Only the LEADING band
        // damages; the rest are purely visual, so this changes nothing mechanically.
        private const int MaxExtraRings = 2;
        private const float RingStagger = 0.09f;   // how far back each trailing band sits, in progress

        private int age;

        // Set by the field at spawn: how far out this sweep travels (the field's own L1 radius).
        public float MaxRadius => Projectile.ai[0];

        // Also set by the field: the damage multiplier this sweep was spawned with. Passing the multiplier
        // itself rather than an "is this the opener?" flag means there is one code path, not two — the
        // opener is simply the far end of a scale, and any future empowered pulse lands at the right
        // intensity on its own without a new branch.
        public float DamageMultiplier => Projectile.ai[1];

        // Normalised strength, 0 for an ordinary sweep and 1 for the ultimate's 10x opener. Anything at or
        // below normal damage reads as 0, which is what keeps ordinary pulses rendering exactly as they
        // did before any of this existed.
        public static float PowerOf(float damageMultiplier)
            => MathHelper.Clamp((damageMultiplier - 1f) / 9f, 0f, 1f);

        private float Power => PowerOf(DamageMultiplier);

        private int RingCount => 1 + (int)MathF.Round(Power * MaxExtraRings);

        private float Progress => MathHelper.Clamp(age / (float)ExpandFrames, 0f, 1f);

        // Ease-out: leaves the core fast and decelerates into the border, so the sweep reads as something
        // released rather than something travelling at a constant speed.
        private float RadiusAt(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return MaxRadius * (1f - (1f - t) * (1f - t));
        }

        private float CurrentRadius => RadiusAt(Progress);

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 8;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = ExpandFrames + 2;
            // Each target is hit at most ONCE by a given sweep. Without this the ring would re-hit every
            // tick it overlapped something, which for a slow-moving band is most of its lifetime.
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void OnSpawn(IEntitySource source)
        {
            // The trailing bands are still travelling when the leading one lands, so a powered sweep has
            // to outlive its own expansion or they would be cut off mid-flight. SetDefaults can't do this:
            // ai[] isn't populated until spawn, so the ring count isn't known there yet.
            int trail = (int)MathF.Ceiling(RingStagger * (RingCount - 1) * ExpandFrames);
            Projectile.timeLeft = ExpandFrames + 2 + trail;
        }

        public override void AI()
        {
            age++;
            Projectile.velocity = Vector2.Zero;

            // The hitbox has to grow with the ring. Colliding() below can only narrow down what the
            // engine already considers a candidate — anything outside this rectangle is never offered
            // for testing at all, so leaving it at its spawn size would mean the sweep silently stopped
            // damaging things a short distance out.
            int size = (int)(CurrentRadius * 2f) + (int)(RingHalfThickness * 2f);
            Vector2 center = Projectile.Center;
            Projectile.width = Projectile.height = Math.Max(size, 8);
            Projectile.Center = center; // resizing shifts what Center resolves to, so restore it

            // The extra frames a powered sweep lives for are for the trailing bands to finish arriving —
            // they are decoration, not part of the hit. Stopping here keeps the damaging window exactly
            // what it has always been, so adding trailing bands can't quietly widen it.
            if (age > ExpandFrames + 2)
                Projectile.friendly = false;
        }

        // Exact diamond-ring test. The ring is the set of points whose L1 distance (|dx| + |dy|) from the
        // centre lies within the band, which is the same metric VoidVisuals.DrawDiamondRing draws with —
        // so the damage area and the visible shape match by construction.
        //
        // Tested against the target's whole rectangle rather than just its centre point, so large enemies
        // are hit when the band crosses any part of them. Both bounds are exact for an axis-aligned rect:
        // |dx| + |dy| is separable, so its minimum over the rect is (min |dx|) + (min |dy|) and its
        // maximum is (max |dx|) + (max |dy|).
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 c = Projectile.Center;

            float nearestX = Math.Abs(MathHelper.Clamp(c.X, targetHitbox.Left, targetHitbox.Right) - c.X);
            float nearestY = Math.Abs(MathHelper.Clamp(c.Y, targetHitbox.Top, targetHitbox.Bottom) - c.Y);
            float minL1 = nearestX + nearestY;

            float farthestX = Math.Max(Math.Abs(targetHitbox.Left - c.X), Math.Abs(targetHitbox.Right - c.X));
            float farthestY = Math.Max(Math.Abs(targetHitbox.Top - c.Y), Math.Abs(targetHitbox.Bottom - c.Y));
            float maxL1 = farthestX + farthestY;

            float radius = CurrentRadius;
            return minL1 <= radius + RingHalfThickness && maxL1 >= radius - RingHalfThickness;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.GetGlobalNPC<VoidCollapseNPC>().AddStack(target, DebuffDuration);
            Main.player[Projectile.owner].GetModPlayer<BubblingOblivionPlayer>().AddLiberationEnergy();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // One band for an ordinary sweep, several for a powered one. At RingCount 1 every number below
            // collapses to exactly what this drew before trailing bands existed, so ordinary pulses are
            // untouched rather than merely similar.
            int rings = RingCount;
            for (int r = 0; r < rings; r++)
            {
                float t = Progress - r * RingStagger;
                if (t <= 0f)
                    continue; // hasn't left the core yet

                float radius = RadiusAt(t);
                float fade = (1f - t * t) * (1f - r * 0.28f); // holds bright, then drops away at the border
                float thickness = RingHalfThickness * (1f - r * 0.18f);

                // Same two-pass treatment as the field's border (wide main-colour band + narrow near-white
                // core line): this sweep is what actually deals the damage, so it has to stay at least as
                // readable as the boundary does — a damaging ring the player can't clearly see isn't fair.
                VoidVisuals.DrawDiamondRing(Projectile.Center, radius, thickness, new Color(VoidVisuals.Color0) * fade);
                VoidVisuals.DrawDiamondRing(Projectile.Center, radius, thickness * 0.32f, new Color(VoidVisuals.Color2) * (fade * 0.85f));
            }
            BubblingOblivionDebug.DrawHitbox(Projectile, Color.Magenta * 0.3f);
            return false;
        }
    }
}
