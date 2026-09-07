using Terraria;
using Terraria.ID;

namespace KyoMod.Content.Items.Melee.CryingTides
{
    public class CryingTidesPostMoonLord : CryingTidesBase
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
            Item.damage = 400;
            Item.crit = 50;
            Item.ArmorPenetration = 40;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.buyPrice(gold: 150);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<CryingTidesPostGolem>()
                .AddIngredient(ItemID.LunarBar, 12)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
