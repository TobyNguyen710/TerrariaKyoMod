using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
	// Cosmetic wind-up for Spectro's ultimate: same hold-then-quick-draw timing as Havoc's first
	// combo swing (CryingTidesSwungHavoc, ComboState 0 — long hold, fast sweep at the very end),
	// but with the plain sword sprite instead of the scythe, and no damage/hit detection. Once the
	// sweep finishes, it fires SpectroUltimateBeam in the direction the swing landed.
	public class SpectroUltimateCast : ModProjectile
	{
		public override string Texture => "KyoMod/Content/Items/Melee/CryingTides/CryingTides";

		private const float NormalTime   = 60f; // matches CryingTidesSwungHavoc's NormalTime
		private const float HoldRatio    = 0.9f; // matches CryingTidesSwungHavoc's HoldRatio
		private const float HalfArc      = MathHelper.Pi * 100f / 180f; // matches Havoc combo 0's HalfArc
		private const float HoldDistance = 5f;

		private bool    initialized     = false;
		private Vector2 direction       = Vector2.Zero;
		private int     lockedDirection = 1;
		private bool    beamFired       = false;

		// TierScale/TierArc, passed in from the actual equipped weapon (CryingTidesPlayer) — draws
		// at the same baseline size (1.2x) the real swing shows at the start/end of its motion,
		// instead of a fixed constant unrelated to the weapon's own scale.
		private float TierScale => Projectile.ai[0] > 0f ? Projectile.ai[0] : 1f;
		private float TierArc   => Projectile.ai[1] > 0f ? Projectile.ai[1] : 1f;

		private Player Owner => Main.player[Projectile.owner];

		private float Timer      => NormalTime - Projectile.timeLeft;
		private float SwingRatio => MathHelper.Clamp(Timer / (NormalTime - 1f), 0f, 1f);
		private float Progress   => SwingRatio <= HoldRatio ? 0f : (SwingRatio - HoldRatio) / (1f - HoldRatio);

		public override void SetDefaults()
		{
			Projectile.width       = Projectile.height = 40;
			Projectile.friendly    = false;
			Projectile.hostile     = false;
			Projectile.tileCollide = false;
			Projectile.penetrate   = -1;
			Projectile.timeLeft    = (int)NormalTime;
			Projectile.DamageType  = DamageClass.Melee;
			Projectile.damage      = 0;
		}

		public override bool? CanHitNPC(NPC target) => false;
		public override bool CanHitPlayer(Player target) => false;

		public override void AI()
		{
			if (!initialized)
			{
				direction       = Vector2.Normalize(Projectile.velocity);
				lockedDirection = direction.X >= 0f ? 1 : -1;
				initialized     = true;

				if (Main.myPlayer == Projectile.owner)
					SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/CryingTIdesSpectroUlt") { PitchVariance = 0.05f, Volume = 0.6f }, Projectile.Center);
			}

			Projectile.Center   = Owner.Center + direction * HoldDistance;
			Projectile.velocity = Vector2.Zero;

			float baseAngle = direction.ToRotation();
			float p         = Progress;

			Projectile.rotation = MathHelper.Lerp(
				baseAngle - HalfArc * lockedDirection,
				baseAngle + HalfArc * lockedDirection,
				p);

			Owner.heldProj      = Projectile.whoAmI;
			Owner.ChangeDir(lockedDirection);
			Owner.itemRotation = Projectile.rotation;
			if (lockedDirection != 1)
				Owner.itemRotation -= MathHelper.Pi;
			Owner.itemRotation = MathHelper.WrapAngle(Owner.itemRotation);

			if (!beamFired && p >= 1f && Main.myPlayer == Projectile.owner)
			{
				beamFired = true;
				// Fires toward the original aim direction, not Projectile.rotation — that's the
				// swing's ENDING pose (offset by HalfArc from the aim), not where the player aimed.
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Center, direction,
					ModContent.ProjectileType<SpectroUltimateBeam>(), 0, 0f, Projectile.owner);
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D sword = ModContent.Request<Texture2D>(Texture).Value;

			SpriteEffects flip       = lockedDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			float         extraAngle = lockedDirection < 0 ? MathHelper.PiOver2 : 0f;
			Vector2       drawOrigin = new Vector2(lockedDirection < 0 ? sword.Width : 0f, sword.Height);
			Vector2       drawOffset = Owner.Center + direction * HoldDistance * 0.5f - Main.screenPosition;
			float         drawRot    = Projectile.rotation + MathHelper.PiOver4 + extraAngle;

			float scale = TierScale * TierArc * 1.2f;
			Main.EntitySpriteDraw(sword, drawOffset, null, lightColor, drawRot, drawOrigin, scale, flip, 0);
			return false;
		}
	}
}
