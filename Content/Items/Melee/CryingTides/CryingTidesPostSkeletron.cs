using Terraria;
using Terraria.ID;

namespace KyoMod.Content.Items.Melee.CryingTides
{
	public class CryingTidesPostSkeletron : CryingTidesBase
	{
		public override string Texture => "KyoMod/Content/Items/Melee/CryingTides/CryingTides";
		public override bool HasSkill => true;
		public override float SwordScale => 1.0f;
		public override float SwordArc => 1.45f;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 135;
			Item.crit = 16;
			Item.rare = ItemRarityID.Orange;
			Item.value = Item.buyPrice(gold: 17);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<CryingTidesPostEvilBoss>()
				.AddIngredient(ItemID.Bone, 30)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
