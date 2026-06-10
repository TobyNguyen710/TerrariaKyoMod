using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles
{
	public class CryingTidesSwungBlade : ModProjectile
	{
		public override string Texture => "KyoMod/Content/Items/CryingTides";

		private bool initialized = false;
		private Vector2 direction = Vector2.Zero;

		public int ComboState => (int)Projectile.ai[0];
		public Player Owner => Main.player[Projectile.owner];

		private const float NormalMaxTime = 35f;
		private const float SpinMaxTime = 50f;
		private float ActualMaxTime => ComboState == 2 ? SpinMaxTime : NormalMaxTime;

		private const float SwingWidth = MathHelper.PiOver2 * 1.5f; // 135 degree arc
		private const float HoldDistance = 35f;

		private float Timer => ActualMaxTime - Projectile.timeLeft;
		private float SwingRatio => MathHelper.Clamp(Timer / ActualMaxTime, 0f, 1f);

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 10;
			ProjectileID.Sets.TrailingMode[Type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.DamageType = DamageClass.Melee;
			Projectile.width = Projectile.height = 60;
			Projectile.tileCollide = false;
			Projectile.friendly = true;
			Projectile.penetrate = -1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = (int)NormalMaxTime;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			float collisionPoint = 0f;
			float bladeLength = 65f * Projectile.scale;
			Vector2 holdPoint = direction * HoldDistance;

			return Collision.CheckAABBvLineCollision(
				targetHitbox.TopLeft(), targetHitbox.Size(),
				Owner.Center + holdPoint,
				Owner.Center + holdPoint + Projectile.rotation.ToRotationVector2() * bladeLength,
				18f, ref collisionPoint);
		}

		// Cubic ease-in-out for a weighty swing feel
		private static float EaseInOut(float t)
		{
			if (t < 0.5f)
				return 4f * t * t * t;
			return 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
		}

		public override void AI()
		{
			if (!initialized)
			{
				Projectile.timeLeft = (int)ActualMaxTime;
				direction = Vector2.Normalize(Projectile.velocity);
				initialized = true;
				Projectile.netUpdate = true;
			}

			Projectile.Center = Owner.Center + direction * HoldDistance;
			Projectile.velocity = Vector2.Zero;

			float baseAngle = direction.ToRotation();
			float eased = EaseInOut(SwingRatio);

			switch (ComboState)
			{
				case 0: // Downward slash: sweeps from above to below
					Projectile.rotation = MathHelper.Lerp(
						baseAngle - SwingWidth / 2f * Owner.direction,
						baseAngle + SwingWidth / 2f * Owner.direction,
						eased);
					break;

				case 1: // Upward slash: reverse of downward
					Projectile.rotation = MathHelper.Lerp(
						baseAngle + SwingWidth / 2f * Owner.direction,
						baseAngle - SwingWidth / 2f * Owner.direction,
						eased);
					break;

				case 2: // Spin: full clockwise 360°
					Projectile.rotation = baseAngle + MathHelper.TwoPi * eased;
					break;
			}

			// Sword grows and shrinks over the arc
			Projectile.scale = 1.2f + (float)Math.Sin(SwingRatio * MathHelper.Pi) * 0.4f;

			Owner.heldProj = Projectile.whoAmI;
			Owner.ChangeDir(Math.Sign(direction.X));
			Owner.itemRotation = Projectile.rotation;
			if (Owner.direction != 1)
				Owner.itemRotation -= MathHelper.Pi;
			Owner.itemRotation = MathHelper.WrapAngle(Owner.itemRotation);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D sword = ModContent.Request<Texture2D>("KyoMod/Content/Items/CryingTides").Value;

			SpriteEffects flip = Owner.direction < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			float extraAngle = Owner.direction < 0 ? MathHelper.PiOver2 : 0f;

			float drawRotation = Projectile.rotation + MathHelper.PiOver4 + extraAngle;
			Vector2 drawOrigin = new Vector2(Owner.direction < 0 ? sword.Width : 0f, sword.Height);
			Vector2 drawOffset = Owner.Center + direction * HoldDistance * 0.5f - Main.screenPosition;

			for (int i = 0; i < Projectile.oldRot.Length; i++)
			{
				float alpha = (1f - i / (float)Projectile.oldRot.Length) * 0.12f;
				float afterRot = Projectile.oldRot[i] + MathHelper.PiOver4 + extraAngle;
				Main.EntitySpriteDraw(sword, drawOffset, null, lightColor * alpha, afterRot, drawOrigin, Projectile.scale, flip, 0);
			}

			Main.EntitySpriteDraw(sword, drawOffset, null, lightColor, drawRotation, drawOrigin, Projectile.scale, flip, 0);
			return false;
		}

		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.Write(initialized);
			writer.WriteVector2(direction);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			initialized = reader.ReadBoolean();
			direction = reader.ReadVector2();
		}
	}
}
