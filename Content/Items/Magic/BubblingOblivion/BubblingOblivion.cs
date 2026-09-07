using Terraria;
using Terraria.ID;

namespace KyoMod.Content.Items.Magic.BubblingOblivion
{
    public class BubblingOblivion : BubblingOblivionBase
    {
        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.damage = 14;
            Item.crit = 4;
            Item.mana = 10;
            Item.rare = ItemRarityID.White;
            Item.value = Item.buyPrice(silver: 50);
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
