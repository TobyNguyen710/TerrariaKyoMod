using System;
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
        // verticalScale: squashes the whole thing vertically. 1 is the true circular black hole; below
        // that it becomes eye-shaped, and at ~0 it is a shut slit.
        // tipSharpness: 1 leaves that squash an ellipse, whose ends are round by construction. Above 1
        // the squash deepens toward the sides, pinching the ends into the points of a lens. It changes
        // only the ends — full height at the top and full width at the tips are both untouched, since
        // |sin| is 1 and 0 there and neither moves under a power.
        //
        // Both are applied to the VERTEX POSITIONS ONLY — UVs stay on the unit circle, so the shader
        // still solves the lensing in undistorted circular space and the finished image is simply warped
        // on its way to the screen. That distinction is the whole reason this works. The warp is smooth
        // and every vertex gets the same rule, so no feature can part company with its neighbour the way
        // it did when the radius metric was warped INSIDE the shader — see the note atop VoidBlackHole.fx.
        //
        // The fan interpolates linearly from the centre out, so this shapes the silhouette exactly and
        // pulls everything inside it along proportionally: the black shadow becomes the same lens shape
        // at a smaller scale, which is what makes the pupil match the eye instead of sitting round
        // inside a pointed one.
        // frontBand: leave at 1 for a normal black hole — it's the disk's near side crossing in front of
        // the shadow, and it's what joins the ribbon's left and right halves into one continuous streak.
        // Pass 0 only when something else is going to occupy the shadow (the Void field's iris does).
        public static void DrawBlackHole(Vector2 worldCenter, float meshRadius, float shadowRadius01, float diskOuterInShadowRadii, float ageSeconds, float seed, float opacity, float verticalScale = 1f, float frontBand = 1f, float tipSharpness = 1f)
        {
            Effect effect = KyoMod.VoidBlackHoleShader?.Value;
            if (effect == null || meshRadius <= 0f || opacity <= 0f || verticalScale <= 0f)
                return;

            Vector2 screenCenter = worldCenter - Main.screenPosition;
            var verts = new VertexPositionColorTexture[BlackHoleSegments * 3];

            for (int i = 0; i < BlackHoleSegments; i++)
            {
                Vector2 dir0 = (MathHelper.TwoPi * i / BlackHoleSegments).ToRotationVector2();
                Vector2 dir1 = (MathHelper.TwoPi * (i + 1) / BlackHoleSegments).ToRotationVector2();

                // Position shaped, UV left alone — see the method comment.
                Vector2 pos0 = ShapeRim(dir0, meshRadius, verticalScale, tipSharpness);
                Vector2 pos1 = ShapeRim(dir1, meshRadius, verticalScale, tipSharpness);

                verts[i * 3]     = new VertexPositionColorTexture(new Vector3(screenCenter, 0f), Color.White, Vector2.Zero);
                verts[i * 3 + 1] = new VertexPositionColorTexture(new Vector3(screenCenter + pos0, 0f), Color.White, dir0);
                verts[i * 3 + 2] = new VertexPositionColorTexture(new Vector3(screenCenter + pos1, 0f), Color.White, dir1);
            }

            Matrix viewProj = Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1f, 1f);

            effect.Parameters["uViewProj"]?.SetValue(viewProj);
            effect.Parameters["uTime"]?.SetValue(ageSeconds);
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uCoreRadius"]?.SetValue(shadowRadius01);
            effect.Parameters["uDiskOuter"]?.SetValue(diskOuterInShadowRadii);
            effect.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(opacity, 0f, 1f));
            effect.Parameters["uFrontBand"]?.SetValue(MathHelper.Clamp(frontBand, 0f, 1f));
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

        // Where a rim vertex in direction `dir` actually lands. The x is left alone, so the silhouette
        // keeps its full width; the y is squashed, then raised to a power that deepens the squash toward
        // the sides. sin is 1 at the top and 0 at the tips, and a power moves neither, so this pinches
        // the ends into points without altering the eye's height or width.
        private static Vector2 ShapeRim(Vector2 dir, float radius, float verticalScale, float tipSharpness)
        {
            float y = dir.Y;
            if (tipSharpness != 1f)
                y = MathF.Sign(y) * MathF.Pow(MathF.Abs(y), tipSharpness);

            return new Vector2(dir.X * radius, y * radius * verticalScale);
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

            DrawPrimitives(verts, verts.Length / 3, BlendState.Additive);
        }

        // The device-state dance every one of these raw meshes needs: drop out of the sprite batch, draw,
        // then put the batch back exactly as Terraria expects it. Shared rather than copied per mesh,
        // because restoring one of these wrongly corrupts every sprite drawn after it that frame — a bug
        // that surfaces somewhere completely unrelated to the effect that caused it.
        private static void DrawPrimitives(VertexPositionColor[] verts, int triangleCount, BlendState blend)
        {
            if (triangleCount <= 0)
                return;

            GraphicsDevice device = Main.spriteBatch.GraphicsDevice;
            Main.spriteBatch.End();

            var savedRasterizer = device.RasterizerState;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            device.BlendState = blend;

            BasicEffect effect = GetRingEffect(device);
            effect.Projection = Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1f, 1f);

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, triangleCount);
            }

            device.RasterizerState = savedRasterizer;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // ---- The eye inside the Void field's black hole ------------------------------------------
        //
        // The field's core is a black hole with an EYE INSIDE IT — not an eye that uses a black hole as
        // its iris. The black hole IS the eye's outer form: the round shadow is the eyeball and the
        // lensed disk sweeping round it supplies the lids, which the shape of a lensed black hole already
        // resembles closely enough that drawing an outline inside it only added a second, competing eye.
        //
        // So the only thing drawn here is the IRIS. Everything surrounding it is the shader's, unmodified.
        //
        // Because the shadow is already black, the iris is pure additive glow and its dark pupil is just
        // the shadow showing through the middle. Nothing here needs to subtract light, and no pupil is
        // drawn at all.
        //
        // Nothing here touches VoidBlackHole.fx, and that is deliberate. An earlier attempt to give the
        // black hole a non-circular silhouette by warping the radius metric inside the shader failed over
        // and over (design doc §13): the radius that forms the lensing IS the radius that forms the
        // silhouette, so squashing one either drifted back toward a circle or tore open a seam where the
        // warped band left the circular rim. Keeping the eye as separate geometry means none of that can
        // come back — and at ~450 of 512 instruction slots there was no room for it regardless.

        private const int IrisSegments = 64; // enough to resolve the flow pattern's finest streaks

        // The iris outline. Equal half-axes give a circle; squashing the height flattens it toward a
        // horizontal slit, which is how the blink is expressed now that there is no eyelid to close.
        private static Vector2[] BuildEllipseLoop(float halfWidth, float halfHeight)
        {
            var loop = new Vector2[IrisSegments];
            for (int i = 0; i < IrisSegments; i++)
            {
                Vector2 dir = (MathHelper.TwoPi * i / IrisSegments).ToRotationVector2();
                loop[i] = new Vector2(dir.X * halfWidth, dir.Y * halfHeight);
            }
            return loop;
        }

        // A soft glowing band running along any closed convex loop, fading to nothing on both sides.
        // Written against any loop rather than against the iris specifically, so the next glowing outline
        // Void needs can reuse it instead of growing a third near-identical copy of this vertex layout.
        // brightness, when given, scales the colour per vertex — that's what lets a band carry a moving
        // pattern along itself rather than being one flat tone.
        private static void DrawLoopBand(Vector2 screenCentre, Vector2[] loop, float[] halfThickness, Color color, float[] brightness = null)
        {
            Color edge = color * 0f; // transparent; contributes nothing under additive blending
            var verts = new VertexPositionColor[loop.Length * 12]; // 4 triangles per segment
            int v = 0;

            for (int i = 0; i < loop.Length; i++)
            {
                int j = (i + 1) % loop.Length;
                Vector2 a = loop[i];
                Vector2 b = loop[j];

                // Outward direction. For a convex loop the direction from the centre to the point is a
                // good enough normal, and unlike a true edge normal it never degenerates at the lens's
                // tips — there it points cleanly along the axis, exactly where the band should go.
                Vector2 dirA = a.SafeNormalize(Vector2.UnitX);
                Vector2 dirB = b.SafeNormalize(Vector2.UnitX);
                float hA = halfThickness[i];
                float hB = halfThickness[j];

                Color colA = brightness == null ? color : color * brightness[i];
                Color colB = brightness == null ? color : color * brightness[j];

                Vector3 midA = new Vector3(screenCentre + a, 0f);
                Vector3 midB = new Vector3(screenCentre + b, 0f);
                Vector3 inA = new Vector3(screenCentre + a - dirA * hA, 0f);
                Vector3 inB = new Vector3(screenCentre + b - dirB * hB, 0f);
                Vector3 outA = new Vector3(screenCentre + a + dirA * hA, 0f);
                Vector3 outB = new Vector3(screenCentre + b + dirB * hB, 0f);

                verts[v++] = new VertexPositionColor(inA, edge);
                verts[v++] = new VertexPositionColor(midA, colA);
                verts[v++] = new VertexPositionColor(midB, colB);
                verts[v++] = new VertexPositionColor(inA, edge);
                verts[v++] = new VertexPositionColor(midB, colB);
                verts[v++] = new VertexPositionColor(inB, edge);

                verts[v++] = new VertexPositionColor(midA, colA);
                verts[v++] = new VertexPositionColor(outA, edge);
                verts[v++] = new VertexPositionColor(outB, edge);
                verts[v++] = new VertexPositionColor(midA, colA);
                verts[v++] = new VertexPositionColor(outB, edge);
                verts[v++] = new VertexPositionColor(midB, colB);
            }

            DrawPrimitives(verts, verts.Length / 3, BlendState.Additive);
        }
        // Draws the iris, and only the iris. Call AFTER DrawBlackHole at a size that fits well inside its
        // shadow, so it sits in the black with room to spare rather than crowding the disk — everything
        // around it, the eye's whole outer form, is the black hole itself.
        //
        // The dark pupil in the middle is the shadow showing through, so no pupil is drawn.
        //
        // halfHeight below halfWidth narrows the iris toward a horizontal slit. That is how the blink is
        // expressed now that there is no eyelid: a fully closed iris flattens onto the same horizontal
        // line the lensed disk already draws across the shadow, so the eye shuts INTO the black hole's own
        // geometry instead of over the top of it.
        //
        // Two passes, as everywhere else in Void's visuals — a wide soft band in the body colour with a
        // narrow near-white line down its middle. Additively that lands as a white-hot core bleeding out
        // through lavender, which is what makes a thin ring read as glowing rather than merely bright.
        public static void DrawIris(Vector2 worldCentre, float halfWidth, float halfHeight, float lineWidth,
            float time, float seed, float opacity)
        {
            if (halfWidth <= 0.5f || halfHeight <= 0f || lineWidth <= 0f || opacity <= 0f)
                return;

            Vector2 screenCentre = worldCentre - Main.screenPosition;
            Vector2[] iris = BuildEllipseLoop(halfWidth, halfHeight);

            var wide = new float[iris.Length];
            var thin = new float[iris.Length];
            var glow = new float[iris.Length];

            for (int i = 0; i < iris.Length; i++)
            {
                // One value drives BOTH the ring's thickness and its brightness at this point, so bright
                // stretches are also fatter. Splitting them into two independent patterns was the
                // alternative and it looks wrong — two unrelated things sliding over each other rather
                // than one piece of material moving.
                float flow = IrisFlow(MathHelper.TwoPi * i / iris.Length, time, seed);

                float w = lineWidth * (0.80f + 0.40f * flow);
                wide[i] = w * 1.8f;
                thin[i] = w * 0.7f;
                glow[i] = 0.45f + 0.55f * flow;
            }

            DrawLoopBand(screenCentre, iris, wide, new Color(Color0) * opacity, glow);
            DrawLoopBand(screenCentre, iris, thin, new Color(Color2) * (opacity * 0.95f), glow);
        }

        // The material running around the iris at a given point on it, 0..1.
        //
        // Every frequency is a whole number of cycles around the loop, which is what makes the pattern
        // seamless: theta = 0 and theta = 2*pi land on identical values by construction, so there is no
        // join to hide. Same reasoning as the shader's circle-sampled noise (design doc §13.3) — sampling
        // along a closed path costs nothing extra to make seam-free, whereas wrapping a noise lattice by
        // hand is both fiddly and expensive.
        //
        // Every term subtracts time rather than adding it, so all of them travel the SAME way round the
        // ring. That is the difference between flowing and merely shimmering: the earlier version mixed
        // the signs, and mixed signs cancel into a pattern that seethes in place instead of going
        // anywhere. The three rates differ so the pattern never visibly repeats, and the ridged third
        // supplies the bright filament streaks over the smoother roll of the first two — the same
        // construction the shader's disk filaments use.
        private static float IrisFlow(float theta, float time, float seed)
        {
            float f = 0.5f
                    + 0.22f * MathF.Sin(theta * 2f - time * 1.5f + seed)
                    + 0.14f * MathF.Sin(theta * 5f - time * 2.3f + seed * 1.7f)
                    + 0.26f * MathF.Pow(MathF.Abs(MathF.Sin(theta * 11f - time * 1.1f + seed * 0.9f)), 0.7f);

            return MathHelper.Clamp(f, 0f, 1f);
        }
    }
}
