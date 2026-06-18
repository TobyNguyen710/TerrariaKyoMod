using System.Linq;
using KyoMod.Common.Players;
using KyoMod.Content.Projectiles.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace KyoMod.Content.Items.Melee.CryingTides
{
    public abstract class CryingTidesBase : ModItem
    {
        public int Combo = 0;
        public int ActiveForm = 0;
        public bool PanelOpen = false;
        private int comboCooldown  = 0;
        private int skillCooldown  = 0;
        private int comboIdleTimer = 0;

        private const int ComboResetFrames = 120; // 2 seconds

        public const int MaxHavocStacks = 20;

        private const int SwingCooldown = 5;
        private const int SpinCooldown = 20;
        private const int SkillCooldownMax = 80;

        protected virtual bool HasSkill => false;
        protected virtual float SwordScale => 1f;
        protected virtual float SwordArc => 1f;
        public virtual bool HasHavocForm => false;

        public override void SetDefaults()
        {
            Item.width = Item.height = 60;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 35;
            Item.useTime = 35;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.shootSpeed = 25f;
        }

        public override bool AltFunctionUse(Player player) => HasSkill;

        public override void HoldItem(Player player)
        {
            // Toggle panel on middle click
            if (Main.myPlayer == player.whoAmI && Main.mouseMiddle && Main.mouseMiddleRelease)
                PanelOpen = !PanelOpen;

            // Maintain panel projectile only while open
            int panelType = ModContent.ProjectileType<CryingTidesPanel>();
            if (PanelOpen)
            {
                bool panelFound = false;
                foreach (Projectile p in Main.projectile)
                {
                    if (p.active && p.owner == player.whoAmI && p.type == panelType)
                    {
                        p.timeLeft = 3;
                        panelFound = true;
                        break;
                    }
                }
                if (!panelFound && Main.myPlayer == player.whoAmI)
                    Projectile.NewProjectile(player.GetSource_ItemUse(Item), player.Center, Vector2.Zero, panelType, 0, 0f, player.whoAmI);
            }

            bool swingActive = Main.projectile.Any(p => p.active && p.owner == player.whoAmI && p.ModProjectile is CryingTidesSwungBase);
            if (!swingActive && comboCooldown > 0)
                comboCooldown--;
            if (skillCooldown > 0)
                skillCooldown--;

            if (swingActive || Combo == 0)
                comboIdleTimer = 0;
            else if (++comboIdleTimer >= ComboResetFrames)
            {
                Combo = 0;
                comboIdleTimer = 0;
            }
        }

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
                return skillCooldown <= 0;
            if (comboCooldown > 0)
                return false;
            return !Main.projectile.Any(p => p.active && p.owner == player.whoAmI && p.ModProjectile is CryingTidesSwungBase);
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            if (ActiveForm != 1)
                return;

            var kyoPlayer = player.GetModPlayer<CryingTidesPlayer>();
            float stackRatio = kyoPlayer.HavocStacks / (float)MaxHavocStacks;
            // 0 stacks = 0.6x damage, max stacks = 2.0x damage
            damage *= MathHelper.Lerp(0.6f, 2.0f, stackRatio);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (HasSkill && player.altFunctionUse == 2)
            {
                if (ActiveForm == 0)
                {
                    Projectile.NewProjectile(source, player.Center, Vector2.Zero, ModContent.ProjectileType<SpectroSlowPulse>(), 0, 0f, player.whoAmI, SwordScale);
                }
                else if (ActiveForm == 1 && HasHavocForm)
                {
                    var kyoPlayer = player.GetModPlayer<CryingTidesPlayer>();
                    int tier     = kyoPlayer.HavocStacks >= MaxHavocStacks        ? 2
                                 : kyoPlayer.HavocStacks >= MaxHavocStacks / 2    ? 1 : 0;
                    int spinDir  = velocity.X >= 0f ? 1 : -1;

                    Projectile.NewProjectile(source, player.Center, velocity,
                        ModContent.ProjectileType<HavocSkillScythe>(),
                        (int)(damage * 2.5f), knockback,
                        player.whoAmI, spinDir, tier, SwordScale);

                    kyoPlayer.HavocStacks = 0;
                }
                skillCooldown = SkillCooldownMax;
                return false;
            }

            int projType = ActiveForm == 1
                ? ModContent.ProjectileType<CryingTidesSwungHavoc>()
                : ModContent.ProjectileType<CryingTidesSwungSpectro>();

            Projectile.NewProjectile(source, player.Center, velocity, projType, damage, knockback, player.whoAmI, Combo, SwordScale, SwordArc);
            int prevCombo = Combo;
            Combo = (Combo + 1) % 3;
            comboCooldown = prevCombo == 2 ? SpinCooldown : SwingCooldown;
            return false;
        }
    }
}
