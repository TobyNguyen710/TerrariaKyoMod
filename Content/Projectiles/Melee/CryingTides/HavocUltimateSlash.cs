using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
    // Purely cosmetic straight-line counterpart to SlashArcEffect — same ink shader (SlashInk.fx)
    // and grow/dissolve lifecycle, but a linear ribbon along the Havoc ultimate's dash path instead
    // of a polar crescent. Spawned by HavocUltimateDash, which owns the actual hit logic.
    // ai[0] = direction.X, ai[1] = direction.Y (unit vector), ai[2] = line length in pixels.
    // Projectile.Center is the line's MIDPOINT (for screen culling); start/end are derived from it.
    public class HavocUltimateSlash : ModProjectile
    {
        public override string Texture => "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheNoStack";

        // Same three-phase shape as SlashArcEffect (see SlashInk.fx) — kept independently tunable
        // since this is a distinct, bigger "ultimate" moment rather than a regular swing.
        private const int GrowFrames     = 6;
        private const int ErodeFrames    = 20;
        private const int HardFadeFrames = 8;
        private const int Lifetime       = GrowFrames + ErodeFrames + HardFadeFrames;

        private const float GrowEdgeBand = 0.05f; // must match growBand in SlashInk.fx

        private const int   Segments = 128;
        private const float BumpP = 2f;
        private const float BumpQ = 2f;
        private const float MinRenderThickness = 2f;

        private const float MaxThickness = 60f; // belly thickness — a big single ultimate cut
        private const float CoreWidth    = 22f;

        private static readonly Color CoreColor = new(255, 235, 220);
        private static readonly Color BodyColor = new(180, 20, 20);

        private Vector2 Direction => new(Projectile.ai[0], Projectile.ai[1]);
        private float   Length    => Projectile.ai[2];

        private float InkSeed => MathF.Abs((MathF.Sin(Direction.X * 12.9898f + Direction.Y * 78.233f + Length * 0.017f) * 43758.5453f) % 1f);

        private static readonly float BumpPeakT   = BumpP / (BumpP + BumpQ);
        private static readonly float BumpPeakVal = MathF.Pow(BumpPeakT, BumpP) * MathF.Pow(1f - BumpPeakT, BumpQ);

        public override void SetDefaults()
        {
            Projectile.width       = Projectile.height = 1;
            Projectile.timeLeft    = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly    = false;
            Projectile.hostile     = false;
            Projectile.penetrate   = -1;
        }

        public override bool? CanHitNPC(NPC target) => false;
        public override bool CanHitPlayer(Player target) => false;

        // Thin at both ends, swelling smoothly at the middle — same symmetric bump curve as the
        // crescent arc's ThicknessAt.
        private static float ThicknessAt(float t)
        {
            float raw        = MathF.Pow(t, BumpP) * MathF.Pow(1f - t, BumpQ);
            float normalized = BumpPeakVal > 0f ? raw / BumpPeakVal : 0f;
            return MaxThickness * normalized;
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

            Vector2 dir   = Direction;
            Vector2 start = Projectile.Center - dir * (Length * 0.5f);

            Matrix viewProj = Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1f, 1f);

            Effect inkEffect = KyoMod.SlashInkShader?.Value;
            if (inkEffect == null)
                return false;

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
            DrawRibbon(device, inkEffect, start, dir, ThicknessAt, BodyColor * 0.85f, BodyColor * 0f);

            device.BlendState = BlendState.Additive;
            inkEffect.Parameters["uForceHot"]?.SetValue(1f);
            DrawRibbon(device, inkEffect, start, dir, CoreWidthAt, CoreColor * 0.9f, CoreColor * 0f);

            device.BlendState       = BlendState.AlphaBlend;
            device.RasterizerState  = savedRasterizer;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        // Straight-line counterpart to SlashArcEffect.DrawRibbon: each sample walks along the line
        // (start + dir*Length*t) instead of sweeping an angle, offsetting perpendicular to dir by
        // the taper width. v=0 on one side, v=1 on the other — matches SlashInk.fx's UV convention
        // (the "trailing edge" bleed/brush just lands on one side of the line, no directional
        // meaning intended here since a straight cut has no inner/outer).
        private void DrawRibbon(GraphicsDevice device, Effect effect, Vector2 start, Vector2 dir,
            Func<float, float> widthAt, Color sideAColor, Color sideBColor)
        {
            Vector2 perp = new(-dir.Y, dir.X);
            Vector2 screenStart = start - Main.screenPosition;

            int vertCount = (Segments + 1) * 2;
            var verts = new VertexPositionColorTexture[vertCount];

            for (int i = 0; i <= Segments; i++)
            {
                float t         = i / (float)Segments;
                float thickness = widthAt(t) + MinRenderThickness;
                float half      = thickness * 0.5f;

                Vector2 center = screenStart + dir * (Length * t);
                Vector2 posA   = center + perp * half;
                Vector2 posB   = center - perp * half;

                verts[i * 2]     = new VertexPositionColorTexture(new Vector3(posA, 0f), sideAColor, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(posB, 0f), sideBColor, new Vector2(t, 1f));
            }

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, vertCount - 2);
            }
        }
    }
}
