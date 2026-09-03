using System;
using KyoMod.Content.Items.Melee.CryingTides;
using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Common.Players
{
    public class CryingTidesPlayer : ModPlayer
    {
        public int HavocStacks = 0;
        private int decayTimer = 0;

        // ~1.5s holding Havoc, ~0.6s otherwise (~2.5x faster decay when inactive)
        private const int DecayIntervalHolding = 90;
        private const int DecayIntervalInactive = 36;

        public void AddHavocStack()
        {
            HavocStacks = Math.Min(HavocStacks + 1, CryingTidesBase.MaxHavocStacks);
            decayTimer = 0;
        }

        public override void PostUpdate()
        {
            if (HavocStacks <= 0)
            {
                decayTimer = 0;
                return;
            }

            bool holdingHavoc = Player.HeldItem.ModItem is CryingTidesBase sword && sword.ActiveForm == 1;
            int decayInterval = holdingHavoc ? DecayIntervalHolding : DecayIntervalInactive;

            if (++decayTimer >= decayInterval)
            {
                decayTimer = 0;
                HavocStacks--;
            }
        }

    }
}
