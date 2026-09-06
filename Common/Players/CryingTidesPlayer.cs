using System;
using KyoMod.Content.Items.Melee.CryingTides;
using KyoMod.Content.Projectiles.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
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

        // "Liberation Energy" — the resource that gates the ultimate (Onikiri calls its equivalent
        // Stance/Vigor; renamed since CryingTides only needs one meter, not a split mobility/damage
        // pair). Fills from swings/hits like Havoc's stacks, but does NOT decay over time — it's
        // meant to persist between fights until spent on an ultimate cast, not a short combat buff.
        public float LiberationEnergy = 0f;
        public const float MaxLiberationEnergy = 75f; // tune later if balance needs it

        // Per-hit gain — +1 for every instance of damage dealt to an enemy, from any CryingTides
        // damage source (basic swing, Spectro's beam per pierce, Havoc's direct hit and each of its
        // delayed "ghost" hits, the skill scythe's direct + ghost hits) — not per swing.
        public const float EnergyPerHit = 1f;

        // Costs for the two forms' ultimates. Spectro's is already wired (SpectroUltimateCast);
        // Havoc's ultimate doesn't exist yet, but the cost is reserved here so the resource design
        // covers both from the start.
        public const float SpectroUltimateCost = 75f;
        public const float HavocUltimateCost   = 50f;

        public void AddLiberationEnergy(float amount = EnergyPerHit)
        {
            LiberationEnergy = Math.Min(LiberationEnergy + amount, MaxLiberationEnergy);
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

        // Fires the Spectro beam (Havoc's half doesn't exist yet), gated on the generic HasUltimate
        // flag rather than a hardcoded item-type check, so any tier that unlocks the ultimate works
        // automatically. Spawns the SpectroUltimateCast wind-up, which fires the actual beam itself
        // once its swing animation finishes. Restricted to Spectro form (ActiveForm == 0) since only
        // Spectro's half of the ultimate exists so far. Now gated on Liberation Energy: the cast
        // input is simply ignored below the cost, spending it in full only once the cast actually
        // fires (not on keypress) so a denied attempt doesn't drain the meter.
        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (!KyoMod.CryingTidesUltimateKeybind.JustPressed) return;
            if (Player.HeldItem?.ModItem is not CryingTidesBase sword || !sword.HasUltimate) return;
            if (sword.ActiveForm != 0) return;
            if (LiberationEnergy < SpectroUltimateCost) return;

            LiberationEnergy -= SpectroUltimateCost;

            Vector2 direction = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX * Player.direction);
            Projectile.NewProjectile(Player.GetSource_Misc("CryingTidesUltimate"), Player.Center, direction,
                ModContent.ProjectileType<SpectroUltimateCast>(), 0, 0f, Player.whoAmI, sword.SwordScale, sword.SwordArc);
        }
    }
}
