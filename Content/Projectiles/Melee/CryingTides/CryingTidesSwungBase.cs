using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
	public abstract class CryingTidesSwungBase : ModProjectile
	{
		protected bool initialized = false;
		protected Vector2 direction = Vector2.Zero;
		protected int lockedDirection = 1;
		protected float prevRotation = 0f;
		private bool slashArcSpawned = false;

		public int ComboState => (int)Projectile.ai[0];
		protected float TierScale => Projectile.ai[1] > 0f ? Projectile.ai[1] : 1f;
		protected float TierArc => Projectile.ai[2] > 0f ? Projectile.ai[2] : 1f;
		public Player Owner => Main.player[Projectile.owner];

		protected const float BaseSwingWidth = MathHelper.PiOver2 * 2.2f;
		protected const float HoldDistance = 5f;

		protected virtual float MaxTimeNormal => 15f;
		protected virtual float MaxTimeSpin => 25f;
		protected virtual float BladeLength => 85f;

		protected float ActualMaxTime => ComboState == 2 ? MaxTimeSpin : MaxTimeNormal;
		protected float Timer => ActualMaxTime - Projectile.timeLeft;
		protected float SwingRatio => MathHelper.Clamp(Timer / (ActualMaxTime - 1f), 0f, 1f);

		// Shared by both hit detection and the cosmetic slash-arc visual so they can never drift apart.
		protected float SlashRadius => BladeLength * TierScale * 2.5f;

		public override void SetDefaults()
		{
			Projectile.DamageType = DamageClass.Melee;
			Projectile.width = Projectile.height = 60;
			Projectile.tileCollide = false;
			Projectile.friendly = true;
			Projectile.penetrate = -1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 25;
		}

		// Sector-sweep test: checks whether the enemy's angle falls within the arc swept *this frame*
		// (prevRotation -> current rotation), not just the sword's exact instantaneous angle. A pure
		// instantaneous-line test leaves real gaps near the tip at large radii between frames — this
		// matches the technique already proven correct in Havoc's own override.
		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			float bladeLen = SlashRadius;
			Vector2 center = Owner.Center;
			float cx = MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right);
			float cy = MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom);
			if (Vector2.DistanceSquared(center, new Vector2(cx, cy)) > bladeLen * bladeLen)
				return false;

			Vector2 toEnemy = targetHitbox.Center.ToVector2() - center;
			if (toEnemy.LengthSquared() < 1f)
				return true;

			float enemyAngle = toEnemy.ToRotation();
			const float buffer = 0.14f;

			float delta    = MathHelper.WrapAngle(Projectile.rotation - prevRotation);
			int   sweepDir = delta >= 0f ? 1 : -1;
			float arcSwept = delta * sweepDir;
			float relAngle = MathHelper.WrapAngle((enemyAngle - prevRotation) * sweepDir);
			return relAngle >= -buffer && relAngle <= arcSwept + buffer;
		}

		protected static float EaseOut(float t)
		{
			float inv = 1f - t;
			return 1f - inv * inv * inv;
		}

		protected static float SmoothStep(float t) => t * t * (3f - 2f * t);

		public override void AI()
		{
			if (!initialized)
			{
				Projectile.timeLeft = (int)ActualMaxTime;
				direction = Vector2.Normalize(Projectile.velocity);
				lockedDirection = direction.X >= 0f ? 1 : -1;
				initialized = true;
				Projectile.netUpdate = true;

				// Seed rotation/prevRotation to the swing's actual starting angle so the first frame's
				// sector-sweep test doesn't see a false jump from the default 0f rotation.
				var (startAngle, _) = GetSwingAngleRange(direction.ToRotation());
				Projectile.rotation = startAngle;
				prevRotation        = startAngle;

				if (Main.myPlayer == Projectile.owner)
				{
					bool heavy = Main.rand.NextBool();
					SoundStyle swingSound = heavy
						? new SoundStyle("KyoMod/Sounds/SwordSwingHeavy") { PitchVariance = 0.2f, Volume = 0.4f }
						: new SoundStyle("KyoMod/Sounds/SwordSwingLight") { PitchVariance = 0.2f };
					SoundEngine.PlaySound(swingSound, Projectile.Center);
				}
			}

			Projectile.Center = Owner.Center + direction * HoldDistance;
			Projectile.velocity = Vector2.Zero;

			float baseAngle = direction.ToRotation();
			float eased = ComboState == 2 ? SmoothStep(SwingRatio) : EaseOut(SwingRatio);

			if (!slashArcSpawned && ShouldSpawnSlashArcNow())
			{
				slashArcSpawned = true;
				if (Main.netMode != NetmodeID.Server)
				{
					var (from, to) = GetSwingAngleRange(baseAngle);
					// Cap at a full circle — some overrides (e.g. Havoc's spin) intentionally overshoot
					// 360° for hit-detection overlap, but the cosmetic arc shouldn't visibly wrap twice.
					float sweep = MathHelper.Clamp(to - from, -MathHelper.TwoPi, MathHelper.TwoPi);
					OnSpawnSlashArc(from, sweep);
				}
			}

			prevRotation = Projectile.rotation;
			UpdateRotation(baseAngle, eased);

			Projectile.scale = TierScale * TierArc * (1.2f + (float)Math.Sin(SwingRatio * MathHelper.Pi) * 0.4f);

			Owner.heldProj = Projectile.whoAmI;
			Owner.ChangeDir(lockedDirection);
			Owner.itemRotation = Projectile.rotation;
			if (lockedDirection != 1)
				Owner.itemRotation -= MathHelper.Pi;
			Owner.itemRotation = MathHelper.WrapAngle(Owner.itemRotation);
		}

		// Static angle boundaries a swing sweeps between, for spawning the cosmetic slash-arc effect.
		// Kept separate from UpdateRotation since some overrides (e.g. Havoc) derive rotation from
		// combo-specific progress rather than the eased parameter directly.
		protected virtual (float from, float to) GetSwingAngleRange(float baseAngle)
		{
			float half = BaseSwingWidth / 2f;
			return ComboState switch
			{
				1 => (baseAngle + half * lockedDirection, baseAngle - half * lockedDirection),
				2 => (baseAngle - MathHelper.PiOver2 * lockedDirection, baseAngle - MathHelper.PiOver2 * lockedDirection + MathHelper.TwoPi * lockedDirection),
				_ => (baseAngle - half * lockedDirection, baseAngle + half * lockedDirection),
			};
		}

		protected virtual void OnSpawnSlashArc(float startAngle, float sweep) { }

		// By default the swing starts moving immediately. Overrides with a hold/wind-up phase before
		// the blade actually moves (e.g. Havoc's quick-draw) should delay the spawn to match, so the
		// cosmetic slash doesn't play out and fade before the real swing motion even begins.
		protected virtual bool ShouldSpawnSlashArcNow() => true;

		protected virtual void UpdateRotation(float baseAngle, float eased)
		{
			float swingWidth = BaseSwingWidth;
			switch (ComboState)
			{
				case 0:
					Projectile.rotation = MathHelper.Lerp(
						baseAngle - swingWidth / 2f * lockedDirection,
						baseAngle + swingWidth / 2f * lockedDirection,
						eased);
					break;

				case 1:
					Projectile.rotation = MathHelper.Lerp(
						baseAngle + swingWidth / 2f * lockedDirection,
						baseAngle - swingWidth / 2f * lockedDirection,
						eased);
					break;

				case 2:
					float spinStart = baseAngle - MathHelper.PiOver2 * lockedDirection;
					Projectile.rotation = spinStart + MathHelper.TwoPi * eased * lockedDirection;
					break;
			}
		}

		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.Write(initialized);
			writer.WriteVector2(direction);
			writer.Write(lockedDirection);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			initialized = reader.ReadBoolean();
			direction = reader.ReadVector2();
			lockedDirection = reader.ReadInt32();
		}
	}
}
