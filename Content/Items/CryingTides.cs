using System.Linq;
using KyoMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Items
{
	public class CryingTides : ModItem
	{
		public int Combo = 0;

		public override void SetDefaults()
		{
			Item.width = Item.height = 60;
			Item.damage = 80;
			Item.DamageType = DamageClass.Melee;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.useAnimation = 35;
			Item.useTime = 35;
			Item.useTurn = true;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.knockBack = 7f;
			Item.UseSound = SoundID.Item1;
			Item.autoReuse = true;
			Item.value = Item.buyPrice(gold: 1);
			Item.rare = ItemRarityID.Blue;
			Item.shoot = ProjectileID.PurificationPowder;
			Item.shootSpeed = 12f;
		}

		public override bool CanUseItem(Player player)
		{
			return !Main.projectile.Any(p => p.active && p.owner == player.whoAmI && p.type == ModContent.ProjectileType<CryingTidesSwungBlade>());
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			Projectile.NewProjectile(source, player.Center, velocity, ModContent.ProjectileType<CryingTidesSwungBlade>(), damage, knockback, player.whoAmI, Combo);
			Combo = (Combo + 1) % 3;
			return false;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.DirtBlock, 10)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
