using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles
{
	public class TestSwordArrow : ModProjectile
	{
		private bool IsSplit => Projectile.ai[1] == 1f;

		public override void SetDefaults()
		{
			Projectile.width = 10;
			Projectile.height = 10;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.timeLeft = 1200;
			Projectile.penetrate = 1;
			Projectile.localNPCHitCooldown = -1;
		}

		public override void AI()
		{
			Projectile.ai[0] += 1f;
			if (Projectile.ai[0] >= 15f)
			{
				Projectile.ai[0] = 15f;
				Projectile.velocity.Y += 0.1f;
			}

			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

			if (Projectile.velocity.Y > 16f)
				Projectile.velocity.Y = 16f;

			Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldFlame);
			dust.noGravity = true;
			dust.velocity *= 0.3f;
			dust.scale = IsSplit ? 0.5f : 0.8f;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (IsSplit)
				return;

			int splitCount = Main.rand.Next(2, 6);
			float speed = Projectile.velocity.Length();

			for (int i = 0; i < splitCount; i++)
			{
				Vector2 splitVel = Main.rand.NextVector2Unit() * (speed * Main.rand.NextFloat(0.6f, 1.2f));

				float clearance = MathHelper.Max(target.width, target.height) * 0.6f + 8f;
				Vector2 spawnPos = Projectile.Center + splitVel.SafeNormalize(Vector2.UnitY) * clearance;

				int idx = Projectile.NewProjectile(
					Projectile.GetSource_FromThis(),
					spawnPos,
					splitVel,
					Type,
					Projectile.damage / 2,
					Projectile.knockBack * 0.5f,
					Projectile.owner,
					ai1: 1f
				);

				// prevent splits from immediately hitting the same target they spawned inside
				if (idx < Main.maxProjectiles)
					Main.projectile[idx].localNPCImmunity[target.whoAmI] = 120;
			}
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
		}
	}
}
