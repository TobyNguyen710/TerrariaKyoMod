using Microsoft.Xna.Framework;

namespace KyoMod.Content.Projectiles.Melee
{
    public class HavocSlashArc : SlashArcEffect
    {
        public override Color CoreColor => new(255, 235, 220);
        public override Color BodyColor => new(180, 20, 20);

        // Havoc's whole identity is melee-range weight/stacking (see the design philosophy memory) —
        // its slash should read as visibly heavier than Spectro's, not just a different color.
        protected override float MaxThickness  => 100f;
        protected override float SpinThickness => 90f;
    }
}
