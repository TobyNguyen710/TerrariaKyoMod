using System.Collections.Generic;
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

        // Per-instance shimmer seed + age (ticks) for BubbleSwirl.fx, so a volley of bolts doesn't
        // shimmer in lockstep.
        private float shaderSeed;
        private int age;

        // Recent Center positions (oldest first), sampled once per AI tick — feeds the trail ribbon
        // drawn by BubblingOblivionMeshEffects.DrawTrail, independent of the bolt's own sprite.
        private const int TrailLength = 14;
        private const float TrailMaxWidth = 10f;
        private readonly List<Vector2> trailHistory = new();

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
            shaderSeed = Main.rand.NextFloat(100f);
        }

        public override void AI()
        {
            age++;

            NPC target = BubblingOblivionHoming.FindTarget(Projectile.Center);
            if (target != null)
            {
                Projectile.velocity = BubblingOblivionHoming.SteerToward(Projectile.velocity, target.Center - Projectile.Center);
            }

            Projectile.rotation += rotationSpeed;

            trailHistory.Add(Projectile.Center);
            if (trailHistory.Count > TrailLength)
                trailHistory.RemoveAt(0);

            if (Main.rand.NextBool(3))
                BubblingOblivionParticleSystem.SpawnInRect(Projectile.Hitbox, BubblingOblivionVisuals.AmbientParticleColor, BubblingOblivionVisuals.ParticleScale, 20);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Drawn first so it sits behind the bolt's own sprite.
            BubblingOblivionMeshEffects.DrawTrail(trailHistory, TrailMaxWidth * Projectile.scale, age / 60f, shaderSeed);

            Texture2D tex = ModContent.Request<Texture2D>($"KyoMod/Content/Projectiles/Magic/BubblingOblivion/BubblingOblivionBubble{Variant}").Value;
            Vector2 origin = new Vector2(tex.Width, tex.Height) * 0.5f;
            BubblingOblivionVisuals.DrawSwirl(tex, Projectile.Center - Main.screenPosition, lightColor, Projectile.rotation,
                origin, Projectile.scale, age / 60f, shaderSeed);
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
                BubblingOblivionParticleSystem.SpawnInRect(Projectile.Hitbox, BubblingOblivionVisuals.AmbientParticleColor, BubblingOblivionVisuals.ParticleScale, 24);
        }
    }
}
