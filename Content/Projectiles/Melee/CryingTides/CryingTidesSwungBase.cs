using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
	public abstract class CryingTidesSwungBase : ModProjectile
	{
		protected bool initialized = false;
		protected Vector2 direction = Vector2.Zero;
		protected int lockedDirection = 1;

		public int ComboState => (int)Projectile.ai[0];
		protected float TierScale => Projectile.ai[1] > 0f ? Projectile.ai[1] : 1f;
		protected float TierArc => Projectile.ai[2] > 0f ? Projectile.ai[2] : 1f;
		public Player Owner => Main.player[Projectile.owner];

		protected const float BaseSwingWidth = MathHelper.PiOver2 * 2.2f;
		protected const float HoldDistance = 5f;

		protected virtual float MaxTimeNormal => 15f;
		protected virtual float MaxTimeSpin => 30f;
		protected virtual float BladeLength => 85f;

		protected float ActualMaxTime => ComboState == 2 ? MaxTimeSpin : MaxTimeNormal;
		protected float Timer => ActualMaxTime - Projectile.timeLeft;
		protected float SwingRatio => MathHelper.Clamp(Timer / (ActualMaxTime - 1f), 0f, 1f);

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

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			float collisionPoint = 0f;
			float bladeLength = BladeLength * Projectile.scale;
			Vector2 holdPoint = direction * HoldDistance;

			return Collision.CheckAABBvLineCollision(
				targetHitbox.TopLeft(), targetHitbox.Size(),
				Owner.Center + holdPoint,
				Owner.Center + holdPoint + Projectile.rotation.ToRotationVector2() * bladeLength,
				18f, ref collisionPoint);
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

			UpdateRotation(baseAngle, eased);

			Projectile.scale = TierScale * TierArc * (1.2f + (float)Math.Sin(SwingRatio * MathHelper.Pi) * 0.4f);

			Owner.heldProj = Projectile.whoAmI;
			Owner.ChangeDir(lockedDirection);
			Owner.itemRotation = Projectile.rotation;
			if (lockedDirection != 1)
				Owner.itemRotation -= MathHelper.Pi;
			Owner.itemRotation = MathHelper.WrapAngle(Owner.itemRotation);
		}

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
