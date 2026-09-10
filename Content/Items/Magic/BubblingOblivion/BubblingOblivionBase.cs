using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using KyoMod.Content.Projectiles.Magic.BubblingOblivion;

namespace KyoMod.Content.Items.Magic.BubblingOblivion
{
    public abstract class BubblingOblivionBase : ModItem
    {
        // Icon/inventory sprite only — the held-in-world look is fully custom-drawn by BubblingOblivionHeld.
        public override string Texture => "KyoMod/Content/Items/Magic/BubblingOblivion/BubblingOblivion";

        public int Combo = 0;
        private int comboIdleTimer = 0;
        private const int ComboResetFrames = 140; // was 120 — idle window before the combo resets

        private const float ComboSpreadAngle = 8f; // degrees, combo 2's twin shots
        private const int SecondBoltDelay = 6; // ~0.1s at 60 ticks/sec

        private int pendingBoltTimer = 0;
        private Vector2 pendingPosition;
        private Vector2 pendingVelocity;
        private int pendingDamage;
        private float pendingKnockback;

        public override void SetDefaults()
        {
            Item.width = Item.height = 48;
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useAnimation = 24;
            Item.useTime = 35;
            Item.useTurn = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.knockBack = 2f;
            Item.shoot = ModContent.ProjectileType<BubblingOblivionBolt>();
            Item.shootSpeed = 7f; // slower than Void form's (future) piercing bolt
        }

        public override void HoldItem(Player player)
        {
            if (Main.myPlayer != player.whoAmI)
                return;

            if (pendingBoltTimer > 0)
            {
                pendingBoltTimer--;
                if (pendingBoltTimer <= 0)
                {
                    Vector2 spawnPos = FindHeldPosition(player) ?? pendingPosition;
                    Projectile.NewProjectile(player.GetSource_ItemUse(Item), spawnPos, pendingVelocity,
                        ModContent.ProjectileType<BubblingOblivionBolt>(), pendingDamage, pendingKnockback, player.whoAmI, 2f, 4f);
                }
            }

            // Keep the held-staff visuals alive through the idle gap between autoReuse swings, not just
            // while itemAnimation > 0. useTime (35) is longer than useAnimation (24), so there's an
            // ~11-tick window between swings where itemAnimation is already 0 but the mouse is still held
            // down and another swing is about to start; gating KeepAlive purely on itemAnimation let
            // BubblingOblivionHeld's short timeLeft (3) expire during that window. When the next swing
            // then started, vanilla resets itemAnimation (and HoldItem runs KeepAlive again) BEFORE
            // Shoot() fires this same tick, so KeepAlive had to spawn a brand-new Held projectile whose
            // position hadn't been updated by its own AI() yet — Shoot() read that stale, un-offset
            // Center as the spawn point, producing an intermittent "bolt spawns slightly above the
            // player" bug that was hard to reproduce on purpose.
            //
            // Gating on controlUseItem (mouse still held) rather than "item is merely selected" matters —
            // the latter kept the staff visible even standing idle without clicking at all, which isn't
            // what "held in hand" should mean here.
            bool activelyUsing = player.itemAnimation > 0 || player.controlUseItem;
            if (activelyUsing)
            {
                KeepAlive(player, ModContent.ProjectileType<BubblingOblivionHeld>());
                KeepAlive(player, ModContent.ProjectileType<BubblingOblivionOrb>());
            }

            if (player.itemAnimation <= 0)
            {
                if (++comboIdleTimer >= ComboResetFrames)
                {
                    Combo = 0;
                    comboIdleTimer = 0;
                }
                return;
            }

            comboIdleTimer = 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Vanilla's own `position` here comes from itemLocation/aim math that (per BubblingOblivionHeld's
            // earlier investigation) isn't reliably tracked for a plain holdStyle-0 item and occasionally
            // returns a stale/wrong value — spawn from the actual held staff visual instead, since that's
            // a position we fully control and know is always sane (it's what's drawn on screen).
            Vector2 spawnPosition = FindHeldPosition(player) ?? position;

            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX * player.direction);
            float speed = velocity.Length();
            if (speed <= 0f)
                speed = Item.shootSpeed;

            string comboSound = Combo switch
            {
                0 => "BO_Bubble_Combo1",
                1 => "BO_Bubble_Combo2",
                _ => "BO_Bubble_Combo3",
            };
            SoundEngine.PlaySound(new SoundStyle($"KyoMod/Sounds/BubblingOblivion/{comboSound}") { PitchVariance = 0.15f }, spawnPosition);

            switch (Combo)
            {
                case 0: // combo 1: single bubble3, scaled 4x
                    Projectile.NewProjectile(source, spawnPosition, dir * speed,
                        ModContent.ProjectileType<BubblingOblivionBolt>(), damage, knockback, player.whoAmI, 3f, 4f);
                    break;

                case 1: // combo 2: two bubble2 shots, scaled 4x, small spread, second one fires ~0.1s later
                    float spread = MathHelper.ToRadians(ComboSpreadAngle);
                    Projectile.NewProjectile(source, spawnPosition, dir.RotatedBy(-spread) * speed,
                        ModContent.ProjectileType<BubblingOblivionBolt>(), damage, knockback, player.whoAmI, 2f, 4f);

                    pendingPosition = spawnPosition;
                    pendingVelocity = dir.RotatedBy(spread) * speed;
                    pendingDamage = damage;
                    pendingKnockback = knockback;
                    pendingBoltTimer = SecondBoltDelay;
                    break;

                default: // combo 3: big lingering bubble4, scaled 4x — travels to a snapshot of the
                         // cursor's world position at cast time, instead of a fixed distance.
                    int bigIndex = Projectile.NewProjectile(source, spawnPosition, dir * speed,
                        ModContent.ProjectileType<BubblingOblivionBigBubble>(), damage, knockback, player.whoAmI);
                    if (Main.projectile[bigIndex].ModProjectile is BubblingOblivionBigBubble bigBubble)
                        bigBubble.TargetDistance = Vector2.Distance(spawnPosition, Main.MouseWorld);
                    break;
            }

            Combo = (Combo + 1) % 3;
            comboIdleTimer = 0;
            return false;
        }

        private static Vector2? FindHeldPosition(Player player)
        {
            int heldType = ModContent.ProjectileType<BubblingOblivionHeld>();
            foreach (Projectile p in Main.projectile)
            {
                if (p.active && p.owner == player.whoAmI && p.type == heldType)
                    return p.Center;
            }
            return null;
        }

        private static void KeepAlive(Player player, int projType)
        {
            foreach (Projectile p in Main.projectile)
            {
                if (p.active && p.owner == player.whoAmI && p.type == projType)
                {
                    p.timeLeft = 3;
                    return;
                }
            }

            Projectile.NewProjectile(player.GetSource_Misc("BubblingOblivionHold"), player.Center, Vector2.Zero, projType, 0, 0f, player.whoAmI);
        }
    }
}
