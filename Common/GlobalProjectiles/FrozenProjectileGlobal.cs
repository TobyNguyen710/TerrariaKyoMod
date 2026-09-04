using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Common.GlobalProjectiles
{
	// Full stop, not a partial slow like SlowedProjectileGlobal — skips AI entirely (rather than
	// scaling the velocity delta AI produces) and holds the projectile's real velocity to restore
	// once the freeze ends, same continuity guarantee as TemporalFreezeNPC.
	public class FrozenProjectileGlobal : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		public bool IsFrozen { get; private set; }
		private int freezeTimer;
		private Vector2 intendedVelocity;

		public void ApplyFreeze(Projectile projectile, int duration)
		{
			if (!IsFrozen)
				intendedVelocity = projectile.velocity;

			IsFrozen  = true;
			freezeTimer = duration;
			projectile.velocity = Vector2.Zero;
		}

		public override bool PreAI(Projectile projectile)
		{
			if (!IsFrozen) return true;

			projectile.velocity = Vector2.Zero;
			return false;
		}

		public override void PostAI(Projectile projectile)
		{
			if (!IsFrozen) return;

			// Re-zero in case engine physics (gravity, tile collision) touched velocity after PreAI.
			projectile.velocity = Vector2.Zero;

			// timeLeft still ticks down at the engine level even with AI skipped — counteract it so
			// a frozen projectile doesn't expire while sitting motionless mid-freeze.
			projectile.timeLeft++;

			if (freezeTimer > 0)
			{
				freezeTimer--;
			}
			else
			{
				IsFrozen = false;
				projectile.velocity = intendedVelocity;
			}
		}
	}
}
