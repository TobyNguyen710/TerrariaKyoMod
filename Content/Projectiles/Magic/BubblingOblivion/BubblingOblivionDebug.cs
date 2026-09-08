using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Temporary debug visualization — draws a translucent overlay over a projectile's actual hitbox so
    // it can be screenshotted against the sprite. Flip Enabled on/off rather than removing call sites.
    internal static class BubblingOblivionDebug
    {
        public static bool Enabled = false;

        // Own guaranteed 1x1 white pixel — TextureAssets.MagicPixel produced a wildly oversized
        // rectangle for reasons that didn't trace back to any scale-math bug on our end, so stop relying
        // on assumptions about that asset's real dimensions/load state entirely.
        private static Texture2D _pixel;

        private static Texture2D GetPixel(GraphicsDevice device)
        {
            if (_pixel == null)
            {
                _pixel = new Texture2D(device, 1, 1);
                _pixel.SetData(new[] { Color.White });
            }
            return _pixel;
        }

        public static void DrawHitbox(Projectile projectile, Color color)
        {
            if (!Enabled)
                return;

            Texture2D pixel = GetPixel(Main.spriteBatch.GraphicsDevice);
            Rectangle hitbox = projectile.Hitbox;
            Vector2 position = new Vector2(hitbox.Center.X, hitbox.Center.Y) - Main.screenPosition;
            Vector2 origin = new Vector2(0.5f, 0.5f); // pixel is 1x1, so origin (0.5,0.5) is its center
            float scale = hitbox.Width; // pixel is 1x1, so scale directly equals the destination size

            Main.EntitySpriteDraw(pixel, position, null, color, 0f, origin, scale, SpriteEffects.None, 0);

            // Numeric readout, so we can tell definitively whether this is a draw bug or the hitbox
            // data itself is wrong, instead of judging by eye from a screenshot.
            string label = $"{hitbox.Width}x{hitbox.Height}";
            Vector2 textPos = position + new Vector2(-20f, -hitbox.Height / 2f - 20f);
            ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, FontAssets.MouseText.Value, label, textPos, Color.White, 0f, Vector2.Zero, Vector2.One);
        }
    }
}
