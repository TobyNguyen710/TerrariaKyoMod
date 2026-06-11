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
		private float aiAccumulator = 0f;
		private bool skippedThisFrame = false;

		private const float SlowFactor = 0.35f;

		public override bool PreAI(NPC npc)
		{
			bool isSlowed = npc.HasBuff(ModContent.BuffType<TemporalSlow>());

			if (!isSlowed)
			{
				skippedThisFrame = false;
				if (wasSlowed) aiAccumulator = 0f;
				return true;
			}

			// Restore velocity from physics delta (only if already tracking this NPC).
			if (wasSlowed)
			{
				// physicsDelta = engine forces (gravity, collision) applied after last PostAI.
				Vector2 physicsDelta = npc.velocity - intendedVelocity * SlowFactor;
				float newX = intendedVelocity.X + physicsDelta.X * SlowFactor;
				float newY = intendedVelocity.Y + physicsDelta.Y * SlowFactor;

				// If tile collision zeroed a component, propagate that zero so the AI detects landing/wall hits.
				if (npc.velocity.X == 0f) { newX = 0f; intendedVelocity.X = 0f; }
				if (npc.velocity.Y == 0f) { newY = 0f; intendedVelocity.Y = 0f; }

				npc.velocity = new Vector2(newX, newY);
			}

			// AI budget: runs ~SlowFactor fraction of frames, starting from the very first slowed frame.
			aiAccumulator += SlowFactor;
			if (aiAccumulator < 1f)
			{
				// Skip frame — scale velocity here in case PostAI doesn't fire when PreAI returns false.
				intendedVelocity = npc.velocity;
				npc.velocity *= SlowFactor;
				wasSlowed = true;
				skippedThisFrame = true;
				return false;
			}

			aiAccumulator -= 1f;
			skippedThisFrame = false;
			return true;
		}

		public override void PostAI(NPC npc)
		{
			// Skip frames already handled velocity in PreAI; don't double-scale.
			if (skippedThisFrame) return;

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
