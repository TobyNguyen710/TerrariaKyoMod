using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using KyoMod.Content.Projectiles.Magic.BubblingOblivion;

namespace KyoMod.Content.Items.Magic.BubblingOblivion
{
    public abstract class BubblingOblivionBase : ModItem
    {
        // Icon/inventory sprite only — the held-in-world look is fully custom-drawn by BubblingOblivionHeld.
        public override string Texture => "KyoMod/Content/Items/Magic/BubblingOblivion/BubblingOblivion";

        public override void SetDefaults()
        {
            Item.width = Item.height = 48;
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useAnimation = 24;
            Item.useTime = 24;
            Item.useTurn = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.knockBack = 2f;
            Item.shoot = ModContent.ProjectileType<BubblingOblivionBolt>();
            Item.shootSpeed = 11f;
        }

        public override void HoldItem(Player player)
        {
            if (Main.myPlayer != player.whoAmI || player.itemAnimation <= 0)
                return;

            KeepAlive(player, ModContent.ProjectileType<BubblingOblivionHeld>());
            KeepAlive(player, ModContent.ProjectileType<BubblingOblivionOrb>());
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
