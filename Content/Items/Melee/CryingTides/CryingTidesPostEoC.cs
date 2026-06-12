using Terraria;
using Terraria.ID;

namespace KyoMod.Content.Items.Melee.CryingTides
{
	public class CryingTidesPostEoC : CryingTidesBase
	{
		public override string Texture => "KyoMod/Content/Items/Melee/CryingTides/CryingTides";
		protected override float SwordScale => 1.0f;
		protected override float SwordArc => 1.1f;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 135;
			Item.rare = ItemRarityID.Green;
			Item.value = Item.buyPrice(gold: 5);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<CryingTides>()
				.AddIngredient(ItemID.CrimtaneBar, 12)
				.AddTile(TileID.Anvils)
				.Register();
			CreateRecipe()
				.AddIngredient<CryingTides>()
				.AddIngredient(ItemID.DemoniteBar, 12)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
