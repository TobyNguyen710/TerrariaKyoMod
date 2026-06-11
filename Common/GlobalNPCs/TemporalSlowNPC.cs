using KyoMod.Content.Buffs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Common.GlobalNPCs
{
	public class TemporalSlowNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		private bool wasSlowed = false;
		private Vector2 intendedVelocity;

		private const float SlowFactor = 0.35f;

		public override bool PreAI(NPC npc)
		{
			if (wasSlowed && npc.HasBuff(ModContent.BuffType<TemporalSlow>()))
			{
				// physicsDelta = engine forces (gravity, collision) applied after last PostAI.
				// Scale by SlowFactor so engine gravity doesn't stack with AI-internal gravity
				// and drain the arc too fast. Flying NPCs (EoC etc.) have delta≈0, no change.
				Vector2 physicsDelta = npc.velocity - intendedVelocity * SlowFactor;
				npc.velocity = intendedVelocity + physicsDelta * SlowFactor;
			}
			return true;
		}

		public override void PostAI(NPC npc)
		{
			bool isSlowed = npc.HasBuff(ModContent.BuffType<TemporalSlow>());

			if (isSlowed)
			{
				intendedVelocity = npc.velocity;
				npc.velocity *= SlowFactor;
				wasSlowed = true;
			}
			else if (wasSlowed)
			{
				npc.velocity = intendedVelocity;
				wasSlowed = false;
			}
		}
	}
}
