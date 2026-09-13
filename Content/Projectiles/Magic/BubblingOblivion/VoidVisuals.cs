using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Void form's own visual identity, parallel to BubblingOblivionVisuals (Bubble form's palette/draw
    // helper) — kept as a separate file/palette rather than folded into that one, since Void's color
    // scheme (user's own spec: "besides the black color of the black hole, the main color scheme
    // should be 57ffff and shades of it") is deliberately distinct from Bubble's 8-color pastel set,
    // not a variation on it.
    internal static class VoidVisuals
    {
        // TEMPORARY — swapped to the reference image's own warm ember gradient per explicit request,
        // purely so shape/structure tuning can be compared directly against the reference without the
        // color scheme getting in the way. Revert to the 57ffff cyan set (commented below) once the
        // shape work is settled. The shader ramps these as Color3 -> Color1 -> Color0 -> Color2, i.e.
        // coolest/darkest through to hottest, so the cyan set slots straight back in unchanged.
        public static readonly Vector3 Color0 = new Vector3(1f, 0.50f, 0.12f);      // orange (mid heat)
        public static readonly Vector3 Color1 = new Vector3(0.62f, 0.14f, 0.03f);   // deep red-orange
        public static readonly Vector3 Color2 = new Vector3(1f, 0.95f, 0.86f);      // near-white (hottest)
        public static readonly Vector3 Color3 = new Vector3(0.16f, 0.03f, 0.005f);  // dark ember floor
        // public static readonly Vector3 Color0 = new Vector3(0.341f, 1f, 1f);       // 57ffff — bright cyan
        // public static readonly Vector3 Color1 = new Vector3(0.102f, 0.4f, 0.4f);   // darker teal
        // public static readonly Vector3 Color2 = new Vector3(0.816f, 1f, 1f);       // near-white cyan highlight
        // public static readonly Vector3 Color3 = new Vector3(0.039f, 0.149f, 0.149f); // deep near-black teal

        public static void ApplyPalette(Effect effect)
        {
            effect.Parameters["uColor0"]?.SetValue(Color0);
            effect.Parameters["uColor1"]?.SetValue(Color1);
            effect.Parameters["uColor2"]?.SetValue(Color2);
            effect.Parameters["uColor3"]?.SetValue(Color3);
        }

        private const int BlackHoleSegments = 64; // higher than BubbleAuraRing's 32 — this is drawn much
                                                    // larger on screen, so the fan needs to stay smooth.

        // Black hole disc — same unit-circle-fan mesh technique as BubblingOblivionMeshEffects.DrawAuraDisc
        // (triangle list, not a native TriangleFan primitive — see that method's own comment for why),
        // duplicated rather than shared since this is a different shader with a different uniform set
        // (uCoreRadius instead of uRingRadius/uRingWidth), consistent with this project's existing
        // "each effect owns its own mesh-building code" convention.
        // shadowRadius01: radius of the black shadow, as a fraction of meshRadius.
        // diskOuterInShadowRadii: how far the accretion disk reaches, measured in shadow radii. The
        // disk's horizontal reach is roughly this value, so shadowRadius01 * diskOuter must stay under
        // ~0.9 or the disk gets clipped flat against the mesh's own rim.
        public static void DrawBlackHole(Vector2 worldCenter, float meshRadius, float shadowRadius01, float diskOuterInShadowRadii, float ageSeconds, float seed, float opacity)
        {
            Effect effect = KyoMod.VoidBlackHoleShader?.Value;
            if (effect == null || meshRadius <= 0f || opacity <= 0f)
                return;

            Vector2 screenCenter = worldCenter - Main.screenPosition;
            var verts = new VertexPositionColorTexture[BlackHoleSegments * 3];

            for (int i = 0; i < BlackHoleSegments; i++)
            {
                Vector2 dir0 = (MathHelper.TwoPi * i / BlackHoleSegments).ToRotationVector2();
                Vector2 dir1 = (MathHelper.TwoPi * (i + 1) / BlackHoleSegments).ToRotationVector2();

                verts[i * 3]     = new VertexPositionColorTexture(new Vector3(screenCenter, 0f), Color.White, Vector2.Zero);
                verts[i * 3 + 1] = new VertexPositionColorTexture(new Vector3(screenCenter + dir0 * meshRadius, 0f), Color.White, dir0);
                verts[i * 3 + 2] = new VertexPositionColorTexture(new Vector3(screenCenter + dir1 * meshRadius, 0f), Color.White, dir1);
            }

            Matrix viewProj = Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1f, 1f);

            effect.Parameters["uViewProj"]?.SetValue(viewProj);
            effect.Parameters["uTime"]?.SetValue(ageSeconds);
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uCoreRadius"]?.SetValue(shadowRadius01);
            effect.Parameters["uDiskOuter"]?.SetValue(diskOuterInShadowRadii);
            effect.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(opacity, 0f, 1f));
            ApplyPalette(effect);

            GraphicsDevice device = Main.spriteBatch.GraphicsDevice;
            Main.spriteBatch.End();

            var savedRasterizer = device.RasterizerState;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            device.BlendState = BlendState.AlphaBlend; // solid black core needs real coverage, not additive glow

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, BlackHoleSegments);
            }

            device.RasterizerState = savedRasterizer;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
