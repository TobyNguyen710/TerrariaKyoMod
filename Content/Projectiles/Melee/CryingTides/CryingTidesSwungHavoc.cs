using System;
using System.Collections.Generic;
using KyoMod.Common.Players;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Projectiles.Melee
{
    public class CryingTidesSwungHavoc : CryingTidesSwungBase
    {
        public override string Texture => "KyoMod/Content/Projectiles/Melee/CryingTides/CryingTidesScytheNoStack";

        private const float NormalTime = 40f;
        private const float HoldRatio  = 0.85f;
        private const float HalfArc    = MathHelper.Pi * 100f / 180f; // 100° per side = 200° total

        protected override float MaxTimeNormal => NormalTime;
        protected override float MaxTimeSpin   => 72f;
        protected override float BladeLength   => 52f;

        private float HavocProgress
        {
            get
            {
                float sr = SwingRatio;
                return sr <= HoldRatio ? 0f : (sr - HoldRatio) / (1f - HoldRatio);
            }
        }

        private bool         swingSoundPlayed  = false;
        private bool         ghostDelayActive  = false;
        private float        prevSpinRot       = 0f;
        private SoundStyle?  savedNpcHitSound  = null;

        private struct DelayedHitEntry
        {
            public int   NpcIndex;
            public int   Timer;
            public int   HitsLeft;
            public float DamageMultiplier;
        }
        private readonly List<DelayedHitEntry> pendingHits = [];

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

        private void PlaySwingSfx()
        {
            string name = Main.rand.NextBool() ? "KyoMod/Sounds/ScytheSlash1" : "KyoMod/Sounds/ScytheSlash2";
            SoundEngine.PlaySound(new SoundStyle(name) { PitchVariance = 0.15f, Volume = 0.6f }, Projectile.Center);
        }

        private void FireDelayedHitSingle(int idx, float damageMultiplier)
        {
            NPC npc = Main.npc[idx];
            if (!npc.active) return;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int scaledDamage = (int)(Projectile.damage * damageMultiplier);
                int dir = Math.Sign(npc.Center.X - Owner.Center.X);
                if (dir == 0) dir = lockedDirection;

                // Suppress NPC's default hit sound so only our ScytheImpactLight plays
                var savedSound = npc.HitSound;
                npc.HitSound = null;
                int actualDamage = npc.SimpleStrikeNPC(scaledDamage, dir, false, Projectile.knockBack, DamageClass.Melee);
                npc.HitSound = savedSound;

                Owner.addDPS(actualDamage);
            }

            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/ScytheImpactLight") { PitchVariance = 0.2f, Volume = 0.5f }, npc.Center);

                for (int i = 0; i < 10; i++)
                {
                    Dust d = Dust.NewDustDirect(npc.Center, 0, 0, DustID.Crimson);
                    d.velocity  = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                    d.scale     = Main.rand.NextFloat(0.8f, 1.5f);
                    d.noGravity = true;
                }

                int markIdx = Projectile.NewProjectile(Projectile.GetSource_FromThis(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<HavocSlashMark>(), 0, 0f, Projectile.owner);
                if (markIdx < Main.maxProjectiles)
                {
                    Main.projectile[markIdx].ai[0] = Main.rand.NextFloat(MathHelper.TwoPi);
                    Main.projectile[markIdx].ai[1] = 35f;
                }
            }

            Owner.GetModPlayer<CryingTidesPlayer>().AddHavocStack();
        }

        public override void AI()
        {
            if (!initialized)
            {
                Projectile.timeLeft  = (int)ActualMaxTime;
                direction            = Vector2.Normalize(Projectile.velocity);
                lockedDirection      = direction.X >= 0f ? 1 : -1;
                initialized          = true;
                Projectile.netUpdate = true;
            }

            Projectile.localNPCHitCooldown = ComboState == 2 ? 999 : 25;

            if (pendingHits.Count > 0)
            {
                if (Projectile.timeLeft <= 1)
                    ghostDelayActive = true;
                if (Projectile.timeLeft <= 2)
                    Projectile.timeLeft = 2;
            }

            for (int i = pendingHits.Count - 1; i >= 0; i--)
            {
                var entry = pendingHits[i];
                if (entry.Timer > 0)
                {
                    entry.Timer--;
                    pendingHits[i] = entry;
                }
                else
                {
                    FireDelayedHitSingle(entry.NpcIndex, entry.DamageMultiplier);
                    entry.HitsLeft--;
                    if (entry.HitsLeft <= 0)
                        pendingHits.RemoveAt(i);
                    else
                    {
                        entry.Timer = 2;
                        pendingHits[i] = entry;
                    }
                }
            }

            prevSpinRot = Projectile.rotation;
            base.AI();

            if (Main.myPlayer == Projectile.owner && !swingSoundPlayed && HavocProgress > 0f)
            {
                swingSoundPlayed = true;
                PlaySwingSfx();
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (HavocProgress == 0f) return false;

            float bladeLen = BladeLength * Projectile.scale;
            Vector2 center = Owner.Center;
            float cx = MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right);
            float cy = MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom);
            if (Vector2.DistanceSquared(center, new Vector2(cx, cy)) > bladeLen * bladeLen)
                return false;

            Vector2 toEnemy = targetHitbox.Center.ToVector2() - center;
            if (toEnemy.LengthSquared() < 1f) return true;

            float enemyAngle = toEnemy.ToRotation();
            const float buffer = 0.14f;

            if (ComboState == 2)
            {
                float arcSwept = MathHelper.WrapAngle(Projectile.rotation - prevSpinRot) * lockedDirection;
                float relAngle = MathHelper.WrapAngle((enemyAngle - prevSpinRot) * lockedDirection);
                return relAngle >= -buffer && relAngle <= arcSwept + buffer;
            }
            else
            {
                // Swept sector: covers the full arc between last frame's rotation and this frame's
                float delta     = MathHelper.WrapAngle(Projectile.rotation - prevSpinRot);
                int   sweepDir  = delta >= 0f ? 1 : -1;
                float arcSwept  = delta * sweepDir;
                float relAngle  = MathHelper.WrapAngle((enemyAngle - prevSpinRot) * sweepDir);
                return relAngle >= -buffer && relAngle <= arcSwept + buffer;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            savedNpcHitSound = target.HitSound;
            target.HitSound  = null;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.HitSound = savedNpcHitSound;

            Owner.GetModPlayer<CryingTidesPlayer>().AddHavocStack();

            if (ComboState == 1)
            {
                pendingHits.Add(new DelayedHitEntry
                {
                    NpcIndex         = target.whoAmI,
                    Timer            = 20,
                    HitsLeft         = 1,
                    DamageMultiplier = 0.5f,
                });
            }
            else if (ComboState == 2)
            {
                pendingHits.Add(new DelayedHitEntry
                {
                    NpcIndex         = target.whoAmI,
                    Timer            = 48,
                    HitsLeft         = 3,
                    DamageMultiplier = 0.5f,
                });
            }

            if (Main.netMode == NetmodeID.Server) return;

            SoundEngine.PlaySound(new SoundStyle("KyoMod/Sounds/ScytheImpactHeavy") { PitchVariance = 0.15f, Volume = 0.7f }, target.Center);

            for (int i = 0; i < 15; i++)
            {
                Dust d = Dust.NewDustDirect(target.Center, 0, 0, DustID.Crimson);
                d.velocity  = (Projectile.rotation + Main.rand.NextFloat(-0.5f, 0.5f)).ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                d.scale     = Main.rand.NextFloat(0.8f, 1.5f);
                d.noGravity = true;
            }

            // Tangent of the arc at the moment of impact: perpendicular to the blade radius in the swing direction
            float sweepDir  = ComboState == 2
                ? lockedDirection
                : (MathHelper.WrapAngle(Projectile.rotation - prevSpinRot) >= 0f ? 1f : -1f);
            float slashAngle = Projectile.rotation + MathHelper.PiOver2 * sweepDir;

            int markIdx = Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<HavocSlashMark>(), 0, 0f, Projectile.owner);
            if (markIdx < Main.maxProjectiles)
            {
                Main.projectile[markIdx].ai[0] = slashAngle;
                Main.projectile[markIdx].ai[1] = 50f;
            }
        }

        protected override void UpdateRotation(float baseAngle, float eased)
        {
            float p = HavocProgress;

            switch (ComboState)
            {
                case 0: // sweep left → right
                    Projectile.rotation = MathHelper.Lerp(
                        baseAngle - HalfArc * lockedDirection,
                        baseAngle + HalfArc * lockedDirection,
                        p);
                    break;

                case 1: // sweep right → left (mirrored combo 0)
                    Projectile.rotation = MathHelper.Lerp(
                        baseAngle + HalfArc * lockedDirection,
                        baseAngle - HalfArc * lockedDirection,
                        p);
                    break;

                case 2: // full 360° spin
                    float spinStart = baseAngle - MathHelper.PiOver2 * lockedDirection;
                    Projectile.rotation = spinStart + MathHelper.TwoPi * p * lockedDirection;
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (ghostDelayActive)
                return false;

            Texture2D scythe    = GetSwingTexture();
            bool mirrorSwing    = ComboState == 1;
            float extraAngle    = lockedDirection < 0 ? MathHelper.PiOver2 : 0f;
            SpriteEffects baseFlip = lockedDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            SpriteEffects flip  = mirrorSwing ? (baseFlip | SpriteEffects.FlipVertically) : baseFlip;
            Vector2 drawOrigin  = new Vector2(lockedDirection < 0 ? scythe.Width : 0f, mirrorSwing ? 0f : scythe.Height);
            Vector2 drawOffset  = Owner.Center + direction * HoldDistance * 0.5f - Main.screenPosition;
            float   baseAngle   = direction.ToRotation();
            float   drawRot     = mirrorSwing
                ? Projectile.rotation - MathHelper.PiOver4 - extraAngle
                : Projectile.rotation + MathHelper.PiOver4 + extraAngle;
            float   mainScale   = Projectile.scale;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            float swingStart = ComboState switch
            {
                1 => baseAngle + HalfArc * lockedDirection,
                2 => baseAngle - MathHelper.PiOver2 * lockedDirection,
                _ => baseAngle - HalfArc * lockedDirection,
            };

            const float closeThreshold = 0.55f;
            float fanTail = SwingRatio < closeThreshold
                ? swingStart
                : MathHelper.Lerp(swingStart, Projectile.rotation,
                    (SwingRatio - closeThreshold) / (1f - closeThreshold));

            int fanCount = 5 + (int)((TierArc - 1f) * 15f);
            for (int i = 0; i < fanCount; i++)
            {
                float t            = i / (float)fanCount;
                float ghostRot     = MathHelper.Lerp(fanTail, Projectile.rotation, t);
                float ghostDrawRot = mirrorSwing
                    ? ghostRot - MathHelper.PiOver4 - extraAngle
                    : ghostRot + MathHelper.PiOver4 + extraAngle;
                float alpha        = t * 0.20f * TierArc;
                float ghostScale   = mainScale * (0.75f + t * 0.25f);
                Main.spriteBatch.Draw(scythe, drawOffset, null, new Color(220, 60, 60) * alpha,
                    ghostDrawRot, drawOrigin, ghostScale, flip, 0);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Main.spriteBatch.Draw(scythe, drawOffset, null, lightColor, drawRot, drawOrigin, mainScale, flip, 0);
            return false;
        }

        private static bool DebugSpinHitbox = false; // ← toggle to visualize spin hitbox

        public override void PostDraw(Color lightColor)
        {
            if (!DebugSpinHitbox || HavocProgress == 0f) return;

            Texture2D pixel  = TextureAssets.MagicPixel.Value;
            float     blade  = BladeLength * Projectile.scale;
            Vector2   center = Owner.Center - Main.screenPosition;

            if (ComboState == 0 || ComboState == 1)
            {
                // Swept sector: previous and current blade edges (yellow), arc at tip (cyan)
                Vector2 holdOffset = direction * HoldDistance;
                Vector2 lineStart  = center + holdOffset;
                DebugLine(pixel, lineStart, lineStart + prevSpinRot.ToRotationVector2()         * blade, Color.Yellow * 0.9f);
                DebugLine(pixel, lineStart, lineStart + Projectile.rotation.ToRotationVector2() * blade, Color.Yellow * 0.9f);

                float delta    = MathHelper.WrapAngle(Projectile.rotation - prevSpinRot);
                int sweepDir   = delta >= 0f ? 1 : -1;
                float arcSwept = delta * sweepDir;
                const int arcSegs = 8;
                for (int i = 0; i < arcSegs; i++)
                {
                    float t0 = i       / (float)arcSegs;
                    float t1 = (i + 1) / (float)arcSegs;
                    float a0 = prevSpinRot + arcSwept * sweepDir * t0;
                    float a1 = prevSpinRot + arcSwept * sweepDir * t1;
                    DebugLine(pixel, lineStart + a0.ToRotationVector2() * blade,
                                     lineStart + a1.ToRotationVector2() * blade, Color.Cyan * 0.9f);
                }
            }
            else if (ComboState == 2)
            {
                // Full circle — distance boundary (dim red)
                const int circleSegs = 64;
                for (int i = 0; i < circleSegs; i++)
                {
                    float a0 = MathHelper.TwoPi * i       / circleSegs;
                    float a1 = MathHelper.TwoPi * (i + 1) / circleSegs;
                    DebugLine(pixel, center + a0.ToRotationVector2() * blade,
                                     center + a1.ToRotationVector2() * blade, Color.Red * 0.35f);
                }

                // Sector boundary lines — angular range this frame (yellow)
                DebugLine(pixel, center, center + prevSpinRot.ToRotationVector2()         * blade, Color.Yellow * 0.9f);
                DebugLine(pixel, center, center + Projectile.rotation.ToRotationVector2() * blade, Color.Yellow * 0.9f);

                // Arc — exact slice being hit-tested this frame (cyan)
                float arcSwept = MathHelper.WrapAngle(Projectile.rotation - prevSpinRot) * lockedDirection;
                const int arcSegs = 16;
                for (int i = 0; i < arcSegs; i++)
                {
                    float t0 = i       / (float)arcSegs;
                    float t1 = (i + 1) / (float)arcSegs;
                    float a0 = prevSpinRot + arcSwept * lockedDirection * t0;
                    float a1 = prevSpinRot + arcSwept * lockedDirection * t1;
                    DebugLine(pixel, center + a0.ToRotationVector2() * blade,
                                     center + a1.ToRotationVector2() * blade, Color.Cyan * 0.9f);
                }
            }
        }

        private static void DebugLine(Texture2D pixel, Vector2 from, Vector2 to, Color color)
        {
            Vector2 dir = to - from;
            float   len = dir.Length();
            if (len < 1f) return;
            Main.spriteBatch.Draw(pixel, from, new Rectangle(0, 0, 1, 1), color,
                dir.ToRotation(), Vector2.Zero, new Vector2(len, 2f), SpriteEffects.None, 0);
        }
    }
}
