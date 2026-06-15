using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
    public class HavocSlashMark : ModProjectile
    {
        public override string Texture => "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheNoStack";

        private const int Lifetime = 14;

        public override void SetDefaults()
        {
            Projectile.width       = 1;
            Projectile.height      = 1;
            Projectile.timeLeft    = Lifetime;
            Projectile.penetrate   = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.hostile     = false;
            Projectile.friendly    = false;
        }

        public override bool? CanHitNPC(NPC target) => false;
        public override bool CanHitPlayer(Player target) => false;

        public override bool PreDraw(ref Color lightColor)
        {
            float alpha  = Projectile.timeLeft / (float)Lifetime;
            float angle  = Projectile.ai[0];
            float length = Projectile.ai[1];

            float fade = alpha * alpha;

            Texture2D    pixel   = TextureAssets.MagicPixel.Value;
            var          src     = new Rectangle(0, 0, 1, 1);
            // origin.Y = 0.5 keeps every segment centered on the same axis regardless of thickness
            var          origin  = new Vector2(0f, 0.5f);
            Vector2      center  = Projectile.Center - Main.screenPosition;
            Vector2      dir     = angle.ToRotationVector2();
            Vector2      from    = center - dir * length * 0.5f;

            Color crimson = new Color(200, 30, 30) * fade;

            // Outer crimson: thin caps (2px) on each end, full thickness (4px) in the middle
            const float capLen   = 2f;
            const float capThick = 2f;
            const float midThick = 4f;
            float       midLen   = length - capLen * 2f;

            Main.spriteBatch.Draw(pixel, from,                           src, crimson, angle, origin, new Vector2(capLen,  capThick), SpriteEffects.None, 0);
            Main.spriteBatch.Draw(pixel, from + dir * capLen,            src, crimson, angle, origin, new Vector2(midLen,  midThick), SpriteEffects.None, 0);
            Main.spriteBatch.Draw(pixel, from + dir * (length - capLen), src, crimson, angle, origin, new Vector2(capLen,  capThick), SpriteEffects.None, 0);

            // White core: single rect, 70% of total length, 1.5px thick
            float   coreLen  = length * 0.7f;
            Vector2 coreFrom = center - dir * coreLen * 0.5f;
            Main.spriteBatch.Draw(pixel, coreFrom, src, Color.White * (fade * 0.7f), angle, origin, new Vector2(coreLen, 1.5f), SpriteEffects.None, 0);

            return false;
        }
    }
}
