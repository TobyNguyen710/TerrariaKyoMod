using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Common.GlobalProjectiles
{
	public class SlowedProjectileGlobal : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		public bool IsSlowed { get; private set; }
		private int slowTimer;
		private Vector2 prevVelocity;

		private const float SlowFactor = 0.35f;

		public void ApplySlow(Projectile projectile, int duration)
		{
			if (projectile.velocity.LengthSquared() < 0.01f) return;

			IsSlowed = true;
			slowTimer = duration;
			projectile.velocity *= SlowFactor;
			projectile.timeLeft = (int)(projectile.timeLeft * (1f / SlowFactor));
		}

		public override bool PreAI(Projectile projectile)
		{
			if (IsSlowed)
				prevVelocity = projectile.velocity;
			return true;
		}

		public override void PostAI(Projectile projectile)
		{
			if (!IsSlowed) return;

			if (slowTimer > 0)
			{
				slowTimer--;

				// Scale the velocity delta from AI this tick.
				// X uses SlowFactor: horizontal forces (homing, bounces) slow proportionally.
				// Y uses SlowFactor² to compensate for the extended timeLeft — otherwise gravity
				// accumulates over 4x longer and the arc drops 4x more than normal.
				Vector2 delta = projectile.velocity - prevVelocity;
				projectile.velocity = new Vector2(
					prevVelocity.X + delta.X * SlowFactor,
					prevVelocity.Y + delta.Y * SlowFactor * SlowFactor
				);

			}
			else
			{
				IsSlowed = false;
			}
		}
	}
}
