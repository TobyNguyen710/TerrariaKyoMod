using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace KyoMod
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class KyoMod : Mod
	{
		// Registering via ModKeybind (rather than checking a raw key) is what makes this show up
		// in Terraria's own Controls menu, rebindable by the player — "Q" here is only the default.
		public static ModKeybind CryingTidesUltimateKeybind;

		// Compiled from docs/Shaders/SlashInk.fx via EasyXnb (HiDef profile, SM3) — source stays
		// local/uncommitted, only the compiled Effects/SlashInk.xnb ships with the mod.
		public static Asset<Effect> SlashInkShader;

		// Compiled from docs/Shaders/BubbleSwirl.fx the same way — pixel-shader-only technique applied
		// around a normal SpriteBatch sprite draw (see BubblingOblivionVisuals), not the custom-mesh
		// approach SlashInk.fx uses. Used on all three Bubble-form sprites (regular bolts, big bubble,
		// held-tip orb): a marbled pastel swirl (reuses SlashInk.fx's domain-warped-noise technique,
		// different palette) rather than a literal optical effect — see that .fx file's header for why
		// two earlier attempts (a hue-cycling rainbow rim, then real Filters.Scene background
		// refraction) were both dropped.
		public static Asset<Effect> BubbleSwirlShader;

		// Compiled from docs/Shaders/BubbleTrail.fx — mesh-based (own VertexShader + uViewProj), same
		// structure as SlashInk.fx, NOT a sprite-texture shader like BubbleSwirlShader above. Draws a
		// marbled ribbon trail behind Bubble form's regular bolts, built from a short history of the
		// bolt's own positions (see BubblingOblivionMeshEffects.DrawTrail) — the "exists in its own
		// form, boundless to the sprite" analog of CryingTides' SlashArcEffect.
		public static Asset<Effect> BubbleTrailShader;

		// Compiled from docs/Shaders/BubbleParticle.fx — a soft circular particle dot with no texture
		// involved at all, used by BubblingOblivionParticleSystem in place of vanilla Dust for effects
		// that need to reproduce this weapon's exact palette (vanilla Dust.color multiplies against
		// each dust type's own baked-in texture color, which none of the vanilla types tried could do
		// cleanly — see that system's own header comment).
		public static Asset<Effect> BubbleParticleShader;

		// Compiled from docs/Shaders/BubbleAuraRing.fx — mesh-based (own VertexShader + uViewProj), a
		// marbled radial disc/ring drawn as a unit-circle fan. Powers two visuals from the same shader
		// (see the .fx file's own header): the big bubble's halo/aura glow, and the pop shockwave ring.
		public static Asset<Effect> BubbleAuraRingShader;

		// Compiled from docs/Shaders/VoidBlackHole.fx — mesh-based (own VertexShader + uViewProj), same
		// unit-circle-fan structure as BubbleAuraRingShader but for Void form's redesigned ultimate: a
		// black hole appearing at the cursor on Bubble->Void switch. Solid black core + a cyan-shades
		// (57ffff and shades) marbled accretion disk, stylized rather than a literal lensing sim.
		public static Asset<Effect> VoidBlackHoleShader;

		public override void Load()
		{
			CryingTidesUltimateKeybind = KeybindLoader.RegisterKeybind(this, "CryingTidesUltimate", "Q");

			// Shader compiled HiDef/SM3 (see docs/Shaders/SlashInk.fx) — untested against this
			// machine's actual GraphicsDevice profile ahead of time, so a load failure here must not
			// take the whole mod down with it. On failure SlashInkShader stays null and the calling
			// effect's own null check falls back to its non-shader path.
			if (!Main.dedServ)
			{
				SlashInkShader = TryLoadShader("Effects/SlashInk");
				BubbleSwirlShader = TryLoadShader("Effects/BubbleSwirl");
				BubbleTrailShader = TryLoadShader("Effects/BubbleTrail");
				BubbleParticleShader = TryLoadShader("Effects/BubbleParticle");
				BubbleAuraRingShader = TryLoadShader("Effects/BubbleAuraRing");
				VoidBlackHoleShader = TryLoadShader("Effects/VoidBlackHole");
			}
		}

		private Asset<Effect> TryLoadShader(string path)
		{
			try
			{
				return Assets.Request<Effect>(path, AssetRequestMode.ImmediateLoad);
			}
			catch (Exception e)
			{
				Logger.Warn($"Shader '{path}' failed to load, falling back to its non-shader path: {e}");
				return null;
			}
		}

		public override void Unload()
		{
			CryingTidesUltimateKeybind = null;
			SlashInkShader = null;
			BubbleSwirlShader = null;
			BubbleTrailShader = null;
			BubbleParticleShader = null;
			BubbleAuraRingShader = null;
			VoidBlackHoleShader = null;
		}
	}
}
