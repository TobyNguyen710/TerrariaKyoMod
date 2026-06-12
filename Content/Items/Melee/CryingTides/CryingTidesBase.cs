using System.Linq;
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
		private int comboCooldown = 0;
		private int skillCooldown = 0;

		private const int SwingCooldown = 5;
		private const int SpinCooldown = 25;
		private const int SkillCooldownMax = 80;

		protected virtual bool HasSkill => false;
		protected virtual float SwordScale => 1f;
		protected virtual float SwordArc => 1f;

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
			Item.UseSound = SoundID.Item1;
			Item.autoReuse = true;
			Item.shoot = ProjectileID.PurificationPowder;
			Item.shootSpeed = 12f;
		}

		public override bool AltFunctionUse(Player player) => HasSkill;

		public override void HoldItem(Player player)
		{
			bool swingActive = Main.projectile.Any(p => p.active && p.owner == player.whoAmI && p.type == ModContent.ProjectileType<CryingTidesSwungSpectro>());
			if (!swingActive && comboCooldown > 0)
				comboCooldown--;
			if (skillCooldown > 0)
				skillCooldown--;
		}

		public override bool CanUseItem(Player player)
		{
			if (player.altFunctionUse == 2)
				return skillCooldown <= 0;
			if (comboCooldown > 0)
				return false;
			return !Main.projectile.Any(p => p.active && p.owner == player.whoAmI && p.type == ModContent.ProjectileType<CryingTidesSwungSpectro>());
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			if (HasSkill && player.altFunctionUse == 2)
			{
				Projectile.NewProjectile(source, player.Center, Vector2.Zero, ModContent.ProjectileType<SpectroSlowPulse>(), 0, 0f, player.whoAmI, SwordScale);
				skillCooldown = SkillCooldownMax;
				return false;
			}

			Projectile.NewProjectile(source, player.Center, velocity, ModContent.ProjectileType<CryingTidesSwungSpectro>(), damage, knockback, player.whoAmI, Combo, SwordScale, SwordArc);
			int prevCombo = Combo;
			Combo = (Combo + 1) % 3;
			comboCooldown = prevCombo == 2 ? SpinCooldown : SwingCooldown;
			return false;
		}
	}
}
