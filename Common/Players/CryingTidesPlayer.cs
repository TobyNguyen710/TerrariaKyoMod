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

        // Temporary test wiring for the PostMech "ultimate" ability (see
        // docs/CryingTides-SlashEffect-DesignDoc.md §11) — still no real cost/cooldown, and only
        // fires the Spectro beam (Havoc's half doesn't exist yet), but now gated on the generic
        // HasUltimate flag rather than a hardcoded item-type check, so any tier that unlocks the
        // ultimate works automatically. Spawns the SpectroUltimateCast wind-up, which fires the
        // actual beam itself once its swing animation finishes. Restricted to Spectro form
        // (ActiveForm == 0) since only Spectro's half of the ultimate exists so far.
        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (!KyoMod.CryingTidesUltimateKeybind.JustPressed) return;
            if (Player.HeldItem?.ModItem is not CryingTidesBase sword || !sword.HasUltimate) return;
            if (sword.ActiveForm != 0) return;

            Vector2 direction = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX * Player.direction);
            Projectile.NewProjectile(Player.GetSource_Misc("CryingTidesUltimate"), Player.Center, direction,
                ModContent.ProjectileType<SpectroUltimateCast>(), 0, 0f, Player.whoAmI, sword.SwordScale, sword.SwordArc);
        }
    }
}
