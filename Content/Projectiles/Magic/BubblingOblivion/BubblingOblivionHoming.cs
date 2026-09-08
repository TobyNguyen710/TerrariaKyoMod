using Microsoft.Xna.Framework;
using Terraria;

namespace KyoMod.Content.Projectiles.Magic.BubblingOblivion
{
    // Shared homing steering for Bubble form's projectiles (regular bolts and the combo-3 big bubble)
    // so the feel stays consistent and tuning it once affects both.
    internal static class BubblingOblivionHoming
    {
        public const float Radius = 300f; // was 500 — only picks up closer targets
        public const float TurnRate = 0.04f; // radians/tick — was 0.06, gentler turning

        public static NPC FindTarget(Vector2 center)
        {
            NPC closest = null;
            float closestDist = Radius;
            foreach (NPC npc in Main.npc)
            {
                if (!npc.active || npc.friendly)
                    continue;

                float dist = Vector2.Distance(center, npc.Center);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        public static Vector2 SteerToward(Vector2 velocity, Vector2 towardTarget)
        {
            float speed = velocity.Length();
            float currentAngle = velocity.ToRotation();
            float desiredAngle = towardTarget.ToRotation();
            float diff = MathHelper.WrapAngle(desiredAngle - currentAngle);
            diff = MathHelper.Clamp(diff, -TurnRate, TurnRate);
            return (currentAngle + diff).ToRotationVector2() * speed;
        }
    }
}
