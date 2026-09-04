using KyoMod.Content.Buffs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Common.GlobalNPCs
{
	// Full immobilization, not a partial slow — same continuity guarantee as TemporalSlowNPC
	// (velocity resumes exactly where it left off once the debuff ends), just without the
	// partial-frame accumulator since nothing should move or think at all while frozen.
	public class TemporalFreezeNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		private bool wasFrozen = false;
		private Vector2 intendedVelocity;
		private bool originalNoGravity;

		public override bool PreAI(NPC npc)
		{
			bool isFrozen = npc.HasBuff(ModContent.BuffType<TemporalFreeze>());
			if (!isFrozen)
			{
				// Restore here, before AI runs this frame — restoring in PostAI instead let the NPC's
				// own AI execute first against a stale velocity=0 (from the last frozen frame), which
				// is what dropped its vertical speed/gravity on unfreeze instead of resuming it.
				if (wasFrozen)
				{
					npc.velocity  = intendedVelocity;
					npc.noGravity = originalNoGravity;
					wasFrozen     = false;
				}
				return true;
			}

			// Capture the real velocity only once, right as the freeze begins — never overwritten
			// again while frozen, since the AI (which would normally set a new velocity) never runs.
			if (!wasFrozen)
			{
				intendedVelocity  = npc.velocity;
				originalNoGravity = npc.noGravity;
			}

			// Zeroing velocity in Pre/PostAI alone still lets the engine's own gravity/tile-collision
			// step add to it between our hooks (the same effect TemporalSlowNPC has to compensate for
			// via physicsDelta) — noGravity stops that at the source instead of chasing it after the
			// fact, which is why the NPC kept visibly falling before this was added.
			npc.noGravity = true;
			npc.velocity  = Vector2.Zero;
			wasFrozen     = true;
			return false;
		}

		public override void PostAI(NPC npc)
		{
			// Only need to re-zero mid-freeze here now — the unfreeze restore moved to PreAI (above)
			// so it happens before that frame's AI runs, not after.
			if (npc.HasBuff(ModContent.BuffType<TemporalFreeze>()))
				npc.velocity = Vector2.Zero;
		}

		// Knockback is blocked at the source rather than fought every frame in PreAI/PostAI —
		// cleaner than letting a hit set velocity and then immediately zeroing it out again.
		public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
		{
			if (npc.HasBuff(ModContent.BuffType<TemporalFreeze>()))
				modifiers.Knockback *= 0f;
		}

		public override void DrawEffects(NPC npc, ref Color drawColor)
		{
			if (npc.HasBuff(ModContent.BuffType<TemporalFreeze>()))
				drawColor = Color.Lerp(drawColor, new Color(255, 200, 60), 0.35f);
		}
	}
}
