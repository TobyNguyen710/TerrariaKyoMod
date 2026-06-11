using KyoMod.Content.Buffs;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
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
		private readonly float[] savedAI = new float[4];
		private readonly float[] savedLocalAI = new float[4];

		private const float SlowFactor = 0.35f;

		// Tracks which NPCs are on a skip frame this update tick; read by TemporalSlowProjectileBlocker.
		internal static readonly HashSet<int> SkipFrameNPCs = new();

		public override bool PreAI(NPC npc)
		{
			bool isSlowed = npc.HasBuff(ModContent.BuffType<TemporalSlow>());

			if (!isSlowed)
			{
				skippedThisFrame = false;
				SkipFrameNPCs.Remove(npc.whoAmI);
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

			// Snapshot ai[] before AI runs so we can restore them in PostAI on skip frames.
			for (int i = 0; i < 4; i++)
			{
				savedAI[i] = npc.ai[i];
				savedLocalAI[i] = npc.localAI[i];
			}

			// AI budget: runs ~SlowFactor fraction of frames, starting from the first slowed frame.
			aiAccumulator += SlowFactor;
			if (aiAccumulator < 1f)
			{
				intendedVelocity = npc.velocity;
				npc.velocity *= SlowFactor;
				wasSlowed = true;
				skippedThisFrame = true;
				SkipFrameNPCs.Add(npc.whoAmI);
				return false;
			}

			aiAccumulator -= 1f;
			skippedThisFrame = false;
			SkipFrameNPCs.Remove(npc.whoAmI);
			return true;
		}

		public override void PostAI(NPC npc)
		{
			if (skippedThisFrame)
			{
				// Restore ai[] so attack/decision timers don't advance on skip frames.
				// Any projectile the AI fired this frame is cancelled by TemporalSlowProjectileBlocker.
				for (int i = 0; i < 4; i++)
				{
					npc.ai[i] = savedAI[i];
					npc.localAI[i] = savedLocalAI[i];
				}
				return;
			}

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
