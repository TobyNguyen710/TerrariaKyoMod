using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Content.Buffs
{
	public class TemporalFreeze : ModBuff
	{
		// Reuse TemporalSlow's icon as a placeholder until a dedicated sprite is made.
		public override string Texture => "KyoMod/Content/Buffs/TemporalSlow";

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.buffNoSave[Type] = true;
		}
	}
}
