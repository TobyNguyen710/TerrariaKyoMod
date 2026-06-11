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
				// Scale by SlowFactor so engine gravity doesn't stack with AI-internal gravity.
				Vector2 physicsDelta = npc.velocity - intendedVelocity * SlowFactor;
				float newX = intendedVelocity.X + physicsDelta.X * SlowFactor;
				float newY = intendedVelocity.Y + physicsDelta.Y * SlowFactor;

				// If tile collision zeroed a component, propagate that zero to intendedVelocity
				// so the AI detects the landing/wall hit and doesn't carry stale momentum.
				if (npc.velocity.X == 0f) { newX = 0f; intendedVelocity.X = 0f; }
				if (npc.velocity.Y == 0f) { newY = 0f; intendedVelocity.Y = 0f; }

				npc.velocity = new Vector2(newX, newY);
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
