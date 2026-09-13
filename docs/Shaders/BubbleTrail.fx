// BubbleTrail.fx — marbled trail ribbon following Bubble form's regular bolts, drawn independent of
// the bolt's own sprite (structurally the same idea as CryingTides' SlashArcEffect/SlashInk.fx: a
// custom triangle-strip mesh built and colored by a shader, not a texture draw). User's own framing:
// "I want the shaders around/in the projectile and not just cover only the part where projectiles
// have the sprite... like how the shaders in crying tides exist in its own form (slash arc), boundless
// to the sword sprite."
//
// Mesh-based (own VertexShader + uViewProj), no texture sampling at all — same structure as
// SlashInk.fx, NOT the sprite-based BubbleSwirl.fx (which samples uImage0). The mesh itself is built
// in C# (BubblingOblivionMeshEffects.DrawTrail) from a short history of the bolt's recent positions:
// a ribbon strip tapering from zero width at the tail (oldest position) to full width at the head
// (current position), with UV.x = 0 at the tail / 1 at the head and UV.y = 0..1 across the width.
//
// Color comes from the same domain-warped-noise marble technique as BubbleSwirl.fx (reused, not
// duplicated logic by coincidence — same palette by default, so the trail reads as "the same
// substance" as the bolt it's following), just evaluated in the ribbon's own UV space instead of the
// sprite's.

float4x4 uViewProj;
float    uTime;   // seconds since the owning bolt spawned — drifts the marble pattern
float    uSeed;   // per-instance offset so multiple bolts' trails don't marble in lockstep

// Same 8-stop gradient as BubbleSwirl.fx (named uniforms, not an array — see that file's header for
// why), kept in sync so the trail reads as "the same substance" as the bolt it follows.
float3   uColor0;
float3   uColor1;
float3   uColor2;
float3   uColor3;
float3   uColor4;
float3   uColor5;
float3   uColor6;
float3   uColor7;

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

// See BubbleSwirl.fx's own Band() for the reasoning — narrow enough that each of the 8 evenly-spaced
// stops still holds a real plateau before the next blend starts.
float Band(float n, float mid)
{
    const float halfWidth = 0.05;
    return smoothstep(mid - halfWidth, mid + halfWidth, n);
}

float4 PS(VSOutput input) : COLOR0
{
    float2 uv = input.UV;

    float2 warpSeed = float2(uSeed, uSeed * 1.7);
    float2 warp = float2(
        ValueNoise(uv * 3.0 + warpSeed),
        ValueNoise(uv * 3.0 + warpSeed + 7.3)
    ) * 0.2;

    float n = ValueNoise((uv + warp) * float2(10.0, 4.0) + float2(uTime * 0.4, 0.0) + warpSeed);

    float3 marble = uColor0;
    marble = lerp(marble, uColor1, Band(n, 0.0714));
    marble = lerp(marble, uColor2, Band(n, 0.2143));
    marble = lerp(marble, uColor3, Band(n, 0.3571));
    marble = lerp(marble, uColor4, Band(n, 0.5000));
    marble = lerp(marble, uColor5, Band(n, 0.6429));
    marble = lerp(marble, uColor6, Band(n, 0.7857));
    marble = lerp(marble, uColor7, Band(n, 0.9286));

    // Fades in from the tail (u near 0) and tapers at the ribbon's own edges (v near 0/1) — the
    // geometry itself already tapers width to ~0 at the tail, this is the per-pixel softening on top
    // of that so the fade doesn't look hard-edged.
    float headFade = smoothstep(0.0, 0.3, uv.x);
    float edgeFade = 1.0 - abs(uv.y - 0.5) * 2.0;
    edgeFade = edgeFade * edgeFade;

    float alpha = headFade * edgeFade * input.Color.a;

    float4 outColor;
    outColor.rgb = marble * alpha; // premultiplied, matches SlashInk.fx's convention for this blend state
    outColor.a   = alpha;
    return outColor;
}

technique Technique1
{
    pass TrailPass
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PS();
    }
}
