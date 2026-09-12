using Terraria;
using Terraria.ID;

namespace KyoMod.Content.Items.Magic.BubblingOblivion
{
    public class BubblingOblivionPostEvilBoss : BubblingOblivionBase
    {
        public override string Texture => "KyoMod/Content/Items/Magic/BubblingOblivion/BubblingOblivion";
        public override bool HasUltimate => true;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.damage = 48;
            Item.crit = 15;
            Item.mana = 8;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 15);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<BubblingOblivionPostEoC>()
                .AddIngredient(ItemID.TissueSample, 20)
                .AddTile(TileID.Anvils)
                .Register();
            CreateRecipe()
                .AddIngredient<BubblingOblivionPostEoC>()
                .AddIngredient(ItemID.ShadowScale, 20)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
