using System;
using KyoMod.Common.GlobalProjectiles;
using KyoMod.Content.Buffs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
	public class SpectroSlowPulse : ModProjectile
	{
		public override string Texture => "KyoMod/Content/Projectiles/Melee/TestSwordArrow";

		private const float MaxRadius = 400f;
		private const int Duration = 40;
		private const int SlowDuration = 240; // 4 seconds

		private float Progress => 1f - Projectile.timeLeft / (float)Duration;
		private float CurrentRadius => Progress * MaxRadius;
		private float TierScale => Projectile.ai[0] > 0f ? Projectile.ai[0] : 1f;

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
			Player owner = Main.player[Projectile.owner];
			Projectile.Center = owner.Center;

			owner.heldProj = Projectile.whoAmI;
			owner.itemRotation = -MathHelper.PiOver2;
			if (owner.direction != 1)
				owner.itemRotation += MathHelper.Pi;

			float radius = CurrentRadius;
			SpawnRingDust(radius);

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

		public override bool PreDraw(ref Color lightColor)
		{
			Player owner = Main.player[Projectile.owner];
			Texture2D sword = ModContent.Request<Texture2D>("KyoMod/Content/Items/Melee/CryingTides/CryingTides").Value;

			int dir = owner.direction;
			SpriteEffects flip = dir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			float extraAngle = dir < 0 ? MathHelper.PiOver2 : 0f;
			float drawRotation = -MathHelper.PiOver2 + MathHelper.PiOver4 + extraAngle;
			Vector2 drawOrigin = new Vector2(dir < 0 ? sword.Width : 0f, sword.Height);
			Vector2 drawPos = owner.Center - Main.screenPosition;

			Main.EntitySpriteDraw(sword, drawPos, null, lightColor, drawRotation, drawOrigin, TierScale * 1.2f, flip, 0);
			return false;
		}
	}
}
