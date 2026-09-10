using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Purely cosmetic — an expanding-then-fading marbled ring (BubblingOblivionMeshEffects.DrawPopShockwave)
    // spawned by BigBubble.Pop() at the exact moment of popping. Lives on for its own short animation
    // after the big bubble itself has already been Kill()ed, since a shockwave needs several frames to
    // play out and the bubble can't stick around to draw it itself. Same "a real cast moment, not just
    // a particle burst" reasoning as the impact-star flash (design doc §12).
    public class BubblingOblivionPopShockwave : ModProjectile
    {
        // Never actually drawn via its own texture (PreDraw fully overrides) — reusing an existing
        // asset here just satisfies ModProjectile's texture requirement without shipping a dummy file.
        public override string Texture => "KyoMod/Content/Projectiles/Magic/BubblingOblivion/BubblingOblivionBubble4";

        private const int Lifetime = 24;

        // Outer radius the ring expands to — passed in as ai[0] at spawn (BigBubble.Pop() passes its
        // own PopRadius), same convention as the other Bubble-form projectiles' ai-field spawn data.
        private float MaxRadius => Projectile.ai[0];

        private float shaderSeed;
        private int age;

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
            shaderSeed = Main.rand.NextFloat(100f);
        }

        public override void AI()
        {
            age++;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float progress = age / (float)Lifetime;
            BubblingOblivionMeshEffects.DrawPopShockwave(Projectile.Center, MaxRadius, progress, age / 60f, shaderSeed);
            return false;
        }
    }
}
