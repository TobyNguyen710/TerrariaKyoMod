using Terraria;
using Terraria.ID;

namespace KyoMod.Content.Items.Melee.CryingTides
{
    public class CryingTidesPostGolem : CryingTidesBase
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
            Item.damage = 320;
            Item.crit = 26;
            Item.ArmorPenetration = 30;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.buyPrice(gold: 90);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<CryingTidesPostPlantera>()
                .AddIngredient(ItemID.BeetleHusk, 12)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
