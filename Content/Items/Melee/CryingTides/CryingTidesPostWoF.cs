using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Items.Melee.CryingTides
{
    public class CryingTidesPostWoF : CryingTidesBase
    {
        public override string Texture => "KyoMod/Content/Items/Melee/CryingTides/CryingTides";
        public override bool HasSkill => true;
        public override bool HasHavocForm => true;
        public override float SwordScale => ActiveForm == 1 ? 2f : 1.0f;
        public override float SwordArc => ActiveForm == 1 ? 1.7f : 1.45f;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.damage = 150;
            Item.crit = 17;
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
