using System;
using KyoMod.Common.GlobalProjectiles;
using KyoMod.Content.Buffs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
	// PostMech ultimate (Spectro): a much bigger version of SpectroBeam's traveling crescent shape,
	// filled with SpectroSlowPulse's particle system instead of a drawn pixel-arc, applying a full
	// freeze (TemporalFreeze) instead of a partial slow. Not yet wired to any item — see
	// docs/CryingTides-SlashEffect-DesignDoc.md §11 for the trigger/item plumbing still to design.
	public class SpectroUltimateBeam : ModProjectile
	{
		public override string Texture => "KyoMod/Content/Projectiles/Melee/TestSwordArrow";

		public const  float TravelSpeed = 52f; // scale the spawn direction up to this on frame 0

		private const int   Lifetime    = 70;
		private const float ArcRadius   = 260f; // far bigger than SpectroBeam's 38f
		private const float ArcHalf     = MathHelper.Pi * 60f / 180f; // 60° each side = 120° total
		private const float Thickness   = 150f; // radial band width the crescent occupies
		private const int   FreezeDuration = 420; // 7 seconds

		private Vector2 TravelDir => Projectile.velocity.SafeNormalize(Vector2.UnitX);

		public override void SetDefaults()
		{
			Projectile.width       = Projectile.height = 2;
			Projectile.friendly    = true;
			Projectile.tileCollide = false;
			Projectile.penetrate   = -1;
			Projectile.timeLeft    = Lifetime;
			Projectile.DamageType  = DamageClass.Melee;
			Projectile.damage      = 0; // pure debuff applicator, same as SpectroSlowPulse
		}

		// Texture is only a placeholder (SpectroSlowPulse's own convention) — this is particle-only,
		// so suppress the default auto-draw that would otherwise render that placeholder sprite.
		public override bool PreDraw(ref Color lightColor) => false;

		public override void AI()
		{
			// Cast sound now plays from SpectroUltimateCast when the wind-up begins, not here.
			if (Projectile.timeLeft == Lifetime)
				Projectile.velocity = TravelDir * TravelSpeed;

			Vector2 dir       = TravelDir;
			Vector2 arcCenter = Projectile.Center - dir * ArcRadius; // crown of the crescent sits on Projectile.Center

			SpawnCrescentDust(arcCenter, dir);

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.immortal) continue;
				if (!IsInsideCrescent(npc.Center, arcCenter, dir)) continue;

				if (!npc.HasBuff(ModContent.BuffType<TemporalFreeze>()))
					npc.AddBuff(ModContent.BuffType<TemporalFreeze>(), FreezeDuration);
			}

			// Freeze enemy projectiles too, same as SpectroSlowPulse does with SlowedProjectileGlobal.
			if (Main.netMode != NetmodeID.MultiplayerClient)
			{
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active || p.friendly || p.whoAmI == Projectile.whoAmI) continue;
					if (!IsInsideCrescent(p.Center, arcCenter, dir)) continue;

					var freezeData = p.GetGlobalProjectile<FrozenProjectileGlobal>();
					if (!freezeData.IsFrozen)
						freezeData.ApplyFreeze(p, FreezeDuration);
				}
			}
		}

		private bool IsInsideCrescent(Vector2 point, Vector2 arcCenter, Vector2 dir)
		{
			Vector2 toPoint = point - arcCenter;
			float   dist    = toPoint.Length();
			if (dist < ArcRadius - Thickness * 0.5f || dist > ArcRadius + Thickness * 0.5f) return false;

			float relAngle = MathHelper.WrapAngle(toPoint.ToRotation() - dir.ToRotation());
			return Math.Abs(relAngle) <= ArcHalf;
		}

		private void SpawnCrescentDust(Vector2 arcCenter, Vector2 dir)
		{
			float travelAngle = dir.ToRotation();
			int   count       = 18;

			for (int i = 0; i < count; i++)
			{
				float   angle  = travelAngle + Main.rand.NextFloat(-ArcHalf, ArcHalf);
				float   radius = ArcRadius + Main.rand.NextFloat(-Thickness * 0.5f, Thickness * 0.5f);
				Vector2 pos    = arcCenter + angle.ToRotationVector2() * radius;

				Dust d = Dust.NewDustDirect(pos - new Vector2(4f), 4, 4, DustID.GoldFlame);
				d.noGravity = true;
				d.velocity  = dir * Main.rand.NextFloat(0.5f, 1.5f);
				d.scale     = Main.rand.NextFloat(1.0f, 1.6f);
			}
		}
	}
}
