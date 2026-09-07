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
	// PostMech ultimate (Spectro): a much bigger version of SpectroBeam's traveling crescent shape.
	// Now actually drawn as a traveling instance of the slash arc effect (same SlashInk.fx shader
	// and grow-wipe/dissolve lifecycle as SlashArcEffect/HavocUltimateSlash) instead of dust alone —
	// the crescent you see is the crescent you hit, redrawn fresh each frame at the beam's current
	// position, plus the existing GoldFlame particle system layered on top.
	public class SpectroUltimateBeam : ModProjectile
	{
		public override string Texture => "KyoMod/Content/Projectiles/Melee/TestSwordArrow";

		public const  float TravelSpeed = 52f; // scale the spawn direction up to this on frame 0

		private const int   Lifetime    = 70;
		private const float ArcRadius   = 260f; // far bigger than SpectroBeam's 38f
		private const float ArcHalf     = MathHelper.Pi * 60f / 180f; // 60° each side = 120° total
		private const float Thickness   = 150f; // radial band width the crescent occupies
		private const int   FreezeDuration = 420; // 7 seconds

		// Arc-effect lifecycle (see SlashInk.fx / SlashArcEffect) scaled to this beam's own
		// 70-frame lifetime instead of a swing's ~24-27 frames: quick wipe-in at launch, most of the
		// flight solid, dissolving out over the last stretch before it naturally expires.
		private const int   GrowFrames     = 8;
		private const int   ErodeFrames    = 47;
		private const int   HardFadeFrames = 15;
		private const float GrowEdgeBand   = 0.05f; // must match growBand in SlashInk.fx

		private const int   Segments = 128;
		private const float BumpP = 2f;
		private const float BumpQ = 2f;
		private const float MinRenderThickness = 2f;
		private const float CoreWidth = 20f;

		private static readonly Color CoreColor = new(255, 250, 205);
		private static readonly Color BodyColor = new(230, 140, 20);

		private static readonly float BumpPeakT   = BumpP / (BumpP + BumpQ);
		private static readonly float BumpPeakVal = MathF.Pow(BumpPeakT, BumpP) * MathF.Pow(1f - BumpPeakT, BumpQ);

		private Vector2 TravelDir => Projectile.velocity.SafeNormalize(Vector2.UnitX);

		private float InkSeed => MathF.Abs((MathF.Sin(Projectile.whoAmI * 12.9898f) * 43758.5453f) % 1f);

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

		// Thin at both angular tips, thickest at the middle — same symmetric bump curve as
		// SlashArcEffect.ThicknessAt, capped at the crescent's own hit-band width.
		private static float ThicknessAt(float t)
		{
			float raw        = MathF.Pow(t, BumpP) * MathF.Pow(1f - t, BumpQ);
			float normalized = BumpPeakVal > 0f ? raw / BumpPeakVal : 0f;
			return Thickness * normalized;
		}

		private const float CoreEdgeFade = 0.08f;

		private static float CoreWidthAt(float t)
		{
			float edge = MathHelper.Clamp(Math.Min(t, 1f - t) / CoreEdgeFade, 0f, 1f);
			edge = edge * edge * (3f - 2f * edge);
			return CoreWidth * edge;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			int age = Lifetime - Projectile.timeLeft;

			float growProgress = MathHelper.Clamp(age / (float)GrowFrames, 0f, 1f);
			float growEdge     = MathHelper.Lerp(-GrowEdgeBand, 1f + GrowEdgeBand, growProgress);

			float erodeAge = age - GrowFrames;
			float erodeT   = MathHelper.Clamp(erodeAge / (float)(ErodeFrames + HardFadeFrames), 0f, 1f);
			erodeT *= erodeT;

			float hardFadeAge = age - (GrowFrames + ErodeFrames);
			float hardFade    = hardFadeAge <= 0f ? 1f : 1f - MathHelper.Clamp(hardFadeAge / (float)HardFadeFrames, 0f, 1f);
			if (hardFade <= 0f)
				return false;

			Effect inkEffect = KyoMod.SlashInkShader?.Value;
			if (inkEffect == null)
				return false;

			Vector2 dir       = TravelDir;
			Vector2 arcCenter = Projectile.Center - dir * ArcRadius - Main.screenPosition;
			float   travelAngle = dir.ToRotation();
			float   startAngle  = travelAngle - ArcHalf;
			float   sweep       = ArcHalf * 2f;

			Matrix viewProj = Main.GameViewMatrix.TransformationMatrix
				* Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1f, 1f);

			GraphicsDevice device = Main.spriteBatch.GraphicsDevice;
			inkEffect.Parameters["uViewProj"]?.SetValue(viewProj);
			inkEffect.Parameters["uTime"]?.SetValue(age / 60f);
			inkEffect.Parameters["uSeed"]?.SetValue(InkSeed);
			inkEffect.Parameters["uGrowEdge"]?.SetValue(growEdge);
			inkEffect.Parameters["uErodeT"]?.SetValue(erodeT);
			inkEffect.Parameters["uHardFade"]?.SetValue(hardFade);

			Main.spriteBatch.End();

			var savedRasterizer = device.RasterizerState;
			device.RasterizerState   = RasterizerState.CullNone;
			device.DepthStencilState = DepthStencilState.None;

			device.BlendState = BlendState.AlphaBlend;
			inkEffect.Parameters["uForceHot"]?.SetValue(0f);
			DrawRibbon(device, inkEffect, arcCenter, startAngle, sweep, ThicknessAt, BodyColor * 0.85f, BodyColor * 0f);

			device.BlendState = BlendState.Additive;
			inkEffect.Parameters["uForceHot"]?.SetValue(1f);
			DrawRibbon(device, inkEffect, arcCenter, startAngle, sweep, CoreWidthAt, CoreColor * 0.9f, CoreColor * 0f);

			device.BlendState       = BlendState.AlphaBlend;
			device.RasterizerState  = savedRasterizer;

			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			return false;
		}

		// Same polar-ribbon construction as SlashArcEffect.DrawRibbon, but the outer edge sits at
		// ArcRadius + half the hit band's width (not a fixed "Radius") since this crescent is an
		// annulus band, not a swing capped at a single hit radius.
		private static void DrawRibbon(GraphicsDevice device, Effect effect, Vector2 center,
			float startAngle, float sweep, Func<float, float> widthAt, Color outerColor, Color innerColor)
		{
			int vertCount = (Segments + 1) * 2;
			var verts = new VertexPositionColorTexture[vertCount];

			for (int i = 0; i <= Segments; i++)
			{
				float t         = i / (float)Segments;
				float thickness = widthAt(t) + MinRenderThickness;

				Vector2 normal   = (startAngle + sweep * t).ToRotationVector2();
				float   outerLen = ArcRadius + Thickness * 0.5f;
				float   innerLen = outerLen - thickness;

				Vector2 outerPos = center + normal * outerLen;
				Vector2 innerPos = center + normal * innerLen;

				verts[i * 2]     = new VertexPositionColorTexture(new Vector3(outerPos, 0f), outerColor, new Vector2(t, 0f));
				verts[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(innerPos, 0f), innerColor, new Vector2(t, 1f));
			}

			foreach (EffectPass pass in effect.CurrentTechnique.Passes)
			{
				pass.Apply();
				device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, vertCount - 2);
			}
		}

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
