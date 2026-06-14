using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Items.Melee.CryingTides
{
    public class CryingTidesPostWoF : CryingTidesBase
    {
        public override string Texture => "KyoMod/Content/Items/Melee/CryingTides/CryingTides";
        protected override bool HasSkill => true;
        public override bool HasHavocForm => true;
        protected override float SwordScale => ActiveForm == 1 ? 2f : 1.0f;
        protected override float SwordArc => ActiveForm == 1 ? 1.7f : 1.45f;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.damage = 180;
            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.buyPrice(gold: 20);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<CryingTidesPostEvilBoss>()
                .AddIngredient(ItemID.CobaltBar, 12)
                .AddTile(TileID.Anvils)
                .Register();
            CreateRecipe()
                .AddIngredient<CryingTidesPostEvilBoss>()
                .AddIngredient(ItemID.PalladiumBar, 12)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
