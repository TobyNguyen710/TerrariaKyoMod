using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Shared Bubble-form drawing helper. DrawSwirl wraps a normal sprite draw with BubbleSwirl.fx (see
    // docs/Shaders/BubbleSwirl.fx and design doc §4) — falls back to a plain EntitySpriteDraw if the
    // shader failed to load (e.g. a GraphicsProfile that can't run it), same defensive pattern as
    // SlashArcEffect's SlashInkShader null check. Used on all three Bubble-form sprites: regular
    // bolts, the big bubble, and the held-tip orb.
    internal static class BubblingOblivionVisuals
    {
        // 8-stop gradient palette for the marbled swirl — the user's own literal hex codes, given
        // directly as "make it a mixture of these" after two earlier 3-color attempts (a near-white
        // pastel trio that washed out, then a hand-picked saturated trio that was too loud). Applied
        // via ApplyPalette to both BubbleSwirl.fx (sprites) and BubbleTrail.fx (the bolt trail ribbon)
        // so both read as the same substance.
        public static readonly Vector3 Color0 = new Vector3(0.988f, 0.616f, 1f);     // fc9dff
        public static readonly Vector3 Color1 = new Vector3(0.957f, 0.976f, 0.988f); // f4f9fc
        public static readonly Vector3 Color2 = new Vector3(0.882f, 0.996f, 0.988f); // e1fefc
        public static readonly Vector3 Color3 = new Vector3(0.902f, 0.902f, 0.902f); // e6e6e6
        public static readonly Vector3 Color4 = new Vector3(0.808f, 0.886f, 0.918f); // cee2ea
        public static readonly Vector3 Color5 = new Vector3(0.875f, 1f, 0.996f);     // dffffe
        public static readonly Vector3 Color6 = new Vector3(0.471f, 0.918f, 0.922f); // 78eaeb
        public static readonly Vector3 Color7 = new Vector3(0.8f, 0.188f, 0.682f);   // cc30ae

        // Particle tint colors for BubblingOblivionParticleSystem, derived from the same palette above.
        // Unlike vanilla Dust (see that system's own header comment), these actually reproduce the
        // palette accurately — BubbleParticle.fx has no texture/base-color multiply to fight against.
        public static readonly Color AmbientParticleColor = new Color(Color6.X, Color6.Y, Color6.Z); // 78eaeb — ordinary flight/idle sparkle
        public static readonly Color CollapseParticleColor = new Color(Color7.X * 0.65f, Color7.Y * 0.65f, Color7.Z * 0.65f); // darkened cc30ae — shrink/collapse

        private static readonly Color[] Palette =
        {
            new Color(Color0.X, Color0.Y, Color0.Z), new Color(Color1.X, Color1.Y, Color1.Z),
            new Color(Color2.X, Color2.Y, Color2.Z), new Color(Color3.X, Color3.Y, Color3.Z),
            new Color(Color4.X, Color4.Y, Color4.Z), new Color(Color5.X, Color5.Y, Color5.Z),
            new Color(Color6.X, Color6.Y, Color6.Z), new Color(Color7.X, Color7.Y, Color7.Z),
        };

        // A single flat color can't match a marbled multi-color shockwave ring no matter which one
        // stop is picked (tried the "strongest" stop, cc30ae — still read as mismatched, since the
        // marble's noise rarely swings to that extreme in practice). Instead, each pop particle picks
        // a random stop from the same 8-color palette the shockwave marbles through, so the burst as a
        // whole spans the same color range as the ring instead of being one flat tone.
        public static Color RandomPopColor() => Palette[Main.rand.Next(Palette.Length)];

        // Shared particle draw size across every Bubble-form projectile — a single source of truth so
        // the bolts and the big bubble's particles read as the same size, instead of each projectile
        // scaling its own particles by its own Projectile.scale (which made the bolts' particles look
        // much bigger than the big bubble's, despite the bubble being the visually larger object).
        public const float ParticleScale = 5f;

        public static void ApplyPalette(Effect effect)
        {
            effect.Parameters["uColor0"]?.SetValue(Color0);
            effect.Parameters["uColor1"]?.SetValue(Color1);
            effect.Parameters["uColor2"]?.SetValue(Color2);
            effect.Parameters["uColor3"]?.SetValue(Color3);
            effect.Parameters["uColor4"]?.SetValue(Color4);
            effect.Parameters["uColor5"]?.SetValue(Color5);
            effect.Parameters["uColor6"]?.SetValue(Color6);
            effect.Parameters["uColor7"]?.SetValue(Color7);
        }

        public static void DrawSwirl(Texture2D texture, Vector2 drawPos, Color lightColor, float rotation,
            Vector2 origin, float scale, float ageSeconds, float seed,
            float swirlScale = 6f, float mixStrength = 0.6f)
        {
            Effect effect = KyoMod.BubbleSwirlShader?.Value;
            if (effect == null)
            {
                Main.EntitySpriteDraw(texture, drawPos, null, lightColor, rotation, origin, scale, SpriteEffects.None, 0);
                return;
            }

            effect.Parameters["uTime"]?.SetValue(ageSeconds);
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uSwirlScale"]?.SetValue(swirlScale);
            effect.Parameters["uMixStrength"]?.SetValue(mixStrength);
            ApplyPalette(effect);

            // Pixel-shader-only effect (see the .fx file's own header comment) — SpriteBatch's own
            // default vertex processing stays bound, so a plain Begin/End swap around the draw is
            // all this needs, no custom mesh/viewProj like SlashArcEffect's ribbon.
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, effect, Main.GameViewMatrix.TransformationMatrix);
            Main.EntitySpriteDraw(texture, drawPos, null, lightColor, rotation, origin, scale, SpriteEffects.None, 0);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
