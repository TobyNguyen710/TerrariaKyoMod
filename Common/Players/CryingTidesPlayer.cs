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
        // docs/CryingTides-SlashEffect-DesignDoc.md §10) — hardcoded to the PostWoF item and the
        // Spectro beam specifically, since PostMech's item/trigger design doesn't exist yet.
        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (!KyoMod.CryingTidesUltimateKeybind.JustPressed) return;
            if (Player.HeldItem?.ModItem is not CryingTidesPostWoF) return;

            Vector2 direction = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX * Player.direction);
            Projectile.NewProjectile(Player.GetSource_Misc("CryingTidesUltimate"), Player.Center, direction,
                ModContent.ProjectileType<SpectroUltimateBeam>(), 0, 0f, Player.whoAmI);
        }
    }
}
