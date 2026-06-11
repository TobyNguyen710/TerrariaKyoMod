using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Content.Buffs
{
	public class TemporalSlow : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.buffNoSave[Type] = true;
		}
	}
}
