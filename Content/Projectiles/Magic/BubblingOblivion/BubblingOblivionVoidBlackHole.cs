using System;
using KyoMod.Common.Players;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Void form's redesigned ultimate cast, replacing the old radial-bolt-burst placeholder for the
    // Bubble->Void direction specifically (Void->Bubble keeps the bolt burst until a symmetric
    // Bubble-themed version is designed later). User's own spec: "a black hole appear at the cursor,
    // deal linger dmg for a bit like 2s then explode deal 10x attack. a field will be created there
    // (we will do this in detail later)". Spawned directly at the cursor's world position at cast time
    // (BubblingOblivionPlayer.ProcessTriggers) — no travel, it just appears.
    public class BubblingOblivionVoidBlackHole : ModProjectile
    {
        // Never drawn via its own texture — PreDraw fully overrides with VoidVisuals.DrawBlackHole.
        public override string Texture => "KyoMod/Content/Items/Magic/BubblingOblivion/BubblingOblivion";

        private const int LingerFrames = 120; // ~2s, per spec ("deal linger dmg for a bit like 2s")
        private const float LingerRadius = 50f;
        private const int LingerHitCooldown = 20; // ~3 ticks of contact damage over the 2s window

        // "10x attack" — same multiplier convention as BigBubble.PopDamageMultiplier.
        private const float ExplosionDamageMultiplier = 10f;
        private const float ExplosionRadius = 220f;

        private float shaderSeed;
        private int age;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = (int)(LingerRadius * 2f);
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LingerFrames + 2;
            Projectile.light = 0.6f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = LingerHitCooldown;
        }

        public override void OnSpawn(IEntitySource source)
        {
            shaderSeed = Main.rand.NextFloat(100f);

            if (Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/BubblingOblivion/BO_Bubble_BigPop") { Pitch = -0.4f, PitchVariance = 0.1f, Volume = 0.7f }, Projectile.Center);
        }

        public override void AI()
        {
            age++;
            Projectile.velocity = Vector2.Zero;

            // Small pulsing light through the linger window, similar reasoning to BigBubble's own
            // PulseSingularityLight — cyan-tinted here instead of purple to match VoidVisuals' palette.
            float pulse = 0.4f + 0.3f * MathF.Sin(age * 0.35f);
            Lighting.AddLight(Projectile.Center, pulse * 0.1f, pulse * 0.55f, pulse * 0.6f);

            if (age >= LingerFrames)
                Explode();
        }

        private void Explode()
        {
            if (Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/BubblingOblivion/BO_Bubble_BigPop") { PitchVariance = 0.1f, Volume = 0.8f }, Projectile.Center);

            int dmg = (int)(Projectile.damage * ExplosionDamageMultiplier);
            foreach (NPC npc in Main.npc)
            {
                if (!npc.active || npc.friendly)
                    continue;
                if (Vector2.Distance(npc.Center, Projectile.Center) > ExplosionRadius)
                    continue;

                int dir = npc.Center.X >= Projectile.Center.X ? 1 : -1;
                bool crit = Main.rand.Next(100) < Owner.GetCritChance(DamageClass.Magic);
                Owner.ApplyDamageToNPC(npc, dmg, 0f, dir, crit);
                Owner.GetModPlayer<BubblingOblivionPlayer>().AddLiberationEnergy();
            }

            // Field created here — pulse/damage-over-time behavior deliberately left for a later
            // session (user's own words: "we will do this in detail later"). Spawning the stub now so
            // the extension point already exists in code rather than being invented from scratch then.
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<BubblingOblivionVoidField>(), 0, 0f, Projectile.owner);

            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float progress = MathHelper.Clamp(age / (float)LingerFrames, 0f, 1f);
            // Grows in from nothing right as it appears, holds near full size through the linger window.
            float growT = MathHelper.Clamp(progress / 0.15f, 0f, 1f);
            growT = growT * growT * (3f - 2f * growT);
            float radius = LingerRadius * 2.2f * growT;

            VoidVisuals.DrawBlackHole(Projectile.Center, radius, coreRadius01: 0.45f, age / 60f, shaderSeed, opacity: 1f);
            BubblingOblivionDebug.DrawHitbox(Projectile, Color.Cyan * 0.4f);
            return false;
        }
    }
}
