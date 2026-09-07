using Terraria;
using Terraria.ID;

namespace KyoMod.Content.Items.Melee.CryingTides
{
    public class CryingTidesPostPlantera : CryingTidesBase
    {
        public override string Texture => "KyoMod/Content/Items/Melee/CryingTides/CryingTides";
        public override bool HasSkill => true;
        public override bool HasHavocForm => true;
        public override bool HasUltimate => true;
        public override float SwordScale => ActiveForm == 1 ? 2.2f : 1.1f;
        public override float SwordArc => ActiveForm == 1 ? 1.85f : 1.6f;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.damage = 230;
            Item.crit = 22;
            Item.ArmorPenetration = 20;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 60);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<CryingTidesPostMech>()
                .AddIngredient(ItemID.SpectreBar, 10)
                .AddIngredient(ItemID.LifeFruit, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
