using System.Collections.Generic;
using KyoMod.Common.Players;
using KyoMod.Content.Items.Magic.BubblingOblivion;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace KyoMod.Common.UI
{
    // Same component/design AND same position as LiberationEnergyBar.cs (design doc §8: "the HUD bar
    // can reuse the same visual design... no need for a distinct look per weapon"), simplified to a
    // single fill color rather than two cost thresholds — Bubbling Oblivion's ultimate is one flat cost
    // shared by both forms (§2: both forms deal the same baseline DPS, no Spectro/Havoc-style cost
    // asymmetry). Bottom-left, not offset to the other side — the two bars can never actually be on
    // screen at the same time (each only draws while its own weapon is the held item, and Terraria
    // only lets you hold one item at once), so there was never a real overlap risk to design around.
    public class BubblingOblivionEnergyBar : ModSystem
    {
        private const int BarWidth = 18;
        private const int BarHeight = 160;
        private const int MarginX = 24;
        private const int MarginY = 60;

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int resourceBarsIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Resource Bars"));
            if (resourceBarsIndex == -1) return;

            layers.Insert(resourceBarsIndex + 1, new LegacyGameInterfaceLayer(
                "KyoMod: Bubbling Oblivion Energy Bar",
                DrawBar,
                InterfaceScaleType.UI));
        }

        private static bool DrawBar()
        {
            Player player = Main.LocalPlayer;
            if (player.HeldItem?.ModItem is not BubblingOblivionBase staff || !staff.HasUltimate)
                return true;

            var kyoPlayer = player.GetModPlayer<BubblingOblivionPlayer>();
            float ratio = kyoPlayer.LiberationEnergy / BubblingOblivionPlayer.MaxLiberationEnergy;

            Vector2 pos = new(MarginX, Main.screenHeight - MarginY - BarHeight);
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            Color buildingColor = new(124, 180, 222);
            Color readyColor = new(220, 120, 255); // matches the palette's cc30ae accent family
            Color fillColor = kyoPlayer.LiberationEnergy >= BubblingOblivionPlayer.UltimateCost ? readyColor : buildingColor;
            Color navy = new(10, 10, 15, 220);

            int fillHeight = (int)(BarHeight * ratio);
            int fillY = (int)pos.Y + (BarHeight - fillHeight);

            const int layerThickness = 2;
            Main.spriteBatch.Draw(pixel, new Rectangle((int)pos.X - 3 * layerThickness, (int)pos.Y - 3 * layerThickness, BarWidth + 6 * layerThickness, BarHeight + 6 * layerThickness), navy);
            Main.spriteBatch.Draw(pixel, new Rectangle((int)pos.X - 2 * layerThickness, (int)pos.Y - 2 * layerThickness, BarWidth + 4 * layerThickness, BarHeight + 4 * layerThickness), fillColor);
            Main.spriteBatch.Draw(pixel, new Rectangle((int)pos.X - 1 * layerThickness, (int)pos.Y - 1 * layerThickness, BarWidth + 2 * layerThickness, BarHeight + 2 * layerThickness), navy);

            Main.spriteBatch.Draw(pixel, new Rectangle((int)pos.X, (int)pos.Y, BarWidth, BarHeight), new Color(30, 30, 40, 200));
            Main.spriteBatch.Draw(pixel, new Rectangle((int)pos.X, fillY, BarWidth, fillHeight), fillColor);

            return true;
        }
    }
}
