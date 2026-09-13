using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Content.Buffs
{
    // Applied by the Void field's pulses (design doc §14.4). Stacking damage-over-time scaling off the
    // target's max health, so it bites hardest on exactly the big targets an ultimate is aimed at.
    //
    // The flat component is not just a balance number — it's load-bearing. NPC damage-over-time runs
    // through npc.lifeRegen, which is integer-quantized, so a pure percentage rounds to LITERALLY zero
    // on ordinary enemies (0.25% of a 14 HP slime is 0.035/sec — not "a little", none). The flat floor
    // makes the debuff meaningful everywhere, and as a bonus removes any need for a fractional damage
    // accumulator: half an HP per second of rounding loss is irrelevant next to a 10/sec floor.
    //
    // Stack count itself lives on VoidCollapseNPC, not here — Terraria's buff arrays hold one instance
    // per type with no stack field.
    public class VoidCollapse : ModBuff
    {
        public const int MaxStacks = 3;
        public const int FlatDamagePerStack = 10;
        public const float PercentMaxHealthPerStack = 0.0025f; // 0.25% per stack, 0.75% at max

        // Reuse the staff item sprite as a placeholder icon until a dedicated one is made, same as
        // BubblingOblivionFormBuff.
        public override string Texture => "KyoMod/Content/Items/Magic/BubblingOblivion/BubblingOblivion";

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }
}
