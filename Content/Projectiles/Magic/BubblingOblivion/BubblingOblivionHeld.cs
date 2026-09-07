using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using KyoMod.Content.Items.Magic.BubblingOblivion;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Custom held-item visual for the staff. The item itself is invisible (Item.noUseGraphic) —
    // this projectile is drawn in its place, held steady with its tip always aimed at the cursor
    // (no swing), the same way CryingTidesSwungBase substitutes for the vanilla held-item draw.
    public class BubblingOblivionHeld : ModProjectile
    {
        public override string Texture => "KyoMod/Content/Items/Magic/BubblingOblivion/BubblingOblivionNoBubble";

        private const float HoldDistance = 5f;
        // Anchor height near the hand, blended by how vertical the aim is (raising the arm to aim
        // overhead genuinely sits higher than aiming out to the side — a single fixed value can't
        // satisfy both). Negative lifts the anchor up.
        private const float HandYOffsetHorizontal = 4f;
        // Just outside dead-level aim on the downward side only — diagonal-down wants a distinctly
        // higher value than dead-level itself, not a smooth ramp up from it (see DeadZone below).
        private const float HandYOffsetNearLevelDown = 10f;
        private const float HandYOffsetVertical = -15f;
        public const float DrawScale = 1.5f;

        // Grip point in local sprite pixels, for facing right. Must lie exactly on the shaft's
        // centerline (butt end (0,63) -> shaft point (52,11), i.e. x+y=63) or rotating toward vertical
        // visibly swings any off-axis error sideways. This sits ~35% of the way up from the butt end,
        // for a mid-shaft hold rather than gripping the very end.
        public static readonly Vector2 GripOrigin = new Vector2(18f, 45f);

        public Player Owner => Main.player[Projectile.owner];

        // The sprite is drawn diagonally (handle at bottom-left, tip at top-right), so its neutral
        // orientation already sits at +45 degrees — matches CryingTidesSwungSpectro's draw convention.
        public int LockedDirection { get; private set; } = 1;
        public float DrawRotation { get; private set; }

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            if (!Owner.active || Owner.dead || Owner.HeldItem.ModItem is not BubblingOblivionBase)
            {
                Projectile.Kill();
                return;
            }

            Vector2 aimDirection = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.UnitX * Owner.direction);
            LockedDirection = aimDirection.X >= 0f ? 1 : -1;

            // Anchor's horizontal position stays fixed regardless of aim angle (only rotation should
            // handle "pointing at the cursor" sideways — letting the full aim vector push the anchor
            // around, as before, made it drift disconnected from the hand at extreme angles). Its
            // height still blends toward the raised-arm position as the aim approaches vertical.
            //
            // The correct height isn't a smooth function of angle right at dead-level aim: perfectly
            // horizontal wants a distinctly lower value than even a slight downward diagonal does (most
            // likely vanilla's own arm/body pose snaps between discrete frames right around level aim).
            // DeadZone holds the flat horizontal value within a few degrees of level; just outside it on
            // the downward side, the target jumps straight to HandYOffsetNearLevelDown instead of
            // ramping up from the (too low) horizontal value, then eases the rest of the way to the
            // raised full-down height. Upward is unaffected — that side already blended smoothly.
            const float DeadZone = 0.08f;
            float absY = Math.Abs(aimDirection.Y);
            float t = absY <= DeadZone ? 0f : (absY - DeadZone) / (1f - DeadZone);
            float handY;
            if (absY <= DeadZone)
                handY = HandYOffsetHorizontal;
            else if (aimDirection.Y > 0f)
                handY = MathHelper.Lerp(HandYOffsetNearLevelDown, HandYOffsetVertical, t * t);
            else
                handY = MathHelper.Lerp(HandYOffsetHorizontal, HandYOffsetVertical, t);
            Projectile.Center = Owner.Center + new Vector2(LockedDirection * HoldDistance, handY);
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = aimDirection.ToRotation();

            Owner.ChangeDir(LockedDirection);
            Owner.heldProj = Projectile.whoAmI;

            float extraAngle = LockedDirection < 0 ? MathHelper.PiOver2 : 0f;
            DrawRotation = Projectile.rotation + MathHelper.PiOver4 + extraAngle;

            Owner.itemRotation = Projectile.rotation;
            if (LockedDirection != 1)
                Owner.itemRotation -= MathHelper.Pi;
            Owner.itemRotation = MathHelper.WrapAngle(Owner.itemRotation);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D staffTex = ModContent.Request<Texture2D>(Texture).Value;
            SpriteEffects flip = LockedDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 drawOrigin = LockedDirection < 0
                ? new Vector2(staffTex.Width - GripOrigin.X, GripOrigin.Y)
                : GripOrigin;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.EntitySpriteDraw(staffTex, drawPos, null, lightColor, DrawRotation, drawOrigin, DrawScale, flip, 0);
            return false;
        }
    }
}
