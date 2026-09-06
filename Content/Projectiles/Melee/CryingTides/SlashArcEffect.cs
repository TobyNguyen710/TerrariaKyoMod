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

        // Lifecycle mirrors Onikiri's OniAnnihilateArc.fx structure: a near-instant directional
        // wipe-in, then a long window where a per-pixel noise dissolve creeps in (barely visible at
        // first, more holes appearing over time — no distinct "nothing changes" hold phase), and a
        // final hard alpha ramp that guarantees the arc is fully gone by end of life regardless of
        // where the dissolve pattern happened to land. Total matches the previously-tuned 24-frame
        // on-screen duration (was Grow5+Hold10+Fade9). Tried making this scale to the swing's actual
        // remaining duration (see git history) — reverted per explicit request in favor of tuning
        // swing speed/damage on the weapon side instead of the visual side.
        private const int GrowFrames     = 8;
        private const int ErodeFrames    = 14;
        private const int HardFadeFrames = 5;
        private const int Lifetime       = GrowFrames + ErodeFrames + HardFadeFrames;

        // Sweeps uGrowEdge from -band to 1+band so the wipe fully clears both tips by the end of
        // the grow phase — must match growBand in SlashInk.fx.
        private const float GrowEdgeBand = 0.05f;

        private const int   Segments = 256;
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
        protected virtual  float MaxThickness => 70f;  // belly thickness for normal (tapered) swings
        protected virtual  float SpinThickness => 64f; // uniform thickness for a full-spin swing
        protected virtual  float CoreWidth     => 14f; // roughly constant highlight width — decoupled
                                                        // from the body's taper, so raising MaxThickness
                                                        // doesn't also balloon the bright rim into a
                                                        // swollen blob at the belly

        private float StartAngle => Projectile.ai[0];
        private float Sweep      => Projectile.ai[1];
        private float Radius     => Projectile.ai[2];

        // Deterministic per-instance seed derived from the swing's own angle/sweep (no spare ai
        // slot to store a random value in) — just needs to differ swing-to-swing, not be truly
        // random, so the ink pattern doesn't look identical every time.
        private float InkSeed => MathF.Abs((MathF.Sin(StartAngle * 12.9898f + Sweep * 78.233f) * 43758.5453f) % 1f);

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

        // Full spins stay at a fixed uniform thickness through the middle, but taper down to meet the
        // outer line at the very start/end of the sweep (the seam where the near-360° gap sits),
        // instead of ending on a blunt flat-cut edge.
        private const float SpinEdgeFade = 0.15f;

        // Thin at both tips, swelling smoothly to its widest around 75% of the way through the arc —
        // a single continuous crescent-moon curve.
        private float ThicknessAt(float t)
        {
            if (IsFullSpin)
            {
                float edge = MathHelper.Clamp(Math.Min(t, 1f - t) / SpinEdgeFade, 0f, 1f);
                edge = edge * edge * (3f - 2f * edge);
                return SpinThickness * edge;
            }

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
            int age = Lifetime - Projectile.timeLeft;

            // Grow-in: a hard-edged directional wipe, computed entirely in the shader from where
            // this edge value currently sits — not a mirror of the fade-out.
            float growProgress = MathHelper.Clamp(age / (float)GrowFrames, 0f, 1f);
            float growEdge     = MathHelper.Lerp(-GrowEdgeBand, 1f + GrowEdgeBand, growProgress);

            // Erosion (per-pixel noise dissolve, see SlashInk.fx) starts creeping the moment grow-in
            // ends and keeps ramping all the way through the hard-fade window too — eased so it's
            // barely perceptible right after grow-in (reads as a hold) and accelerates toward the end.
            float erodeAge = age - GrowFrames;
            float erodeT   = MathHelper.Clamp(erodeAge / (float)(ErodeFrames + HardFadeFrames), 0f, 1f);
            erodeT *= erodeT;

            // Final hard backstop: guarantees the arc is fully invisible by end of life regardless of
            // where the noise dissolve happened to leave stray surviving pixels.
            float hardFadeAge = age - (GrowFrames + ErodeFrames);
            float hardFade    = hardFadeAge <= 0f ? 1f : 1f - MathHelper.Clamp(hardFadeAge / (float)HardFadeFrames, 0f, 1f);
            if (hardFade <= 0f)
                return false;

            Vector2 center   = Projectile.Center - Main.screenPosition;
            Matrix  viewProj = Main.GameViewMatrix.TransformationMatrix
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

            // Outer body ribbon: wide, dim, alpha-blended — the bulk of the crescent's fill. Its outer
            // edge is capped at Radius (the actual hit boundary) and it builds inward from there, so
            // nothing visible ever reaches further out than what can actually be hit. Full ink pass:
            // banding/brush/dissolve all active.
            device.BlendState = BlendState.AlphaBlend;
            inkEffect.Parameters["uForceHot"]?.SetValue(0f);
            DrawRibbon(device, inkEffect, center,
                widthAt: ThicknessAt, outerInset: 0f,
                outerColor: BodyColor * 0.85f, innerColor: BodyColor * 0f);

            // Inner core ribbon: narrow, bright, additive, hugging the same outer boundary — the
            // "still sharp" highlight that survives the body's dissolve intact (Onikiri draws the
            // same mesh twice this way: one full ink+dissolve pass, one thin always-hot pass).
            device.BlendState = BlendState.Additive;
            inkEffect.Parameters["uForceHot"]?.SetValue(1f);
            DrawRibbon(device, inkEffect, center,
                widthAt: CoreWidthAt, outerInset: 0f,
                outerColor: CoreColor * 0.9f, innerColor: CoreColor * 0f);

            device.BlendState       = BlendState.AlphaBlend;
            device.RasterizerState  = savedRasterizer;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        // Builds a ribbon strip following the whole arc: each sample contributes an outer and inner
        // vertex, so the GPU interpolates color/width smoothly with no visible seams. The outer vertex
        // never sits further from center than (Radius - outerInset), keeping the visual within hit
        // range. The full mesh is always built — grow-in reveal, dissolve, and the hard-fade backstop
        // are all handled per-pixel by the shader now (see SlashInk.fx), not by cropping vertices or
        // multiplying a CPU-side erosion factor here.
        private void DrawRibbon(GraphicsDevice device, Effect effect, Vector2 center,
            Func<float, float> widthAt, float outerInset, Color outerColor, Color innerColor)
        {
            int vertCount = (Segments + 1) * 2;
            var verts = new VertexPositionColorTexture[vertCount];

            for (int i = 0; i <= Segments; i++)
            {
                float t         = i / (float)Segments;
                float thickness = widthAt(t) + MinRenderThickness;

                Vector2 normal   = (StartAngle + Sweep * t).ToRotationVector2();
                float   outerLen = Radius - outerInset;
                float   innerLen = outerLen - thickness;

                Vector2 outerPos = center + normal * outerLen;
                Vector2 innerPos = center + normal * innerLen;

                // v = 0 at the outer edge, 1 at the inner (trailing) edge — matches SlashInk.fx's UV
                // convention for its edge-bleed and trailing dry-brush streak.
                verts[i * 2]     = new VertexPositionColorTexture(new Vector3(outerPos, 0f), outerColor, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(innerPos, 0f), innerColor, new Vector2(t, 1f));
            }

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, vertCount - 2);
            }
        }
    }
}
