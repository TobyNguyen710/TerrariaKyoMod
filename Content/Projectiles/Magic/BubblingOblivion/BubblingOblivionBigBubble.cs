using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Bubble form's combo-3 special: travels forward a short distance, then plants in place as a
    // lingering, poppable bubble. Popping (currently: a regular bolt touching it — this trigger is
    // explicitly provisional, see design doc) deals a burst of bonus damage. Other big bubbles never
    // pop this one, so multiple can coexist.
    public class BubblingOblivionBigBubble : ModProjectile
    {
        public override string Texture => "KyoMod/Content/Projectiles/Magic/BubblingOblivion/BubblingOblivionBubble4";

        private const int BaseSize = 24;
        private const float ScaleFactor = 8f;
        private const float MaxTargetDistance = 1000f; // safety cap if the cursor snapshot is very far away
        private const int LingerTime = 420; // 7 seconds (was 180 / 3s)
        private const float PopRadius = BaseSize * ScaleFactor; // matches the lingering bubble's own size (192)
        private const float PopDamageMultiplier = 5f;

        // Independent slow spin, randomized per-instance.
        private float rotationSpeed;

        private SoundStyle? savedNpcHitSound;

        private bool Planted
        {
            get => Projectile.ai[0] == 1f;
            set => Projectile.ai[0] = value ? 1f : 0f;
        }

        private float Traveled
        {
            get => Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        // Distance to the cursor's world position at the moment combo 3 was cast — set by the item right
        // after spawning (see BubblingOblivionBase.Shoot). It travels there and plants, rather than
        // flying a fixed distance.
        public float TargetDistance
        {
            get => Projectile.localAI[0];
            set => Projectile.localAI[0] = MathHelper.Clamp(value, 0f, MaxTargetDistance);
        }

        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults()
        {
            // Native (unscaled) size — vanilla's Projectile.SetDefaults_End automatically multiplies
            // width/height by Projectile.scale once, right after this method returns. Pre-multiplying
            // here too double-applies the scale (was producing a 1536x1536 hitbox: 192 * 8).
            Projectile.width = Projectile.height = BaseSize;
            Projectile.scale = ScaleFactor;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false; // passes through blocks entirely, travel or planted
            Projectile.ignoreWater = true;
            Projectile.light = 0.6f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            rotationSpeed = Main.rand.NextFloat(0.0075f, 0.00875f) * (Main.rand.NextBool() ? 1f : -1f);
        }

        public override void AI()
        {
            Projectile.rotation += rotationSpeed;

            if (!Planted)
            {
                Traveled += Projectile.velocity.Length();

                if (Traveled >= TargetDistance)
                    Plant();

                if (Main.rand.NextBool(2))
                    Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0f, 0f, 100, default, 1.5f);
                return;
            }

            Projectile.velocity = Vector2.Zero;
            if (Main.rand.NextBool(6))
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0f, 0f, 150, default, 1.2f);

            CheckForPop();
        }

        public override void OnKill(int timeLeft)
        {
            if (!Planted)
                Plant();
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

        private void Plant()
        {
            Planted = true;
            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = LingerTime;
            Projectile.netUpdate = true;
        }

        // Only regular bolts pop a planted bubble — two big bubbles never pop each other, so several can
        // be placed to coexist and stack up area coverage (synergizes with Recall, which wants multiple
        // bubbles to be out at once) instead of the newest one just clearing out the others.
        private void CheckForPop()
        {
            int boltType = ModContent.ProjectileType<BubblingOblivionBolt>();
            Rectangle myRect = Projectile.Hitbox;

            foreach (Projectile p in Main.projectile)
            {
                if (!p.active || p.whoAmI == Projectile.whoAmI || p.owner != Projectile.owner)
                    continue;
                if (p.type != boltType)
                    continue;
                if (!myRect.Intersects(p.Hitbox))
                    continue;

                p.Kill();
                Pop();
                return;
            }
        }

        private void Pop()
        {
            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/BubblingOblivion/BO_Bubble_BigPop") { PitchVariance = 0.1f, Volume = 0.7f }, Projectile.Center);
                for (int i = 0; i < 20; i++)
                    Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0f, 0f, 100, default, 2f);
            }

            int dmg = (int)(Projectile.damage * PopDamageMultiplier);
            foreach (NPC npc in Main.npc)
            {
                if (!npc.active || npc.friendly)
                    continue;
                if (Vector2.Distance(npc.Center, Projectile.Center) > PopRadius)
                    continue;

                int dir = npc.Center.X >= Projectile.Center.X ? 1 : -1;
                bool crit = Main.rand.Next(100) < Owner.GetCritChance(DamageClass.Magic);
                Owner.ApplyDamageToNPC(npc, dmg, 0f, dir, crit);
            }

            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 origin = new Vector2(tex.Width, tex.Height) * 0.5f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            BubblingOblivionDebug.DrawHitbox(Projectile, Color.Red * 0.4f);
            return false;
        }
    }
}
