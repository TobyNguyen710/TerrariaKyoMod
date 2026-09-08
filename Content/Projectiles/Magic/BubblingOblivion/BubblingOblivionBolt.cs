using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Bubble form's regular bolt (combo hits 1-2) — a slow, homing bubble shot.
    // ai[0] = bubble sprite variant (2 or 3), ai[1] = visual/hitbox scale multiplier.
    public class BubblingOblivionBolt : ModProjectile
    {
        public override string Texture => "KyoMod/Content/Projectiles/Magic/BubblingOblivion/BubblingOblivionBubble3";

        private int Variant => Projectile.ai[0] >= 1f ? (int)Projectile.ai[0] : 3;
        private float VisualScale => Projectile.ai[1] > 0f ? Projectile.ai[1] : 1f;

        // Native pixel size of each bubble sprite variant — must match the actual .png dimensions so the
        // hitbox lines up with what's drawn (bubble2.png is 10x10, bubble3.png is 14x14).
        private int NativeSize => Variant switch
        {
            2 => 10,
            3 => 14,
            _ => 6,
        };

        // Independent slow spin, randomized per-instance so a volley of bubbles doesn't spin in lockstep.
        private float rotationSpeed;

        private SoundStyle? savedNpcHitSound;

        public override void SetDefaults()
        {
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false; // bubbles ignore terrain, same as BigBubble
            Projectile.ignoreWater = true;
            Projectile.light = 0.4f;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            float scale = VisualScale;
            Projectile.scale = scale;
            Projectile.width = Projectile.height = (int)(NativeSize * scale);
            rotationSpeed = Main.rand.NextFloat(0.01f, 0.0125f) * (Main.rand.NextBool() ? 1f : -1f);
        }

        public override void AI()
        {
            NPC target = BubblingOblivionHoming.FindTarget(Projectile.Center);
            if (target != null)
            {
                Projectile.velocity = BubblingOblivionHoming.SteerToward(Projectile.velocity, target.Center - Projectile.Center);
            }

            Projectile.rotation += rotationSpeed;

            if (Main.rand.NextBool(3))
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0f, 0f, 100, default, 0.8f * Projectile.scale);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>($"KyoMod/Content/Projectiles/Magic/BubblingOblivion/BubblingOblivionBubble{Variant}").Value;
            Vector2 origin = new Vector2(tex.Width, tex.Height) * 0.5f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            BubblingOblivionDebug.DrawHitbox(Projectile, Color.Lime * 0.4f);
            return false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            savedNpcHitSound = target.HitSound;
            target.HitSound = null;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.HitSound = savedNpcHitSound;

            if (Main.netMode == NetmodeID.Server)
                return;

            SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/BubblingOblivion/BO_Bubble_Hit") { PitchVariance = 0.3f, Volume = 0.5f }, target.Center);
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0f, 0f, 100, default, 1.2f * Projectile.scale);
        }
    }
}
