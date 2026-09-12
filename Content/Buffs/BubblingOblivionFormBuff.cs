using Terraria;
using Terraria.ModLoader;

namespace KyoMod.Content.Buffs
{
    // Granted on the newly-active form right after an ultimate/form-switch cast (design doc §7.4:
    // "Grants a buff on the newly-active form, sized by how many stacks were banked in the form just
    // left"). Currently a pure marker — BubblingOblivionPlayer.FormBuffPotency records the banked-stack
    // count the buff was granted with, but no stat bonus reads it yet, since stacks can't be generated
    // at all until skills exist (§9: PostWoF) and the exact bonus numbers are explicitly deferred until
    // then (§10) rather than guessed at now. Wiring the buff itself in now means the payoff can be
    // added later without touching the ultimate-cast code that grants it.
    public class BubblingOblivionFormBuff : ModBuff
    {
        // Reuse the staff item sprite as a placeholder icon until a dedicated one is made.
        public override string Texture => "KyoMod/Content/Items/Magic/BubblingOblivion/BubblingOblivion";

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
        }
    }
}
