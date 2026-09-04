using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader;

namespace KyoMod
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class KyoMod : Mod
	{
		// Registering via ModKeybind (rather than checking a raw key) is what makes this show up
		// in Terraria's own Controls menu, rebindable by the player — "Q" here is only the default.
		public static ModKeybind CryingTidesUltimateKeybind;

		public override void Load()
		{
			CryingTidesUltimateKeybind = KeybindLoader.RegisterKeybind(this, "CryingTidesUltimate", "Q");
		}

		public override void Unload()
		{
			CryingTidesUltimateKeybind = null;
		}
	}
}
