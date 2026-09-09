using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Lightweight custom particle system for Bubble form, replacing vanilla Dust for effects that need
    // to reproduce this weapon's exact palette. Vanilla Dust.color is a straight multiply against each
    // dust type's own baked-in texture color (see that field's own doc comment: "works best with
    // sprites with low saturation... not as effective with colored sprites") — tried DustID.Water
    // (pre-baked blue) then DustID.TintableDust (turned out to have a dim/grey-ish base despite the
    // name, producing a muddy result in-game — "I dont think thats good... i like the one before
    // more"), neither reproduced the palette cleanly. These particles are plain vertex-colored dots
    // (BubbleParticle.fx, no texture/base-color multiply involved at all), so the color passed in is
    // the color that comes out.
    public class BubblingOblivionParticleSystem : ModSystem
    {
        private struct Particle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Scale;
            public float Drag;
            public int Age;
            public int Lifetime;
        }

        private const int MaxParticles = 400;
        private static readonly List<Particle> particles = new(MaxParticles);

        // drag: velocity multiplier applied every tick (1f = no slowdown, e.g. for the inward-pulled
        // collapse particles which should stay brisk all the way to the center).
        public static void Spawn(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime, float drag = 0.97f)
        {
            if (particles.Count >= MaxParticles)
                return;

            particles.Add(new Particle
            {
                Position = position,
                Velocity = velocity,
                Color = color,
                Scale = scale,
                Drag = drag,
                Age = 0,
                Lifetime = lifetime,
            });
        }

        // Convenience wrapper matching the old Dust.NewDust(position, width, height, ...) shape: a
        // random point inside `rect` with a small random drift velocity — the ordinary "ambient
        // sparkle" spawn pattern used in a few places.
        public static void SpawnInRect(Rectangle rect, Color color, float scale, int lifetime, float driftSpeed = 0.6f, float drag = 0.97f)
        {
            Vector2 position = new Vector2(rect.X + Main.rand.NextFloat(rect.Width), rect.Y + Main.rand.NextFloat(rect.Height));
            Vector2 velocity = Main.rand.NextVector2Circular(driftSpeed, driftSpeed);
            Spawn(position, velocity, color, scale, lifetime, drag);
        }

        public override void PostUpdateDusts()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                p.Age++;
                if (p.Age >= p.Lifetime)
                {
                    particles.RemoveAt(i);
                    continue;
                }

                p.Position += p.Velocity;
                p.Velocity *= p.Drag;
                particles[i] = p;
            }
        }

        public override void PostDrawTiles()
        {
            if (particles.Count == 0)
                return;

            Effect effect = KyoMod.BubbleParticleShader?.Value;
            if (effect == null)
                return;

            Texture2D pixel = BubblingOblivionDebug.GetPixel(Main.spriteBatch.GraphicsDevice);
            Vector2 origin = new Vector2(0.5f, 0.5f);

            // SpriteSortMode.Immediate, not Deferred — each particle sets a different uColor before its
            // own draw call, and Deferred batches draw calls together and only applies effect
            // parameters once per batch, not per sprite (the same reasoning already established for
            // BubblingOblivionVisuals.DrawSwirl's per-instance shader parameters).
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, effect, Main.GameViewMatrix.TransformationMatrix);

            foreach (Particle p in particles)
            {
                float lifeT = p.Age / (float)p.Lifetime;
                float fade = 1f - lifeT * lifeT; // fades slowly at first, quickly near the end
                Color drawColor = p.Color * fade;

                effect.Parameters["uColor"]?.SetValue(drawColor.ToVector3());
                Main.EntitySpriteDraw(pixel, p.Position - Main.screenPosition, null, drawColor, 0f, origin, p.Scale, SpriteEffects.None, 0);
            }

            Main.spriteBatch.End();
        }
    }
}
