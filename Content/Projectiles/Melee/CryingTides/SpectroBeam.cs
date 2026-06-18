using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
    public class SpectroBeam : ModProjectile
    {
        private const int   Lifetime    = 45;
        private const int   FadeFrames  = 8;
        private const int   ArcSegments = 20;
        private const float ArcHalf     = MathHelper.Pi * 70f / 180f; // 70° each side = 140° total
        private const float ArcRadius   = 38f;
        private const float MaxThick    = 5.5f; // thickest point at the crescent's crown

        public override string Texture => "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheNoStack";

        public override void SetDefaults()
        {
            Projectile.width       = 40;
            Projectile.height      = 40;
            Projectile.friendly    = true;
            Projectile.DamageType  = DamageClass.Melee;
            Projectile.penetrate            = 3;
            Projectile.tileCollide          = true;
            Projectile.ignoreWater          = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown  = 10;
            Projectile.timeLeft             = Lifetime;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // Golden dust trail
            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(Projectile.Center, 4, 4, DustID.GoldFlame);
                d.velocity  = -Projectile.velocity * 0.15f
                            + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(0.5f);
                d.noGravity = true;
                d.scale     = Main.rand.NextFloat(0.5f, 0.9f);
            }

            Lighting.AddLight(Projectile.Center, 0.9f, 0.65f, 0.1f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float alpha = Projectile.timeLeft < FadeFrames ? Projectile.timeLeft / (float)FadeFrames : 1f;

            Texture2D pixel    = TextureAssets.MagicPixel.Value;
            var       src      = new Rectangle(0, 0, 1, 1);
            float     travel   = Projectile.rotation;
            Vector2   travelDir = travel.ToRotationVector2();

            // Circle centre sits ArcRadius behind the projectile so the crown lands on Projectile.Center
            Vector2 arcCenter = Projectile.Center - Main.screenPosition - travelDir * ArcRadius;

            // AlphaBlend pass — solid golden crescent
            DrawArc(pixel, src, arcCenter, travel, alpha,
                edgeColor:   new Color(220, 150, 20),
                centerColor: new Color(255, 240, 160),
                maxThick:    MaxThick);

            // Additive pass — soft outer glow
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            DrawArc(pixel, src, arcCenter, travel, alpha * 0.45f,
                edgeColor:   new Color(255, 160, 20),
                centerColor: Color.White,
                maxThick:    MaxThick * 2.2f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        private static void DrawArc(Texture2D pixel, Rectangle src, Vector2 arcCenter,
            float travelAngle, float alpha, Color edgeColor, Color centerColor, float maxThick)
        {
            var segOrigin = new Vector2(0f, 0.5f);

            for (int i = 0; i < ArcSegments; i++)
            {
                float t0  = i       / (float)ArcSegments;
                float t1  = (i + 1) / (float)ArcSegments;
                float mid = (t0 + t1) * 0.5f;

                // Taper to a point at both tips; thickest at the crown (mid = 0.5)
                float taper = 1f - Math.Abs(mid - 0.5f) * 2f;
                float thick = maxThick * taper;
                if (thick < 0.4f) continue;

                float   a0 = travelAngle - ArcHalf + t0 * ArcHalf * 2f;
                float   a1 = travelAngle - ArcHalf + t1 * ArcHalf * 2f;
                Vector2 p0 = arcCenter + a0.ToRotationVector2() * ArcRadius;
                Vector2 p1 = arcCenter + a1.ToRotationVector2() * ArcRadius;

                Vector2 seg    = p1 - p0;
                float   segLen = seg.Length();
                float   segAng = seg.ToRotation();

                Color col = Color.Lerp(edgeColor, centerColor, taper) * alpha;

                Main.spriteBatch.Draw(pixel, p0, src, col, segAng, segOrigin,
                    new Vector2(segLen + 1f, thick), SpriteEffects.None, 0f);
            }
        }
    }
}
