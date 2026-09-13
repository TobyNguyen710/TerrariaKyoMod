using System;
using KyoMod.Content.Buffs;
using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Common.GlobalNPCs
{
    // Stack counter + damage-over-time for the VoidCollapse debuff (design doc §14.4). Terraria's buff
    // arrays hold one instance per type with no stack count, so the stacks live here and the ModBuff
    // itself is just the visible marker whose remaining duration keeps them alive.
    public class VoidCollapseNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int stacks;
        public int Stacks => stacks;

        // Called by each Void field pulse that connects. Refreshing the buff's duration on every hit is
        // what makes stacks decay naturally when a target leaves the field rather than needing separate
        // bookkeeping — and since pulses arrive on a fixed interval, the stack count ends up tracking
        // how long the target stayed inside it.
        public void AddStack(NPC npc, int durationTicks)
        {
            stacks = Math.Min(stacks + 1, VoidCollapse.MaxStacks);
            npc.AddBuff(ModContent.BuffType<VoidCollapse>(), durationTicks);
        }

        public override void ResetEffects(NPC npc)
        {
            // Stacks are meaningless once the marker buff lapses; without this they'd persist on the
            // NPC for the rest of its life and the next application would resume from the old count.
            if (!npc.HasBuff(ModContent.BuffType<VoidCollapse>()))
                stacks = 0;
        }

        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            if (stacks <= 0 || !npc.HasBuff(ModContent.BuffType<VoidCollapse>()))
                return;

            // Cancel natural regeneration first, the same way vanilla's own damaging debuffs do —
            // otherwise a regenerating target quietly offsets part of the drain.
            if (npc.lifeRegen > 0)
                npc.lifeRegen = 0;

            int perSecond = DamagePerSecond(npc);
            npc.lifeRegen -= perSecond * 2; // lifeRegen counts in HALF hp per second, hence the doubling
            if (damage < perSecond)
                damage = perSecond;         // the number shown in the floating damage text
        }

        // Deliberately ignores defense (lifeRegen-based damage always does), which keeps the percentage
        // component honest against heavily-armored targets.
        public int DamagePerSecond(NPC npc)
            => VoidCollapse.FlatDamagePerStack * stacks
             + (int)(npc.lifeMax * VoidCollapse.PercentMaxHealthPerStack * stacks);
    }
}
