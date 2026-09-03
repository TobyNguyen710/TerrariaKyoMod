using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Content.Buffs
{
    public class CryingTidesSkillCooldown : ModBuff
    {
        // Reuse the sword item sprite as a placeholder icon until a dedicated 32x32 is made.
        public override string Texture => "KyoMod/Content/Items/Melee/CryingTides/CryingTides";

        public override void SetStaticDefaults()
        {
            Main.debuff[Type]     = true; // debuffs can't be clicked off
            Main.buffNoSave[Type] = true;
        }
    }
}
