using Terraria;
using Terraria.ID;

namespace KyoMod.Content.Items.Magic.BubblingOblivion
{
    public class BubblingOblivionPostEoC : BubblingOblivionBase
    {
        public override string Texture => "KyoMod/Content/Items/Magic/BubblingOblivion/BubblingOblivion";

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.damage = 32;
            Item.crit = 10;
            Item.mana = 6;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(gold: 2, silver: 50);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<BubblingOblivion>()
                .AddIngredient(ItemID.CrimtaneBar, 12)
                .AddTile(TileID.Anvils)
                .Register();
            CreateRecipe()
                .AddIngredient<BubblingOblivion>()
                .AddIngredient(ItemID.DemoniteBar, 12)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
