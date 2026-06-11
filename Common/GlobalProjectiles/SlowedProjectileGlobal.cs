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
		private float originalSpeed;
		private float slowedSpeed;

		public void ApplySlow(Projectile projectile, int duration)
		{
			float len = projectile.velocity.Length();
			if (len < 0.1f) return;

			originalSpeed = len;
			slowedSpeed = len * 0.25f;
			IsSlowed = true;
			slowTimer = duration;
			projectile.velocity = projectile.velocity.SafeNormalize(Vector2.Zero) * slowedSpeed;
		}

		public override void PostAI(Projectile projectile)
		{
			if (!IsSlowed) return;

			if (slowTimer > 0)
			{
				slowTimer--;
				// Re-clamp each tick so homing or re-accelerating projectiles stay slow
				if (projectile.velocity.LengthSquared() > slowedSpeed * slowedSpeed)
					projectile.velocity = projectile.velocity.SafeNormalize(Vector2.Zero) * slowedSpeed;
			}
			else
			{
				projectile.velocity = projectile.velocity.SafeNormalize(Vector2.Zero) * originalSpeed;
				IsSlowed = false;
			}
		}
	}
}
