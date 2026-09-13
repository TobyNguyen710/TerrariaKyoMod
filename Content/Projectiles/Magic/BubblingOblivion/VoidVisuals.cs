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
        // VOID FORM'S PALETTE — form-wide, not just the black hole's. Settled after the black hole's
        // colors landed ("the void form palette will be based around these colors then"), so anything
        // Void gains later (the diamond projectile, distortion field, player halo, particles) should be
        // drawn from this same set rather than picking its own. Changing these changes Void's whole
        // identity. Supersedes the weapon's original 57ffff-cyan spec for Void; design doc §4/§13.6.
        //
        // The shader ramps these as
        // Color3 -> Color1 -> Color0 -> Color2, i.e. darkest through to hottest. The lensed disk is
        // only legible because brightness climbs from the black shadow out to the hot cores, so where
        // each color sits in that order matters more than the hues do.
        //
        // Brightness climbs cleanly across all four (292769 -> 2b248f -> 5a3d9e -> dfefff), so there's no
        // dip at the midtone. Hue runs violet through the darks into lavender for the body, then white.
        //
        // Colors that aren't in this list still show up on screen, and the channel ORDER decides which:
        // as a spot heats up, whichever channel is largest clips first while the others keep climbing.
        // Back when the body was a pure blue (103084, red almost absent) that meant blue -> cyan -> white.
        // Lavender puts red well above green, so it now runs lavender -> pink/magenta -> white instead.
        // If the highlights ever need to go back toward cyan, that's a red-vs-green question here, not
        // something to change in the shader (see the composite in VoidBlackHole.fx).
        public static readonly Vector3 Color0 = new Vector3(0.3529f, 0.2392f, 0.6196f); // 5a3d9e — dark lavender, the main disk body
        public static readonly Vector3 Color1 = new Vector3(0.1686f, 0.1412f, 0.5608f); // 2b248f — indigo, the cooler midtones
        public static readonly Vector3 Color2 = new Vector3(0.8745f, 0.9373f, 1f);      // dfefff — ice white, the hottest cores
        public static readonly Vector3 Color3 = new Vector3(0.1608f, 0.1529f, 0.4118f); // 292769 — muted violet floor
        // Previous sets, kept for reference:
        // Bubble-family (magenta/cyan, matched to BubblingOblivionVisuals):
        // Color0 (0.471f, 0.918f, 0.922f) | Color1 (0.8f, 0.188f, 0.682f) | Color2 (0.957f, 0.976f, 0.988f) | Color3 (0.224f, 0.053f, 0.191f)
        // Warm/Interstellar (used while matching the reference's shape):
        // Color0 (1f, 0.50f, 0.12f) | Color1 (0.62f, 0.14f, 0.03f) | Color2 (1f, 0.95f, 0.86f) | Color3 (0.16f, 0.03f, 0.005f)
        // Original Void cyan (57ffff and shades):
        // Color0 (0.341f, 1f, 1f) | Color1 (0.102f, 0.4f, 0.4f) | Color2 (0.816f, 1f, 1f) | Color3 (0.039f, 0.149f, 0.149f)

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

        private static BasicEffect _ringEffect;

        private static BasicEffect GetRingEffect(GraphicsDevice device)
        {
            if (_ringEffect == null)
            {
                _ringEffect = new BasicEffect(device)
                {
                    VertexColorEnabled = true,
                    World = Matrix.Identity,
                    View = Matrix.Identity,
                };
            }
            return _ringEffect;
        }

        // A diamond (rhombus) ring outline — the Void field's border and its outward pulses (design doc
        // §14.2). `radius` is the distance from the centre to a POINT of the diamond, i.e. the L1 radius:
        // the set of points where |dx| + |dy| == radius is exactly a diamond, the same way |(dx,dy)| ==
        // radius is a circle. The collision test in BubblingOblivionVoidPulse uses that same L1 metric,
        // so what's drawn and what actually hits are the same shape by construction, not by eye.
        //
        // No .fx shader: this is plain untextured vertex-coloured geometry, so there's nothing a pixel
        // shader would add (§4's "the outline shape itself doesn't need a shader" note). Soft edges come
        // from vertex alpha instead — the inner and outer rims are fully transparent and the middle of
        // the band is fully opaque, so the ring fades out on both sides rather than having hard edges.
        public static void DrawDiamondRing(Vector2 worldCenter, float radius, float halfThickness, Color color)
        {
            if (radius <= 0f || halfThickness <= 0f)
                return;

            GraphicsDevice device = Main.spriteBatch.GraphicsDevice;
            Vector2 screenCenter = worldCenter - Main.screenPosition;

            // Diamonds of different L1 radii are similar shapes, so the inner/mid/outer bands are just
            // the same four corner directions scaled by three different radii.
            Vector2[] corners = { new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(0f, -1f) };
            float inner = MathHelper.Max(radius - halfThickness, 0f);
            float outer = radius + halfThickness;
            Color edge = color * 0f; // transparent: with additive blending this contributes nothing

            var verts = new VertexPositionColor[48]; // 4 edges * 2 bands * 2 triangles * 3 vertices
            int v = 0;

            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i];
                Vector2 b = corners[(i + 1) % 4];

                Vector3 innerA = new Vector3(screenCenter + a * inner, 0f);
                Vector3 innerB = new Vector3(screenCenter + b * inner, 0f);
                Vector3 midA   = new Vector3(screenCenter + a * radius, 0f);
                Vector3 midB   = new Vector3(screenCenter + b * radius, 0f);
                Vector3 outerA = new Vector3(screenCenter + a * outer, 0f);
                Vector3 outerB = new Vector3(screenCenter + b * outer, 0f);

                // inner rim -> centre of the band
                verts[v++] = new VertexPositionColor(innerA, edge);
                verts[v++] = new VertexPositionColor(midA, color);
                verts[v++] = new VertexPositionColor(midB, color);
                verts[v++] = new VertexPositionColor(innerA, edge);
                verts[v++] = new VertexPositionColor(midB, color);
                verts[v++] = new VertexPositionColor(innerB, edge);

                // centre of the band -> outer rim
                verts[v++] = new VertexPositionColor(midA, color);
                verts[v++] = new VertexPositionColor(outerA, edge);
                verts[v++] = new VertexPositionColor(outerB, edge);
                verts[v++] = new VertexPositionColor(midA, color);
                verts[v++] = new VertexPositionColor(outerB, edge);
                verts[v++] = new VertexPositionColor(midB, color);
            }

            Main.spriteBatch.End();

            var savedRasterizer = device.RasterizerState;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            device.BlendState = BlendState.Additive;

            BasicEffect effect = GetRingEffect(device);
            effect.Projection = Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1f, 1f);

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, verts.Length / 3);
            }

            device.RasterizerState = savedRasterizer;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
