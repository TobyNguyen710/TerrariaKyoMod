using KyoMod.Common.GlobalNPCs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace KyoMod.Common.GlobalProjectiles
{
	public class TemporalSlowProjectileBlocker : GlobalProjectile
	{
		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (source is EntitySource_Parent parentSource &&
				parentSource.Entity is NPC sourceNpc &&
				TemporalSlowNPC.SkipFrameNPCs.Contains(sourceNpc.whoAmI))
			{
				projectile.active = false;
			}
		}
	}
}
