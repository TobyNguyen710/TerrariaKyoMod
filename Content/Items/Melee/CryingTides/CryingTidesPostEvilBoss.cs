using Terraria;
using Terraria.ID;

namespace KyoMod.Content.Items.Melee.CryingTides
{
	public class CryingTidesPostEvilBoss : CryingTidesBase
	{
		public override string Texture => "KyoMod/Content/Items/Melee/CryingTides/CryingTides";
		protected override bool HasSkill => true;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 145;
			Item.rare = ItemRarityID.Orange;
			Item.value = Item.buyPrice(gold: 15);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<CryingTidesPostEoC>()
				.AddIngredient(ItemID.TissueSample, 20)
				.AddTile(TileID.Anvils)
				.Register();
			CreateRecipe()
				.AddIngredient<CryingTidesPostEoC>()
				.AddIngredient(ItemID.ShadowScale, 20)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
