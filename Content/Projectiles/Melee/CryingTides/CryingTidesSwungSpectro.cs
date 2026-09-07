using KyoMod.Common.Players;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
	public class CryingTidesSwungSpectro : CryingTidesSwungBase
	{
		public override string Texture => "KyoMod/Content/Items/Melee/CryingTides/CryingTides";

		protected override float BladeLength => 70f;

		protected override void OnSpawnSlashArc(float startAngle, float sweep)
		{
			Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Center + direction * HoldDistance, Vector2.Zero,
				ModContent.ProjectileType<SpectroSlashArc>(), 0, 0f, Projectile.owner, startAngle, sweep, SlashRadius);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			Owner.GetModPlayer<CryingTidesPlayer>().AddLiberationEnergy();

			if (Main.netMode == NetmodeID.Server) return;

			SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/CryingTides/SwordImpactLight") { PitchVariance = 0.25f }, target.Center);

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
			Vector2 drawOrigin = new Vector2(lockedDirection < 0 ? sword.Width : 0f, sword.Height);
			Vector2 drawOffset = Owner.Center + direction * HoldDistance * 0.5f - Main.screenPosition;

			float baseAngle = direction.ToRotation();
			float swingStart = ComboState switch
			{
				1 => baseAngle + BaseSwingWidth / 2f * lockedDirection,
				2 => Projectile.rotation - MathHelper.Pi * lockedDirection,
				_ => baseAngle - BaseSwingWidth / 2f * lockedDirection,
			};

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			const float closeThreshold = 0.55f;
			float fanTail = SwingRatio < closeThreshold
				? swingStart
				: MathHelper.Lerp(swingStart, Projectile.rotation, (SwingRatio - closeThreshold) / (1f - closeThreshold));

			int fanCount = 5 + (int)((TierArc - 1f) * 15f);
			for (int i = 0; i < fanCount; i++)
			{
				float t = i / (float)fanCount;
				float ghostRot = MathHelper.Lerp(fanTail, Projectile.rotation, t);
				float ghostDrawRot = ghostRot + MathHelper.PiOver4 + extraAngle;
				float alpha = t * 0.20f * TierArc;
				float ghostScale = Projectile.scale * (0.75f + t * 0.25f);
				Main.EntitySpriteDraw(sword, drawOffset, null, lightColor * alpha, ghostDrawRot, drawOrigin, ghostScale, flip, 0);
			}

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			float drawRotation = Projectile.rotation + MathHelper.PiOver4 + extraAngle;
			Main.EntitySpriteDraw(sword, drawOffset, null, lightColor, drawRotation, drawOrigin, Projectile.scale, flip, 0);
			return false;
		}
	}
}
