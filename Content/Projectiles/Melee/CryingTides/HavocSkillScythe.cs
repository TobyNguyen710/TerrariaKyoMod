using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
    public class HavocSkillScythe : ModProjectile
    {
        // ai[0] = spin direction (1 or -1)
        // ai[1] = texture tier at cast (0 / 1 / 2)
        // ai[2] = sword scale from item

        private int   SpinDir     => (int)Projectile.ai[0];
        private int   TextureTier => (int)Projectile.ai[1];
        private float SwordScale  => Projectile.ai[2] > 0f ? Projectile.ai[2] : 1f;

        private const int   Lifetime  = 25;
        private const float SpinSpeed = 0.45f;
        private const int   FadeFrames = 7; // fade out over last N frames

        public override string Texture => "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheNoStack";

        public override void SetDefaults()
        {
            Projectile.width  = 50;
            Projectile.height = 50;
            Projectile.friendly        = true;
            Projectile.DamageType      = DamageClass.Melee;
            Projectile.penetrate       = -1;
            Projectile.tileCollide     = false;
            Projectile.ignoreWater     = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown  = -1; // hit each NPC once per pass
            Projectile.timeLeft        = Lifetime;
        }

        private Texture2D GetTexture()
        {
            string path = TextureTier >= 2
                ? "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheMaxStack"
                : TextureTier >= 1
                    ? "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheHalfStack"
                    : "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheNoStack";
            return ModContent.Request<Texture2D>(path).Value;
        }

        public override void AI()
        {
            Projectile.rotation += SpinSpeed * SpinDir;

            // Crimson dust trail
            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustDirect(Projectile.Center, 8, 8, DustID.Crimson);
                d.velocity  = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(1f, 3f);
                d.noGravity = true;
                d.scale     = Main.rand.NextFloat(0.6f, 1.2f);
            }

            // Crimson point light
            float glow = TextureTier >= 2 ? 1.0f : TextureTier >= 1 ? 0.55f : 0.25f;
            Lighting.AddLight(Projectile.Center, glow * 0.85f, glow * 0.10f, glow * 0.06f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex    = GetTexture();
            float     alpha  = Projectile.timeLeft < FadeFrames ? Projectile.timeLeft / (float)FadeFrames : 1f;
            var       origin = new Vector2(tex.Width * 0.5f, tex.Height * 0.5f);
            float     scale  = SwordScale * 3f * alpha; // shrinks slightly as it fades

            Main.spriteBatch.Draw(tex,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor * alpha,
                Projectile.rotation,
                origin,
                scale,
                SpriteEffects.None,
                0f);

            return false;
        }
    }
}
