using System;
using System.Collections.Generic;
using KyoMod.Common.Players;
using KyoMod.Content.Items.Melee.CryingTides;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace KyoMod.Common.UI
{
	// Minimal fixed-position fill bar for Liberation Energy — only drawn while holding a CryingTides
	// tier with HasUltimate, so it doesn't clutter the HUD for players who haven't unlocked it yet.
	// Deliberately plain (no segmented thresholds, no shader) unlike Onikiri's Stance/Vigor bars —
	// the only job here is "can I tell at a glance how close I am to the ult," not a full HUD redesign.
	public class LiberationEnergyBar : ModSystem
	{
		private const int BarWidth  = 18;
		private const int BarHeight = 160;
		private const int MarginX   = 24; // from the left screen edge
		private const int MarginY   = 60; // from the bottom screen edge

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int resourceBarsIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Resource Bars"));
			if (resourceBarsIndex == -1) return;

			layers.Insert(resourceBarsIndex + 1, new LegacyGameInterfaceLayer(
				"KyoMod: Liberation Energy Bar",
				DrawBar,
				InterfaceScaleType.UI));
		}

		private static bool DrawBar()
		{
			Player player = Main.LocalPlayer;
			if (player.HeldItem?.ModItem is not CryingTidesBase sword || !sword.HasUltimate)
				return true;

			var kyoPlayer = player.GetModPlayer<CryingTidesPlayer>();
			float ratio = kyoPlayer.LiberationEnergy / CryingTidesPlayer.MaxLiberationEnergy;

			// Bottom-left, vertical — fills upward from the bottom as energy builds.
			Vector2 pos = new(MarginX, Main.screenHeight - MarginY - BarHeight);
			Texture2D pixel = TextureAssets.MagicPixel.Value;

			// Which threshold sits lower/higher is derived from the actual cost constants, not
			// hardcoded to "Spectro is the expensive one" — if a future balance pass makes Havoc's
			// ult the pricier one, the colors swap right along with it, no code change needed here.
			// UI-specific palette (deliberately distinct from the slash arcs' own BodyColor) —
			// user-picked for bar readability rather than matching the in-world slash tint.
			Color spectroColor  = new(246, 255, 115);
			Color havocColor    = new(168, 0, 53);
			Color buildingColor = new(124, 222, 150);

			float spectroCost = CryingTidesPlayer.SpectroUltimateCost;
			float havocCost   = CryingTidesPlayer.HavocUltimateCost;
			float lowerCost   = Math.Min(spectroCost, havocCost);
			float higherCost  = Math.Max(spectroCost, havocCost);
			Color lowerColor  = spectroCost <= havocCost ? spectroColor : havocColor;
			Color higherColor = spectroCost <= havocCost ? havocColor : spectroColor;

			Color fillColor = kyoPlayer.LiberationEnergy >= higherCost ? higherColor
				: kyoPlayer.LiberationEnergy >= lowerCost ? lowerColor
				: buildingColor;
			Color navy = new(10, 10, 15, 220);

			int fillHeight = (int)(BarHeight * ratio);
			int fillY      = (int)pos.Y + (BarHeight - fillHeight);

			// Border sandwich: navy -> energy color -> navy, one pixel-thick layer at a time, drawn
			// outside-in so each smaller rect overdraws the previous one's center, leaving only its
			// own ring visible.
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
