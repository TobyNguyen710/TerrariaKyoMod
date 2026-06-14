using KyoMod.Common.Players;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
    public class CryingTidesSwungHavoc : CryingTidesSwungBase
    {
        public override string Texture => "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheNoStack";

        protected override float MaxTimeNormal => 22f;
        protected override float MaxTimeSpin => 38f;
        protected override float BladeLength => 115f;

        private Texture2D GetSwingTexture()
        {
            int stacks = Owner.GetModPlayer<CryingTidesPlayer>().HavocStacks;
            string path = stacks >= 20
                ? "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheMaxStack"
                : stacks >= 10
                    ? "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheHalfStack"
                    : "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheNoStack";
            return ModContent.Request<Texture2D>(path).Value;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Owner.GetModPlayer<CryingTidesPlayer>().AddHavocStack();

            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 15; i++)
            {
                Dust d = Dust.NewDustDirect(target.Center, 0, 0, DustID.Crimson);
                d.velocity = (Projectile.rotation + Main.rand.NextFloat(-0.5f, 0.5f)).ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                d.scale = Main.rand.NextFloat(0.8f, 1.5f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D scythe = GetSwingTexture();

            SpriteEffects flip = lockedDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float extraAngle = lockedDirection < 0 ? MathHelper.PiOver2 : 0f;
            Vector2 drawOrigin = new Vector2(lockedDirection < 0 ? scythe.Width : 0f, scythe.Height);
            Vector2 drawOffset = Owner.Center + direction * HoldDistance * 0.5f - Main.screenPosition;

            float baseAngle = direction.ToRotation();
            float swingStart = ComboState switch
            {
                1 => baseAngle + BaseSwingWidth / 2f * lockedDirection,
                2 => Projectile.rotation - MathHelper.Pi * lockedDirection,
                _ => baseAngle - BaseSwingWidth / 2f * lockedDirection,
            };

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            const float closeThreshold = 0.55f;
            float fanTail = SwingRatio < closeThreshold
                ? swingStart
                : MathHelper.Lerp(swingStart, Projectile.rotation, (SwingRatio - closeThreshold) / (1f - closeThreshold));

            Color trailColor = new Color(220, 60, 60);
            int fanCount = 5 + (int)((TierArc - 1f) * 15f);
            for (int i = 0; i < fanCount; i++)
            {
                float t = i / (float)fanCount;
                float ghostRot = MathHelper.Lerp(fanTail, Projectile.rotation, t);
                float ghostDrawRot = ghostRot + MathHelper.PiOver4 + extraAngle;
                float alpha = t * 0.20f * TierArc;
                float ghostScale = Projectile.scale * (0.75f + t * 0.25f);
                Main.EntitySpriteDraw(scythe, drawOffset, null, trailColor * alpha, ghostDrawRot, drawOrigin, ghostScale, flip, 0);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            float drawRotation = Projectile.rotation + MathHelper.PiOver4 + extraAngle;
            Main.EntitySpriteDraw(scythe, drawOffset, null, lightColor, drawRotation, drawOrigin, Projectile.scale, flip, 0);
            return false;
        }
    }
}
