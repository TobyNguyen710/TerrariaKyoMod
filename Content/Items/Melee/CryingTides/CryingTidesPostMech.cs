using Terraria;
using Terraria.ID;

namespace KyoMod.Content.Items.Melee.CryingTides
{
    public class CryingTidesPostMech : CryingTidesBase
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
            Item.damage = 190;
            Item.crit = 20;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(gold: 40);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<CryingTidesPostWoF>()
                .AddIngredient(ItemID.HallowedBar, 12)
                .AddIngredient(ItemID.SoulofMight, 8)
                .AddIngredient(ItemID.SoulofSight, 8)
                .AddIngredient(ItemID.SoulofFright, 8)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
