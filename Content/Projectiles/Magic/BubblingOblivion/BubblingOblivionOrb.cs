using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using KyoMod.Content.Items.Magic.BubblingOblivion;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Purely decorative bubble that sits at the tip of the held no-bubble staff sprite and spins in place.
    public class BubblingOblivionOrb : ModProjectile
    {
        public override string Texture => "KyoMod/Content/Projectiles/Magic/BubblingOblivion/BubblingOblivionBolt";

        // Pixel [36,10] on the 48x48 no-bubble sprite, relative to BubblingOblivionHeld's grip pivot —
        // derived rather than hardcoded so retuning the grip automatically keeps the bubble attached.
        private static readonly Vector2 TipPixel = new Vector2(52.5f, 10.5f);
        private static Vector2 TipOffset => TipPixel - BubblingOblivionHeld.GripOrigin;
        private const float SpinSpeed = 0.02f;

        // Per-instance shimmer seed + running age for BubbleIridescent.fx. Set in OnSpawn rather than
        // a field initializer — ModProjectile clones via MemberwiseClone, so a field initializer would
        // only ever run once (on the template) and every spawned orb would share the same seed.
        private float shaderSeed;
        private int age;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 6;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3;
            Projectile.ignoreWater = true;
            Projectile.light = 0.3f;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            shaderSeed = Main.rand.NextFloat(100f);
        }

        private BubblingOblivionHeld FindHeld()
        {
            int heldType = ModContent.ProjectileType<BubblingOblivionHeld>();
            foreach (Projectile p in Main.projectile)
            {
                if (p.active && p.owner == Projectile.owner && p.type == heldType && p.ModProjectile is BubblingOblivionHeld held)
                    return held;
            }
            return null;
        }

        public override void AI()
        {
            if (!Owner.active || Owner.dead || Owner.HeldItem.ModItem is not BubblingOblivionBase)
            {
                Projectile.Kill();
                return;
            }

            BubblingOblivionHeld held = FindHeld();
            if (held == null)
            {
                Projectile.Kill();
                return;
            }

            Vector2 localOffset = new Vector2(TipOffset.X * held.LockedDirection, TipOffset.Y) * BubblingOblivionHeld.DrawScale;
            Projectile.Center = held.Projectile.Center + localOffset.RotatedBy(held.DrawRotation);
            Projectile.rotation += SpinSpeed;
            Projectile.scale = BubblingOblivionHeld.DrawScale;
            age++;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 origin = new Vector2(tex.Width, tex.Height) * 0.5f;
            BubblingOblivionVisuals.DrawSwirl(tex, Projectile.Center - Main.screenPosition, lightColor, Projectile.rotation,
                origin, Projectile.scale, age / 60f, shaderSeed);
            return false;
        }
    }
}
