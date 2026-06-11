using System;
using KyoMod.Common.GlobalProjectiles;
using KyoMod.Content.Buffs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
	public class SpectroSlowPulse : ModProjectile
	{
		public override string Texture => "KyoMod/Content/Projectiles/Melee/TestSwordArrow";

		private const float MaxRadius = 300f;
		private const int Duration = 40;
		private const int SlowDuration = 240; // 4 seconds

		private float Progress => 1f - Projectile.timeLeft / (float)Duration;
		private float CurrentRadius => Progress * MaxRadius;

		public override void SetDefaults()
		{
			Projectile.width = Projectile.height = 2;
			Projectile.friendly = true;
			Projectile.tileCollide = false;
			Projectile.penetrate = -1;
			Projectile.timeLeft = Duration;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.damage = 0;
		}

		public override void AI()
		{
			Projectile.Center = Main.player[Projectile.owner].Center;

			float radius = CurrentRadius;
			SpawnRingDust(radius);

			// Apply slow to NPCs as ring reaches them
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.immortal) continue;
				if (Vector2.Distance(npc.Center, Projectile.Center) > radius) continue;
				if (!npc.HasBuff(ModContent.BuffType<TemporalSlow>()))
					npc.AddBuff(ModContent.BuffType<TemporalSlow>(), SlowDuration);
			}

			// Apply slow to enemy projectiles as ring reaches them (server/singleplayer only)
			if (Main.netMode != NetmodeID.MultiplayerClient)
			{
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active || p.friendly || p.whoAmI == Projectile.whoAmI) continue;
					if (Vector2.Distance(p.Center, Projectile.Center) > radius) continue;

					var slowData = p.GetGlobalProjectile<SlowedProjectileGlobal>();
					if (!slowData.IsSlowed)
						slowData.ApplySlow(p, SlowDuration);
				}
			}
		}

		private void SpawnRingDust(float radius)
		{
			if (radius < 4f) return;

			// Fade in at start, bright in middle, fade out at end
			float alpha = (float)Math.Sin(Progress * Math.PI);
			int dustCount = Math.Max(4, (int)(radius * 0.3f));

			for (int i = 0; i < dustCount; i++)
			{
				float angle = MathHelper.TwoPi * i / dustCount;
				Vector2 pos = Projectile.Center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;

				Dust d = Dust.NewDustDirect(pos - new Vector2(4f), 4, 4, DustID.GoldFlame);
				d.noGravity = true;
				d.velocity = Vector2.Zero;
				d.scale = 0.8f + alpha * 0.6f;
				d.alpha = (int)(255 * (1f - alpha));
			}

			// Inner glow particles near center of ring
			if (Progress > 0.1f && Progress < 0.9f)
			{
				for (int i = 0; i < 2; i++)
				{
					float angle = Main.rand.NextFloat(MathHelper.TwoPi);
					float r = radius * Main.rand.NextFloat(0.85f, 1.15f);
					Vector2 pos = Projectile.Center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * r;

					Dust d = Dust.NewDustDirect(pos - new Vector2(2f), 2, 2, DustID.GoldFlame);
					d.noGravity = true;
					d.velocity = (pos - Projectile.Center).SafeNormalize(Vector2.Zero) * 0.5f;
					d.scale = 0.5f;
				}
			}
		}

		public override bool PreDraw(ref Color lightColor) => false;
	}
}
