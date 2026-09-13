// SlashInk.fx — procedural ink-wash shader for CryingTides' slash arc ribbon.
// Full lifecycle modeled on CalamityOverhaul Onikiri's OniAnnihilateArc.fx:
//  - Grow-in: a clean hard-edged directional wipe along the arc, with a bright bloom riding the
//    leading edge — NOT a mirror of the fade-out.
//  - Body fill: domain-warped noise drives stepped "ink density" banding (watercolor pooling), a
//    high-frequency streak mask on the trailing edge approximates a dry-brush pattern.
//  - Fade-out: a per-pixel noise-threshold dissolve (patchy, not a single sweeping line), biased so
//    the arc's start (u near 0) dissolves first and the outer/leading edge survives longest, plus a
//    thin bright "ember" band right at each pixel's dissolve boundary before it disappears.
//  - A final CPU-driven hard alpha ramp (uHardFade) backstops the dissolve so the arc is guaranteed
//    fully gone by end of life even if noise happened to leave stray pixels alive.
//  - uForceHot skips the ink/dissolve pass entirely (used for the bright core ribbon pass, which
//    stays crisp throughout — Onikiri draws the same mesh twice, once full ink+dissolve, once thin
//    and always-hot).
// No textures — everything is procedural.
//
// IMPORTANT: exactly one `return` at the end of PS, no early exits and no `if`/branch containing a
// `return`. An earlier version branched on uForceHot with a `return` inside each branch, and the
// legacy SM3 effect compiler this project uses (EasyXnb, an old fxc-based tool) silently mangled the
// values computed inside that branch — the whole ink pass came out as near-zero density everywhere.
// Blending both code paths together with lerp/step into one final return fixed it. Don't reintroduce
// early returns here without re-testing carefully.
//
// Vertex UV convention (set by SlashArcEffect.cs): u = 0..1 along the arc sweep (t),
// v = 0 at the outer edge, 1 at the inner edge.

float4x4 uViewProj;
float    uTime;      // seconds since this arc instance spawned — drifts the noise field
float    uSeed;      // per-instance random offset so repeated swings don't look identical
float    uGrowEdge;  // wipe position along u: sweeps from -band to 1+band over the grow phase
float    uErodeT;    // 0 = fresh, 1 = fully dissolved — eased ramp covering hold+fade
float    uHardFade;  // 1 = full opacity, ramps to 0 over the last few frames (hard backstop)
float    uForceHot;  // >0.5 = skip ink banding/brush/dissolve (used for the bright core pass)

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 UV       : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 UV       : TEXCOORD0;
};

VSOutput VS(VSInput input)
{
    VSOutput output;
    output.Position = mul(input.Position, uViewProj);
    output.Color    = input.Color;
    output.UV       = input.UV;
    return output;
}

// Cheap hash-based value noise (2D) — no texture lookups needed.
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

float4 PS(VSOutput input) : COLOR0
{
    float2 uv    = input.UV;
    float4 color = input.Color;

    // --- Grow-in: hard-edged directional wipe (not a whole-shape fade-in), with a brief bright
    // bloom riding the leading edge of the wipe.
    const float growBand = 0.05;
    float reveal = smoothstep(uv.x - growBand, uv.x + growBand, uGrowEdge);
    float bloom  = (1.0 - smoothstep(0.0, growBand * 1.5, abs(uv.x - uGrowEdge))) * reveal;

    float2 warpSeed = float2(uSeed, uSeed * 1.7);

    // Domain-warp the sample point for organic swirl instead of a plain grid pattern.
    float2 warp = float2(
        ValueNoise(uv * 3.0 + warpSeed),
        ValueNoise(uv * 3.0 + warpSeed + 7.3)
    ) * 0.18;

    float ink = ValueNoise((uv + warp) * float2(14.0, 6.0) + float2(uTime * 0.6, 0.0) + warpSeed);

    // Stepped ink density (watercolor pooling): quantize into bands and darken hard near each
    // boundary.
    const float steps = 3.0;
    float stepped    = floor(ink * steps) / steps;
    float toBoundary = abs(frac(ink * steps) - 0.5) * 2.0; // 1 mid-band, 0 at a boundary
    float boundaryDarken = 1.0 - 0.85 * (1.0 - smoothstep(0.0, 0.3, toBoundary));
    float density = saturate(lerp(0.08, 1.0, stepped) * boundaryDarken);

    // Dry-brush streak mask on the trailing (inner, v->1) edge only.
    float streak         = ValueNoise(float2(uv.x * 40.0 + warpSeed.x, 0.0));
    float brushMask      = smoothstep(0.40, 0.48, streak);
    float trailingWeight = smoothstep(0.35, 1.0, uv.y);
    float brush          = lerp(1.0, brushMask, trailingWeight);

    // Bleed only at the trailing (inner, v=1) edge — the outer edge (v=0) IS the actual hit
    // boundary and must stay crisp.
    float bleedNoise = ValueNoise(uv * float2(5.0, 1.0) + warpSeed + 11.0);
    float bleedWidth = 0.14 + 0.08 * bleedNoise;
    float edgeFadeV  = smoothstep(0.0, bleedWidth, 1.0 - uv.y);

    // --- Fade-out: per-pixel noise-threshold dissolve, biased so the arc's start (u near 0) and
    // trailing edge (v near 1) die first — a patchy dissolve front, matching Onikiri's
    // OniAnnihilateArc.fx approach. At uErodeT = 0 this stays fully alive everywhere (posBias only
    // scales the *rate*, never offsets the baseline).
    float dissolveNoise = ValueNoise(uv * float2(9.0, 4.0) + warpSeed + 23.0 + uTime * 0.3);
    float posBias = (1.0 - uv.x) * 0.6 + uv.y * 0.1;
    float eTh     = uErodeT * (1.2 + posBias) - 0.55;
    float survive = smoothstep(eTh - 0.15, eTh + 0.15, dissolveNoise);

    // Thin bright "ember" band exactly at each pixel's dissolve boundary, just before it dies.
    float toEdge = dissolveNoise - eTh;
    float burn   = 1.0 - smoothstep(0.0, 0.10, abs(toEdge));

    float3 inkRgb = color.rgb * lerp(0.3, 1.0, density);
    inkRgb        = lerp(inkRgb, float3(1.0, 0.85, 0.55), burn * 0.5);
    float inkAlphaMul = density * brush * edgeFadeV * survive;

    // Blend between the full ink+dissolve result and the plain "forced hot" pass (no texture, no
    // dissolve — used for the bright core ribbon) with arithmetic instead of a branch+return.
    float  hotFlag       = step(0.5, uForceHot); // 0 = ink pass, 1 = forced hot
    float3 finalRgb      = lerp(inkRgb, color.rgb, hotFlag);
    float  finalAlphaMul = lerp(inkAlphaMul, 1.0, hotFlag);

    float4 outColor;
    outColor.rgb = finalRgb + bloom * 0.6;
    outColor.a   = color.a * finalAlphaMul * reveal * uHardFade;
    return outColor;
}

technique Technique1
{
    pass InkPass
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PS();
    }
}
