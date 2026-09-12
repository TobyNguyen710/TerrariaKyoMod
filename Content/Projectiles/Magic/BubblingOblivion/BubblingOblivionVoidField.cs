using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Stub — spawned by BubblingOblivionVoidBlackHole.Explode() at the black hole's location. Deliberate
    // placeholder: user's own words, "a field will be created there, which will release pulses and deal
    // dmg over a period of time... we will do this in detail later." This exists so the spawn point/
    // extension seam is already wired into the black hole's own lifecycle, rather than needing to be
    // threaded through that code again once the field's actual pulse/damage design is decided. No
    // damage, no persistent visual yet — just holds its position and expires.
    public class BubblingOblivionVoidField : ModProjectile
    {
        public override string Texture => "KyoMod/Content/Items/Magic/BubblingOblivion/BubblingOblivion";

        private const int Lifetime = 180; // 3s placeholder — not a designed duration yet

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.ignoreWater = true;
        }

        public override void OnSpawn(IEntitySource source)
        {
            Projectile.velocity = Vector2.Zero;
        }

        public override bool PreDraw(ref Color lightColor) => false; // no visual yet
    }
}
