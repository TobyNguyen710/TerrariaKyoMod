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

        private const int   Segments     = 128;
        private const float ThickenEnd   = 0.62f; // arc-length fraction where thickness peaks
        private const float TaperStart   = 0.74f; // fraction where the sharp tip taper begins
        private const float MinThickness = 5f;

        protected abstract Color CoreColor { get; }
        protected abstract Color BodyColor { get; }
        protected virtual  float MaxThickness => 46f;

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

        // Reference sweep matching a normal (non-spin) swing's arc width — sweeps at or below this
        // keep today's base thickness; wider sweeps (e.g. a full spin) scale thicker, up to the cap.
        private static readonly float ReferenceSweepRadians = MathHelper.Pi * 200f / 180f;
        private const float MaxSweepThicknessScale = 2.4f;

        private float SweepThicknessScale
        {
            get
            {
                float factor = Math.Abs(Sweep) / ReferenceSweepRadians;
                return MathHelper.Clamp(factor, 1f, MaxSweepThicknessScale);
            }
        }

        // A sweep this wide is effectively a full spin, not a directional slash — tapering it would
        // falsely imply a "thick side" that doesn't correspond to anything about a 360° swing.
        private static readonly float FullSpinThreshold = MathHelper.Pi * 300f / 180f;
        private bool IsFullSpin => Math.Abs(Sweep) >= FullSpinThreshold;

        // Thin at the start, swells through the belly, then tapers to a sharp point at the tip —
        // not a symmetric bell curve, matching a crescent-slash silhouette rather than a rounded tube.
        // Full spins skip the taper entirely and use uniform thickness all the way around.
        private float ThicknessAt(float t)
        {
            if (IsFullSpin)
                return MaxThickness * SweepThicknessScale;

            float grow = MathHelper.Clamp(t / ThickenEnd, 0f, 1f);
            grow = grow * grow * (3f - 2f * grow);

            float shrink = t <= TaperStart ? 0f : MathHelper.Clamp((t - TaperStart) / (1f - TaperStart), 0f, 1f);
            shrink = shrink * shrink * (3f - 2f * shrink);

            float thickness = MathHelper.Lerp(MinThickness, MaxThickness, grow) * (1f - shrink);
            return thickness * SweepThicknessScale;
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
                widthMul: 1.0f, outerInset: 0f,
                outerColor: BodyColor * (globalAlpha * 0.85f), innerColor: BodyColor * (globalAlpha * 0.15f));

            // Inner core ribbon: narrow, bright, additive, hugging the same outer boundary — the crisp
            // highlight edge tracing the top of the crescent, without extending past the hit range.
            device.BlendState = BlendState.Additive;
            DrawRibbon(device, effect, center, visibleSegments,
                widthMul: 0.32f, outerInset: 0f,
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
            float widthMul, float outerInset, Color outerColor, Color innerColor)
        {
            int vertCount = (visibleSegments + 1) * 2;
            var verts = new VertexPositionColor[vertCount];

            for (int i = 0; i <= visibleSegments; i++)
            {
                float t         = i / (float)Segments;
                float thickness = ThicknessAt(t) * widthMul;
                if (thickness < 0.5f)
                    thickness = 0f;

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
