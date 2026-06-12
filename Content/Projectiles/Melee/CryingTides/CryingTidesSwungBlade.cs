using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
	public class CryingTidesSwungBlade : ModProjectile
	{
		public override string Texture => "KyoMod/Content/Items/Melee/CryingTides/CryingTides";

		private bool initialized = false;
		private Vector2 direction = Vector2.Zero;
		private int lockedDirection = 1;

		public int ComboState => (int)Projectile.ai[0];
		private float TierScale => Projectile.ai[1] > 0f ? Projectile.ai[1] : 1f;
		public Player Owner => Main.player[Projectile.owner];

		private const float NormalMaxTime = 25f;
		private const float SpinMaxTime = 45f;
		private float ActualMaxTime => ComboState == 2 ? SpinMaxTime : NormalMaxTime;

		private const float SwingWidth = MathHelper.PiOver2 * 2.2f;
		private const float HoldDistance = 5f;

		private float Timer => ActualMaxTime - Projectile.timeLeft;
		private float SwingRatio => MathHelper.Clamp(Timer / (ActualMaxTime - 1f), 0f, 1f);

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

		// Fast start that decelerates — sword commits immediately and finishes with authority
		private static float EaseOut(float t)
		{
			float inv = 1f - t;
			return 1f - inv * inv * inv;
		}

		// Gentle ease-in-out — shows the full spin arc evenly from start to finish
		private static float SmoothStep(float t) => t * t * (3f - 2f * t);

		public override void AI()
		{
			if (!initialized)
			{
				Projectile.timeLeft = (int)ActualMaxTime;
				direction = Vector2.Normalize(Projectile.velocity);
				lockedDirection = direction.X >= 0f ? 1 : -1;
				initialized = true;
				Projectile.netUpdate = true;
			}

			Projectile.Center = Owner.Center + direction * HoldDistance;
			Projectile.velocity = Vector2.Zero;

			float baseAngle = direction.ToRotation();
			float eased = ComboState == 2 ? SmoothStep(SwingRatio) : EaseOut(SwingRatio);

			switch (ComboState)
			{
				case 0: // Downward slash
					Projectile.rotation = MathHelper.Lerp(
						baseAngle - SwingWidth / 2f * lockedDirection,
						baseAngle + SwingWidth / 2f * lockedDirection,
						eased);
					break;

				case 1: // Upward slash
					Projectile.rotation = MathHelper.Lerp(
						baseAngle + SwingWidth / 2f * lockedDirection,
						baseAngle - SwingWidth / 2f * lockedDirection,
						eased);
					break;

				case 2: // Full 360 spin
					float spinStart = baseAngle - MathHelper.PiOver2 * lockedDirection;
					Projectile.rotation = spinStart + MathHelper.TwoPi * eased * lockedDirection;
					break;
			}

			Projectile.scale = TierScale * (1.2f + (float)Math.Sin(SwingRatio * MathHelper.Pi) * 0.4f);

			Owner.heldProj = Projectile.whoAmI;
			Owner.ChangeDir(lockedDirection);
			Owner.itemRotation = Projectile.rotation;
			if (lockedDirection != 1)
				Owner.itemRotation -= MathHelper.Pi;
			Owner.itemRotation = MathHelper.WrapAngle(Owner.itemRotation);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Main.netMode == NetmodeID.Server) return;

			for (int i = 0; i < 15; i++)
			{
				Dust d = Dust.NewDustDirect(target.Center, 0, 0, DustID.GoldFlame);
				d.velocity = (Projectile.rotation + Main.rand.NextFloat(-0.5f, 0.5f)).ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
				d.scale = Main.rand.NextFloat(0.8f, 1.5f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D sword = ModContent.Request<Texture2D>("KyoMod/Content/Items/Melee/CryingTides/CryingTides").Value;

			SpriteEffects flip = lockedDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			float extraAngle = lockedDirection < 0 ? MathHelper.PiOver2 : 0f;

			float drawRotation = Projectile.rotation + MathHelper.PiOver4 + extraAngle;
			Vector2 drawOrigin = new Vector2(lockedDirection < 0 ? sword.Width : 0f, sword.Height);
			Vector2 drawOffset = Owner.Center + direction * HoldDistance * 0.5f - Main.screenPosition;

			for (int i = 0; i < Projectile.oldRot.Length; i++)
			{
				float alpha = (1f - i / (float)Projectile.oldRot.Length) * 0.18f;
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
