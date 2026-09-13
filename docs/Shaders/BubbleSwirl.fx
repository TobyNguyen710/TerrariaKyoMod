// BubbleSwirl.fx — marbled pastel iridescence for all three Bubble-form sprites (regular bolts,
// the big bubble, and the held-tip orb).
//
// Replaces two earlier attempts, both explicitly rejected by the user:
//  1. A hue-cycling rainbow rim (BubbleIridescent.fx) — read as "rainbow sticker," not "bubble."
//  2. Real background refraction via Filters.Scene (BubbleRefraction.fx) — technically worked, but a
//     simulated-optics lens effect doesn't fit Terraria's flat 2D art at all ("the lens might be too
//     much... I dont think it fits terraria"). The user was explicit that STANDING OUT is fine (like
//     CryingTides' own ink-wash shader) — the problem was specifically mimicking real-world camera
//     optics in a pixel-art game, not visual intensity.
//
// Real soap-bubble iridescence isn't actually a clean rainbow gradient — it's blotchy, marbled,
// swirling color patches from uneven soap-film thickness. That's a much better match for
// SlashInk.fx's own domain-warped-noise/stepped-color-band technique (already proven, already in this
// codebase, already reads as "painterly" rather than "simulated") than for a literal optical effect.
// This shader is that same technique with a restrained pastel palette instead of ink, giving Bubbling
// Oblivion's Bubble form a "standout shader" moment in the same visual language as CryingTides' —
// consistent mod-wide art identity — rather than reaching for photorealism.
//
// Pixel-shader-only technique, sprite-based (not a screen filter) — see BubbleRim.fx's header for why
// this is the correct tModLoader on-sprite-shader pattern. Never samples outside its own sprite's
// texture, so (unlike BubbleRefraction.fx) there's no tex2Dlod/computed-UV compiler gotcha here.

sampler uImage0 : register(s0);

float  uTime;        // seconds since this instance spawned — drifts the marble pattern
float  uSeed;         // per-instance offset so multiple bubbles don't marble in lockstep
float  uSwirlScale;   // noise frequency — higher = smaller, busier marble blotches
float  uMixStrength;  // how much the marbled palette replaces the sprite's own base color (0..1)

// 8-stop gradient (user-supplied palette) instead of 3 named colors — named uColor0..7 rather than an
// HLSL array, since a dynamically-indexed array lookup in a pixel shader is a real risk on this old
// SM3 compiler (see reference_tmodloader_shader_tooling.md for other quirks already hit) and named
// uniforms sidestep the question entirely.
float3 uColor0;
float3 uColor1;
float3 uColor2;
float3 uColor3;
float3 uColor4;
float3 uColor5;
float3 uColor6;
float3 uColor7;

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
}

float ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = Hash(i);
    float b = Hash(i + float2(1.0, 0.0));
    float c = Hash(i + float2(0.0, 1.0));
    float d = Hash(i + float2(1.0, 1.0));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Soft transition centered at `mid`, narrow enough (±0.05) that each of the 8 evenly-spaced stops
// (1/7 apart, ~0.143) still holds a real plateau near its own value before the next blend starts.
float Band(float n, float mid)
{
    const float halfWidth = 0.05;
    return smoothstep(mid - halfWidth, mid + halfWidth, n);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float4 tex = tex2D(uImage0, coords);

    // Domain-warp the sample point (same trick as SlashInk.fx's ink pass) so the marble pattern
    // swirls organically instead of sitting on a plain noise grid.
    float2 warpSeed = float2(uSeed, uSeed * 1.7);
    float2 warp = float2(
        ValueNoise(coords * 3.0 + warpSeed),
        ValueNoise(coords * 3.0 + warpSeed + 7.3)
    ) * 0.35;

    float n = ValueNoise((coords + warp) * uSwirlScale + float2(uTime * 0.15, uTime * 0.1) + warpSeed);

    // Soft 8-stop gradient chain (not hard stepped bands like the ink pass) — a blotchy soap-film
    // patch look reads better here than a watercolor-pooling look. Each Band() call blends toward the
    // next stop over a narrow window centered between the two, so every color still gets a real
    // plateau near its own stop (per the earlier cyan fix: a color that only exists as an
    // instantaneous inflection point between two lerps never reads as present at all).
    float3 marble = uColor0;
    marble = lerp(marble, uColor1, Band(n, 0.0714));
    marble = lerp(marble, uColor2, Band(n, 0.2143));
    marble = lerp(marble, uColor3, Band(n, 0.3571));
    marble = lerp(marble, uColor4, Band(n, 0.5000));
    marble = lerp(marble, uColor5, Band(n, 0.6429));
    marble = lerp(marble, uColor6, Band(n, 0.7857));
    marble = lerp(marble, uColor7, Band(n, 0.9286));

    // A gentle Fresnel-style rim on top of the marble, so the silhouette edge still pops a little —
    // much subtler role than in the earlier rim-only version, just a finishing touch here.
    float2 centered = coords - 0.5;
    float  fresnel  = pow(saturate(length(centered) * 2.0), 2.0);

    // tex.rgb is already alpha-premultiplied by the content pipeline — every marble/rim term below is
    // scaled by tex.a to stay consistent with that, so nothing bleeds outside the sprite's silhouette.
    float3 tinted   = lerp(tex.rgb, marble * tex.a, uMixStrength);
    float3 finalRgb = tinted + marble * fresnel * 0.35 * tex.a;

    float4 outColor;
    outColor.rgb = finalRgb * color.rgb;
    outColor.a   = tex.a * color.a;
    return outColor;
}

technique Technique1
{
    pass SwirlPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
