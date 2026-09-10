using Terraria;
using Terraria.ID;

namespace KyoMod.Content.Items.Magic.BubblingOblivion
{
    public class BubblingOblivion : BubblingOblivionBase
    {
        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.damage = 20;
            Item.crit = 5; // matches CryingTides.cs's own pre-boss tier crit
            Item.mana = 5; // may scale up at later tiers — not decided yet
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
