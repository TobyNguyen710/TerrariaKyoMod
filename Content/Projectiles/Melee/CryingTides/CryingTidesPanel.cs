using System;
using KyoMod.Content.Items.Melee.CryingTides;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
    public class CryingTidesPanel : ModProjectile
    {
        public override string Texture => "KyoMod/Content/Items/Melee/CryingTides/CryingTides";

        private const int IconSize = 34;
        private const int Padding = 6;
        private const int FormCount = 2;

        private const float PanelYOffset = -80f;

        private Player Owner => Main.player[Projectile.owner];

        private Vector2 GetPanelTopLeft()
        {
            int panelWidth = Padding + FormCount * (IconSize + Padding);
            Vector2 playerScreen = Owner.Center - Main.screenPosition;
            return playerScreen + new Vector2(-panelWidth / 2f, PanelYOffset);
        }

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 2;
            Projectile.tileCollide = false;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            Projectile.Center = Owner.Center;

            if (!Owner.active || Owner.dead)
            {
                Projectile.Kill();
                return;
            }

            if (Main.myPlayer != Projectile.owner)
                return;

            if (Owner.HeldItem.ModItem is not CryingTidesBase sword)
                return;

            int panelWidth = Padding + FormCount * (IconSize + Padding);
            int panelHeight = Padding * 2 + IconSize;
            Vector2 panelTopLeft = GetPanelTopLeft();
            Rectangle panelRect = new Rectangle((int)panelTopLeft.X, (int)panelTopLeft.Y, panelWidth, panelHeight);

            if (!panelRect.Contains(Main.mouseX, Main.mouseY))
                return;

            Main.LocalPlayer.mouseInterface = true;

            for (int i = 0; i < FormCount; i++)
            {
                bool isUnlocked = i == 0 || sword.HasHavocForm;
                if (!isUnlocked)
                    continue;

                Rectangle iconRect = new Rectangle(
                    (int)(panelTopLeft.X + Padding + i * (IconSize + Padding)),
                    (int)(panelTopLeft.Y + Padding),
                    IconSize, IconSize);

                if (iconRect.Contains(Main.mouseX, Main.mouseY) && Main.mouseLeft && Main.mouseLeftRelease)
                {
                    sword.ActiveForm = i;
                    sword.PanelOpen = false;
                    Projectile.Kill();
                    return;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Main.myPlayer != Projectile.owner)
                return false;

            if (Owner.HeldItem.ModItem is not CryingTidesBase sword)
                return false;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Texture2D[] iconTextures =
            {
                ModContent.Request<Texture2D>("KyoMod/Content/Projectiles/Melee/CryingTides/SpectroIcon").Value,
                ModContent.Request<Texture2D>("KyoMod/Content/Projectiles/Melee/CryingTides/HavocIcon").Value,
            };

            int panelWidth = Padding + FormCount * (IconSize + Padding);
            int panelHeight = Padding * 2 + IconSize;
            Vector2 panelTopLeft = GetPanelTopLeft();

            // Background
            sb.Draw(pixel, new Rectangle((int)panelTopLeft.X, (int)panelTopLeft.Y, panelWidth, panelHeight),
                new Color(15, 15, 25, 210));

            Color[] formColors = { new Color(255, 215, 80), new Color(200, 60, 60) };
            bool[] formUnlocked = { true, sword.HasHavocForm };

            for (int i = 0; i < FormCount; i++)
            {
                Vector2 iconPos = panelTopLeft + new Vector2(Padding + i * (IconSize + Padding), Padding);
                bool isActive = sword.ActiveForm == i;
                bool isUnlocked = formUnlocked[i];

                Color formColor = isUnlocked ? formColors[i] : Color.Gray * 0.5f;
                float alpha = isActive ? 1f : (isUnlocked ? 0.55f : 0.3f);

                // Icon background
                sb.Draw(pixel, new Rectangle((int)iconPos.X, (int)iconPos.Y, IconSize, IconSize),
                    isActive ? new Color(50, 50, 70, 220) : new Color(25, 25, 35, 180));

                // Active form border
                if (isActive)
                {
                    const int bw = 2;
                    sb.Draw(pixel, new Rectangle((int)iconPos.X, (int)iconPos.Y, IconSize, bw), formColor);
                    sb.Draw(pixel, new Rectangle((int)iconPos.X, (int)iconPos.Y + IconSize - bw, IconSize, bw), formColor);
                    sb.Draw(pixel, new Rectangle((int)iconPos.X, (int)iconPos.Y, bw, IconSize), formColor);
                    sb.Draw(pixel, new Rectangle((int)iconPos.X + IconSize - bw, (int)iconPos.Y, bw, IconSize), formColor);
                }

                // Form icon
                Texture2D iconTex = iconTextures[i];
                float scale = (IconSize - 4f) / Math.Max(iconTex.Width, iconTex.Height);
                Vector2 center = iconPos + new Vector2(IconSize * 0.5f);
                sb.Draw(iconTex, center, null, Color.White * alpha,
                    0f, new Vector2(iconTex.Width * 0.5f, iconTex.Height * 0.5f), scale, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}
