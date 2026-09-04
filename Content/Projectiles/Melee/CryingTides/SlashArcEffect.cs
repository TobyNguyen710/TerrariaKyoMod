using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
    // Purely cosmetic: a filled crescent-shaped swoosh drawn along a swing's arc as an actual
    // triangle-strip ribbon mesh (GPU-interpolated, no visible seams), independent of the sword
    // sprite's own size.
    // ai[0] = start angle, ai[1] = signed sweep (end - start), ai[2] = arc radius in pixels.
    public abstract class SlashArcEffect : ModProjectile
    {
        public override string Texture => "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheNoStack";

        private const int GrowFrames = 5;
        private const int HoldFrames = 3;
        private const int FadeFrames = 9;
        private const int Lifetime   = GrowFrames + HoldFrames + FadeFrames;

        private const int   Segments = 128;
        // Equal exponents split the arc perfectly 50/50 — a symmetric bump peaking exactly in the
        // middle, zero slope at both tips (both exponents > 1 avoids a kink at either end).
        // One formula for the whole arc — no stitched pieces.
        private const float BumpP = 2f;
        private const float BumpQ = 2f;

        // Added (never clamped) to the *rendered* geometry width only, right where vertices are built
        // — never inside ThicknessAt() itself, which stays a mathematically pure curve reaching zero.
        // A hard Math.Max floor would create a slope discontinuity exactly where the natural curve
        // crosses it (flat below, curved above) — a real visible crease. Adding a constant everywhere
        // keeps the curve perfectly smooth while guaranteeing the strip's width never hits literal
        // zero, which is what produces near-degenerate triangles at the tips — a known source of GPU
        // rasterization artifacts regardless of how smooth the underlying math is. Onikiri's own arc
        // renderers do the same: their width formula can reach zero, but the geometry they emit never
        // does.
        private const float MinRenderThickness = 2f;

        protected abstract Color CoreColor { get; }
        protected abstract Color BodyColor { get; }
        protected virtual  float MaxThickness => 55f;  // belly thickness for normal (tapered) swings
        protected virtual  float SpinThickness => 50f; // uniform thickness for a full-spin swing
        protected virtual  float CoreWidth     => 14f; // roughly constant highlight width — decoupled
                                                        // from the body's taper, so raising MaxThickness
                                                        // doesn't also balloon the bright rim into a
                                                        // swollen blob at the belly

        private float StartAngle => Projectile.ai[0];
        private float Sweep      => Projectile.ai[1];
        private float Radius     => Projectile.ai[2];

        private static BasicEffect _effect;

        private static BasicEffect GetEffect(GraphicsDevice device)
        {
            if (_effect == null || _effect.IsDisposed)
            {
                _effect = new BasicEffect(device)
                {
                    VertexColorEnabled = true,
                    TextureEnabled     = false,
                    World              = Matrix.Identity,
                };
            }
            return _effect;
        }

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

        // A sweep this wide is effectively a full spin, not a directional slash — tapering it would
        // falsely imply a "thick side" that doesn't correspond to anything about a 360° swing.
        private static readonly float FullSpinThreshold = MathHelper.Pi * 300f / 180f;
        private bool IsFullSpin => Math.Abs(Sweep) >= FullSpinThreshold;

        // Zero at both ends, peaking at BumpP/(BumpP+BumpQ) — one smooth curve for the whole arc,
        // not two separate ramps stitched at a point. Normalized so the peak equals 1.
        private static readonly float BumpPeakT   = BumpP / (BumpP + BumpQ);
        private static readonly float BumpPeakVal = MathF.Pow(BumpPeakT, BumpP) * MathF.Pow(1f - BumpPeakT, BumpQ);

        // Thin at both tips, swelling smoothly to its widest around 75% of the way through the arc —
        // a single continuous crescent-moon curve.
        // Full spins skip this entirely and use their own fixed uniform thickness.
        private float ThicknessAt(float t)
        {
            if (IsFullSpin)
                return SpinThickness;

            float raw        = MathF.Pow(t, BumpP) * MathF.Pow(1f - t, BumpQ);
            float normalized = BumpPeakVal > 0f ? raw / BumpPeakVal : 0f;

            return MaxThickness * normalized;
        }

        // Independent of ThicknessAt: stays a roughly constant thin streak along the whole arc,
        // only tapering to nothing right at the very tips (where the body also vanishes) instead of
        // swelling in the belly the way the body does.
        private const float CoreEdgeFade = 0.08f;

        private float CoreWidthAt(float t)
        {
            if (IsFullSpin)
                return CoreWidth;

            float edge = MathHelper.Clamp(Math.Min(t, 1f - t) / CoreEdgeFade, 0f, 1f);
            edge = edge * edge * (3f - 2f * edge);
            return CoreWidth * edge;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int   age    = Lifetime - Projectile.timeLeft;
            float growT  = MathHelper.Clamp(age / (float)GrowFrames, 0f, 1f);
            float reveal = 1f - (1f - growT) * (1f - growT); // ease-out

            float fadeStart = GrowFrames + HoldFrames;
            float fadeT     = age <= fadeStart ? 0f : MathHelper.Clamp((age - fadeStart) / (float)FadeFrames, 0f, 1f);
            float globalAlpha = 1f - fadeT * fadeT;
            if (globalAlpha <= 0f)
                return false;

            Vector2 center           = Projectile.Center - Main.screenPosition;
            int     visibleSegments  = Math.Max(1, (int)(Segments * reveal));

            GraphicsDevice device = Main.spriteBatch.GraphicsDevice;
            BasicEffect    effect = GetEffect(device);
            effect.View       = Main.GameViewMatrix.TransformationMatrix;
            effect.Projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1f, 1f);

            Main.spriteBatch.End();

            var savedRasterizer = device.RasterizerState;
            device.RasterizerState   = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            // Outer body ribbon: wide, dim, alpha-blended — the bulk of the crescent's fill. Its outer
            // edge is capped at Radius (the actual hit boundary) and it builds inward from there, so
            // nothing visible ever reaches further out than what can actually be hit.
            device.BlendState = BlendState.AlphaBlend;
            DrawRibbon(device, effect, center, visibleSegments,
                widthAt: ThicknessAt, outerInset: 0f,
                outerColor: BodyColor * (globalAlpha * 0.85f), innerColor: BodyColor * (globalAlpha * 0.15f));

            // Inner core ribbon: narrow, bright, additive, hugging the same outer boundary — the crisp
            // highlight edge tracing the top of the crescent, without extending past the hit range.
            device.BlendState = BlendState.Additive;
            DrawRibbon(device, effect, center, visibleSegments,
                widthAt: CoreWidthAt, outerInset: 0f,
                outerColor: CoreColor * (globalAlpha * 0.9f), innerColor: CoreColor * (globalAlpha * 0.05f));

            device.BlendState       = BlendState.AlphaBlend;
            device.RasterizerState  = savedRasterizer;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        // Builds a ribbon strip following the arc: each sample contributes an outer and inner vertex,
        // so the GPU interpolates color/width smoothly with no visible seams. The outer vertex never
        // sits further from center than (Radius - outerInset), keeping the visual within hit range.
        private void DrawRibbon(GraphicsDevice device, BasicEffect effect, Vector2 center, int visibleSegments,
            Func<float, float> widthAt, float outerInset, Color outerColor, Color innerColor)
        {
            int vertCount = (visibleSegments + 1) * 2;
            var verts = new VertexPositionColor[vertCount];

            for (int i = 0; i <= visibleSegments; i++)
            {
                float t         = i / (float)Segments;
                float thickness = widthAt(t) + MinRenderThickness;

                Vector2 normal   = (StartAngle + Sweep * t).ToRotationVector2();
                float   outerLen = Radius - outerInset;
                float   innerLen = outerLen - thickness;

                Vector2 outerPos = center + normal * outerLen;
                Vector2 innerPos = center + normal * innerLen;

                verts[i * 2]     = new VertexPositionColor(new Vector3(outerPos, 0f), outerColor);
                verts[i * 2 + 1] = new VertexPositionColor(new Vector3(innerPos, 0f), innerColor);
            }

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, vertCount - 2);
            }
        }
    }
}
