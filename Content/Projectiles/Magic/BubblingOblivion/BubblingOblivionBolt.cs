using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Temp projectile: straight-line bolt, no special behavior yet.
    public class BubblingOblivionBolt : ModProjectile
    {
        public override string Texture => "KyoMod/Content/Projectiles/Magic/BubblingOblivion/BubblingOblivionBolt";

        public override void SetDefaults()
        {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.light = 0.4f;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(3))
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0f, 0f, 100, default, 0.8f);
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0f, 0f, 100, default, 1.2f);
        }
    }
}
