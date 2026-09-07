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
        // The hand sits a little below Owner.Center — nudge the whole staff down to match.
        private const float HandYOffset = 3f;

        // Grip point in local sprite pixels, for facing right. Shifted from the stick's true bottom-left
        // end (0, 48) partway up the shaft toward the tip, since a staff is held around the middle, not
        // by the very butt end — pulls the visible tip back and lets the butt poke out slightly behind
        // the hand. Tune by eye if it still looks off.
        public static readonly Vector2 GripOrigin = new Vector2(8f, 40f);

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

            Projectile.Center = Owner.Center + aimDirection * HoldDistance + new Vector2(0f, HandYOffset);
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

            Main.EntitySpriteDraw(staffTex, drawPos, null, lightColor, DrawRotation, drawOrigin, 1f, flip, 0);
            return false;
        }
    }
}
