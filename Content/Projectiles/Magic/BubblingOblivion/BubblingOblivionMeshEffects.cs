using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Mesh-based Bubble-form effects — drawn as their own geometry via BubbleTrail.fx, independent of
    // any sprite's texture bounds (the "exists in its own form, boundless to the sprite" analog of
    // CryingTides' SlashArcEffect). Currently just the bolt trail; the halo/pop-shockwave ideas
    // discussed alongside it would land here too if they pan out.
    internal static class BubblingOblivionMeshEffects
    {
        // Draws a tapering ribbon strip through `history` (oldest to newest, world-space positions) —
        // zero width at the tail, `maxWidth` at the head. No-ops if the shader failed to load or there
        // aren't at least 2 points to connect. `headOffset` slides the whole ribbon backward along the
        // head's own direction of travel, so the full-width head sits a bit behind the sprite's actual
        // center instead of exactly on top of it — without this, the ribbon's straight-edged head
        // peeks out from under the (round) sprite right at its center, reading as a visual glitch.
        public static void DrawTrail(List<Vector2> history, float maxWidth, float ageSeconds, float seed, float headOffset = 12f)
        {
            Effect effect = KyoMod.BubbleTrailShader?.Value;
            if (effect == null || history.Count < 2)
                return;

            int count = history.Count;
            var verts = new VertexPositionColorTexture[count * 2];

            Vector2 headTangent = (history[count - 1] - history[count - 2]).SafeNormalize(Vector2.UnitX);
            Vector2 backOffset = headTangent * headOffset;

            for (int i = 0; i < count; i++)
            {
                Vector2 point = history[i] - Main.screenPosition - backOffset;

                Vector2 tangent;
                if (i == 0)
                    tangent = history[1] - history[0];
                else if (i == count - 1)
                    tangent = history[i] - history[i - 1];
                else
                    tangent = history[i + 1] - history[i - 1];

                if (tangent.LengthSquared() < 0.0001f)
                    tangent = Vector2.UnitX;
                tangent.Normalize();
                Vector2 normal = new Vector2(-tangent.Y, tangent.X);

                float t = i / (float)(count - 1); // 0 = tail (oldest), 1 = head (current position)
                float halfWidth = maxWidth * t * 0.5f;

                Vector2 outer = point + normal * halfWidth;
                Vector2 inner = point - normal * halfWidth;

                verts[i * 2]     = new VertexPositionColorTexture(new Vector3(outer, 0f), Color.White, new Vector2(t, 1f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(inner, 0f), Color.White, new Vector2(t, 0f));
            }

            Matrix viewProj = Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1f, 1f);

            effect.Parameters["uViewProj"]?.SetValue(viewProj);
            effect.Parameters["uTime"]?.SetValue(ageSeconds);
            effect.Parameters["uSeed"]?.SetValue(seed);
            BubblingOblivionVisuals.ApplyPalette(effect);

            GraphicsDevice device = Main.spriteBatch.GraphicsDevice;
            Main.spriteBatch.End();

            var savedRasterizer = device.RasterizerState;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            device.BlendState = BlendState.AlphaBlend;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            device.RasterizerState = savedRasterizer;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private const int AuraDiscSegments = 32;

        // Shared mesh/draw core for BubbleAuraRing.fx — a flat unit-circle fan (built here as a
        // triangle list: FNA's DrawUserPrimitives has no TriangleFan primitive type) scaled to
        // `meshRadius`, with the actual visible "band" shape (glow disc vs. thin traveling ring)
        // entirely controlled by the shader's uRingRadius/uRingWidth — see that file's own header.
        // Additive-blended: this is a glow, not a flat translucent disc.
        private static void DrawAuraDisc(Vector2 worldCenter, float meshRadius, float ringRadius01, float ringWidth01, float ageSeconds, float seed, float opacity)
        {
            Effect effect = KyoMod.BubbleAuraRingShader?.Value;
            if (effect == null || meshRadius <= 0f || opacity <= 0f)
                return;

            Vector2 screenCenter = worldCenter - Main.screenPosition;
            var verts = new VertexPositionColorTexture[AuraDiscSegments * 3];

            for (int i = 0; i < AuraDiscSegments; i++)
            {
                Vector2 dir0 = (MathHelper.TwoPi * i / AuraDiscSegments).ToRotationVector2();
                Vector2 dir1 = (MathHelper.TwoPi * (i + 1) / AuraDiscSegments).ToRotationVector2();

                verts[i * 3]     = new VertexPositionColorTexture(new Vector3(screenCenter, 0f), Color.White, Vector2.Zero);
                verts[i * 3 + 1] = new VertexPositionColorTexture(new Vector3(screenCenter + dir0 * meshRadius, 0f), Color.White, dir0);
                verts[i * 3 + 2] = new VertexPositionColorTexture(new Vector3(screenCenter + dir1 * meshRadius, 0f), Color.White, dir1);
            }

            Matrix viewProj = Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1f, 1f);

            effect.Parameters["uViewProj"]?.SetValue(viewProj);
            effect.Parameters["uTime"]?.SetValue(ageSeconds);
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uRingRadius"]?.SetValue(ringRadius01);
            effect.Parameters["uRingWidth"]?.SetValue(ringWidth01);
            effect.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(opacity, 0f, 1f));
            BubblingOblivionVisuals.ApplyPalette(effect);

            GraphicsDevice device = Main.spriteBatch.GraphicsDevice;
            Main.spriteBatch.End();

            var savedRasterizer = device.RasterizerState;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            device.BlendState = BlendState.Additive;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, AuraDiscSegments);
            }

            device.RasterizerState = savedRasterizer;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // Static halo/aura around the big bubble — a soft glow disc larger than the sprite itself,
        // bright at the center and fading toward the edge (uRingRadius = 0 collapses the shader's
        // "band" into a plain radial falloff). `radius` should track the sprite's own current visual
        // size so the halo grows/shrinks along with it instead of staying a fixed size.
        public static void DrawHalo(Vector2 worldCenter, float radius, float ageSeconds, float seed, float opacity = 0.5f)
        {
            DrawAuraDisc(worldCenter, radius, ringRadius01: 0f, ringWidth01: 1.1f, ageSeconds, seed, opacity);
        }

        // Pop shockwave — a thin ring expanding outward from the pop point to maxRadius as `progress`
        // (0..1, caller owns the timing) advances, fading in and back out via a sine envelope instead
        // of a hard cut, so popping reads as a genuine burst rather than just the particle telegraph
        // ring (BigBubble.Pop()) on its own. Marbled with the full 8-color palette, same as the halo —
        // the pop particles are the ones that match this, not the other way around: each particle
        // picks a random stop from that same palette (BubblingOblivionVisuals.RandomPopColor) so the
        // burst as a whole spans the ring's own color range instead of being a single flat tone.
        public static void DrawPopShockwave(Vector2 worldCenter, float maxRadius, float progress, float ageSeconds, float seed)
        {
            progress = MathHelper.Clamp(progress, 0f, 1f);
            float alpha = MathF.Sin(MathHelper.Pi * progress);
            DrawAuraDisc(worldCenter, maxRadius, ringRadius01: progress, ringWidth01: 0.12f, ageSeconds, seed, alpha);
        }

        private const int ImpactStarSpikes = 4;
        private const float ImpactStarLength = 70f;
        private const float ImpactStarWidth = 15f; // peak width at the bulge, not a uniform thickness
        private const int ImpactStarSegments = 14;
        private static readonly Color ImpactStarColor = Color.Lerp(Color.White, new Color(178, 102, 255), 0.55f);

        // Sparkle/star-flare width profile: pinched to ~0 right at the center, rising fast to a bulge
        // close in, then a long smooth taper out to a fine point at the tip — NOT a uniform-thickness
        // rectangle. Same normalized-power-curve trick as SlashArcEffect.ThicknessAt (small P = fast
        // rise, larger Q = long slow taper), just with very different exponents for a very different
        // silhouette.
        private const float SpikeShapeP = 0.35f;
        private const float SpikeShapeQ = 2.5f;
        private static readonly float SpikePeakT = SpikeShapeP / (SpikeShapeP + SpikeShapeQ);
        private static readonly float SpikePeakVal = MathF.Pow(SpikePeakT, SpikeShapeP) * MathF.Pow(1f - SpikePeakT, SpikeShapeQ);

        private static float SpikeWidthAt(float t)
        {
            float raw = MathF.Pow(MathHelper.Clamp(t, 0.0001f, 1f), SpikeShapeP) * MathF.Pow(1f - t, SpikeShapeQ);
            float normalized = SpikePeakVal > 0f ? raw / SpikePeakVal : 0f;
            return ImpactStarWidth * normalized;
        }

        // Cached rather than constructed per-draw — a plain untextured vertex-colored effect is all
        // this needs (no custom .fx: BasicEffect is a standard built-in MonoGame/FNA class), used only
        // for solid-color geometry like this star, not anything that samples a texture or needs
        // procedural noise.
        private static BasicEffect _flashEffect;

        private static BasicEffect GetFlashEffect(GraphicsDevice device)
        {
            if (_flashEffect == null)
            {
                _flashEffect = new BasicEffect(device)
                {
                    VertexColorEnabled = true,
                    World = Matrix.Identity,
                    View = Matrix.Identity,
                };
            }
            return _flashEffect;
        }

        // A cross-shaped "hit spark" flash — four tapered spikes shooting outward from `worldCenter`
        // then fading, additive-blended for the classic bright-glow impact look. `progress` is 0..1
        // across the flash's own short lifetime (the caller owns the timing/duration). Each spike is
        // an actual tapered ribbon mesh (SpikeWidthAt), not a stretched rectangle — a uniform-width
        // quad reads as a plus sign, not a sparkle/star flare.
        public static void DrawImpactStar(Vector2 worldCenter, float progress)
        {
            GraphicsDevice device = Main.spriteBatch.GraphicsDevice;
            Vector2 screenCenter = worldCenter - Main.screenPosition;

            float lengthT = 1f - (1f - progress) * (1f - progress); // ease-out: shoots out fast, then holds
            float length = ImpactStarLength * lengthT;
            float alpha = 1f - progress * progress; // fades slowly at first, quickly near the end
            Color color = ImpactStarColor * alpha;

            var verts = new VertexPositionColor[(ImpactStarSegments + 1) * 2];

            Main.spriteBatch.End();

            var savedRasterizer = device.RasterizerState;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            device.BlendState = BlendState.Additive;

            BasicEffect effect = GetFlashEffect(device);
            effect.Projection = Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1f, 1f);

            for (int spike = 0; spike < ImpactStarSpikes; spike++)
            {
                float angle = MathHelper.TwoPi * spike / ImpactStarSpikes;
                Vector2 direction = angle.ToRotationVector2();
                Vector2 normal = new Vector2(-direction.Y, direction.X);

                for (int i = 0; i <= ImpactStarSegments; i++)
                {
                    float t = i / (float)ImpactStarSegments;
                    float halfWidth = SpikeWidthAt(t) * 0.5f;
                    Vector2 along = screenCenter + direction * (length * t);

                    verts[i * 2]     = new VertexPositionColor(new Vector3(along + normal * halfWidth, 0f), color);
                    verts[i * 2 + 1] = new VertexPositionColor(new Vector3(along - normal * halfWidth, 0f), color);
                }

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
                }
            }

            device.RasterizerState = savedRasterizer;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
