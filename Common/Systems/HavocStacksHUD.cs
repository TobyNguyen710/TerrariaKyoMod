using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using KyoMod.Common.Players;
using KyoMod.Content.Items.Melee.CryingTides;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace KyoMod.Common.Systems
{
    public class HavocStacksHUD : ModSystem
    {
        private const int   PipCount = 10;
        private const float PipSize  = 7f;  // diamond half-diagonal
        private const float PipGap   = 4f;  // pixels between pip centres

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            Player player = Main.LocalPlayer;

            if (player.HeldItem?.ModItem is not CryingTidesBase sword || sword.ActiveForm != 1)
                return;

            int stacks = player.GetModPlayer<CryingTidesPlayer>().HavocStacks;

            // Stacks 1-10: crimson phase. Stacks 11-20: purple phase, display resets to 1-10.
            bool inPurplePhase = stacks > 10;
            int  displayFilled = inPurplePhase ? stacks - 10 : stacks;

            float step     = PipSize + PipGap;
            float rowWidth = PipCount * step - PipGap;
            // Divide by UIScale to convert game pixels → UI logical pixels
            Vector2 playerScreen = (player.Bottom - Main.screenPosition) / Main.UIScale;
            float startX = playerScreen.X - rowWidth * 0.5f;
            float startY = playerScreen.Y + 25f;

            Texture2D pixel  = TextureAssets.MagicPixel.Value;
            var       src    = new Rectangle(0, 0, 1, 1);
            float     rot    = MathHelper.PiOver4;
            var       origin = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < PipCount; i++)
            {
                bool filled = i < displayFilled;
                float x = startX + i * step;

                Color fillColor, emptyColor;
                if (inPurplePhase)
                {
                    // #9500ff (149, 0, 255) and its shades
                    fillColor  = i >= 8 ? new Color(200, 80,  255)  // bright purple
                               : i >= 4 ? new Color(149, 0,   255)  // #9500ff
                               :          new Color(100, 0,   180); // dim purple
                    emptyColor = new Color(30, 5, 50);
                }
                else
                {
                    // Crimson shades
                    fillColor  = i >= 8 ? new Color(255, 60,  20)
                               : i >= 4 ? new Color(200, 30,  10)
                               :          new Color(140, 20,   8);
                    emptyColor = new Color(40, 15, 15);
                }

                Color color = filled ? fillColor : emptyColor;

                spriteBatch.Draw(pixel, new Vector2(x, startY), src,
                    color * (filled ? 1f : 0.5f),
                    rot, origin,
                    new Vector2(PipSize, PipSize),
                    SpriteEffects.None, 0f);

                if (filled)
                {
                    spriteBatch.Draw(pixel, new Vector2(x, startY), src,
                        Color.White * 0.25f,
                        rot, origin,
                        new Vector2(PipSize * 0.45f, PipSize * 0.45f),
                        SpriteEffects.None, 0f);
                }
            }
        }
    }
}
