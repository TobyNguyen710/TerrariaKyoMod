using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace KyoMod
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class KyoMod : Mod
	{
		// Registering via ModKeybind (rather than checking a raw key) is what makes this show up
		// in Terraria's own Controls menu, rebindable by the player — "Q" here is only the default.
		public static ModKeybind CryingTidesUltimateKeybind;

		// Compiled from docs/Shaders/SlashInk.fx via EasyXnb (HiDef profile, SM3) — source stays
		// local/uncommitted, only the compiled Effects/SlashInk.xnb ships with the mod.
		public static Asset<Effect> SlashInkShader;

		public override void Load()
		{
			CryingTidesUltimateKeybind = KeybindLoader.RegisterKeybind(this, "CryingTidesUltimate", "Q");

			// First attempt at a real shader for the slash arc (compiled HiDef/SM3, see
			// docs/Shaders/SlashInk.fx) — untested against this machine's actual GraphicsDevice
			// profile, so a load failure here must not take the whole mod down with it. On failure
			// SlashInkShader stays null and SlashArcEffect's own null check falls back to the
			// existing flat-color BasicEffect path.
			if (!Main.dedServ)
			{
				try
				{
					SlashInkShader = Assets.Request<Effect>("Effects/SlashInk", AssetRequestMode.ImmediateLoad);
				}
				catch (Exception e)
				{
					Logger.Warn($"SlashInk shader failed to load, falling back to flat-color slash arcs: {e}");
					SlashInkShader = null;
				}
			}
		}

		public override void Unload()
		{
			CryingTidesUltimateKeybind = null;
			SlashInkShader = null;
		}
	}
}
