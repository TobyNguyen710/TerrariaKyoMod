using KyoMod.Content.Buffs;
using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Common.GlobalNPCs
{
	public class TemporalSlowNPC : GlobalNPC
	{
		public override void PostAI(NPC npc)
		{
			if (npc.HasBuff(ModContent.BuffType<TemporalSlow>()))
				npc.velocity *= 0.2f;
		}
	}
}
