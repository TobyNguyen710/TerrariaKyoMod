using System;
using KyoMod.Common.GlobalNPCs;
using KyoMod.Common.Players;
using Microsoft.Xna.Framework;
using Terraria;
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

        private const int ExpandFrames = 40;       // ~0.67s to travel from the core out to the border. Kept
                                                   // proportional to the field's own radius so the sweep's
                                                   // apparent SPEED stays constant when the field is
                                                   // resized — otherwise a smaller field reads as sluggish
        private const float RingHalfThickness = 38f; // how wide the damaging/visible band is, in world units
        private const int DebuffDuration = 300;    // 5s — comfortably longer than the field's pulse interval,
                                                   // so stacks accumulate instead of lapsing between sweeps

        private int age;

        // Set by the field at spawn: how far out this sweep travels (the field's own L1 radius).
        public float MaxRadius => Projectile.ai[0];

        private float Progress => MathHelper.Clamp(age / (float)ExpandFrames, 0f, 1f);

        // Ease-out: leaves the core fast and decelerates into the border, so the sweep reads as something
        // released rather than something travelling at a constant speed.
        private float CurrentRadius
        {
            get
            {
                float t = Progress;
                return MaxRadius * (1f - (1f - t) * (1f - t));
            }
        }

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
            float fade = 1f - Progress * Progress; // holds bright, then drops away as it reaches the border
            float radius = CurrentRadius;

            // Same two-pass treatment as the field's border (wide main-colour band + narrow near-white
            // core line): this sweep is what actually deals the damage, so it has to stay at least as
            // readable as the boundary does — a damaging ring the player can't clearly see isn't fair.
            VoidVisuals.DrawDiamondRing(Projectile.Center, radius, RingHalfThickness, new Color(VoidVisuals.Color0) * fade);
            VoidVisuals.DrawDiamondRing(Projectile.Center, radius, RingHalfThickness * 0.32f, new Color(VoidVisuals.Color2) * (fade * 0.85f));
            BubblingOblivionDebug.DrawHitbox(Projectile, Color.Magenta * 0.3f);
            return false;
        }
    }
}
