// BubbleAuraRing.fx — a marbled radial disc/ring, drawn as its own mesh (a triangle-fan circle) with
// no texture, same "boundless to the sprite" idea as BubbleTrail.fx. Powers TWO distinct visuals from
// one shader, distinguished only by uRingRadius/uRingWidth:
//   - The big bubble's halo/aura: uRingRadius = 0, uRingWidth = ~1.0, so the "band" IS the whole disc —
//     a soft glow bright at the center, fading out toward the edge.
//   - The pop shockwave: uRingRadius animates 0 -> 1 over the effect's lifetime with a narrow
//     uRingWidth, so the bright band reads as a ring expanding outward from the pop point.
// Mesh is built in C# (BubblingOblivionMeshEffects.DrawHalo / DrawPopShockwave) as a unit-circle fan;
// UV.xy holds the same local [-1,1] coordinate as the vertex position, pre-radius-scale, so the pixel
// shader can compute radius/angle independent of the mesh's actual world-space size.
//
// Color uses the same domain-warped-noise 8-color marble technique as BubbleSwirl.fx/BubbleTrail.fx
// (duplicated, not shared — this toolchain's .fx files are each self-contained, see BubbleTrail.fx's
// own header), evaluated in polar (angle, radius) space instead of a flat UV so the marbling reads as
// swirling around the disc rather than sliding across it.

float4x4 uViewProj;
float    uTime;
float    uSeed;
float    uRingRadius; // 0..~1, center of the bright band in local unit-circle radius space
float    uRingWidth;  // band softness/width — large (~1) for a filled glow disc, small for a thin ring
float    uOpacity;    // caller-side master fade, already clamped 0..1 before being passed in

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

float Band(float n, float mid)
{
    const float halfWidth = 0.05;
    return smoothstep(mid - halfWidth, mid + halfWidth, n);
}

float4 PS(VSOutput input) : COLOR0
{
    float2 uv = input.UV;
    float radius = length(uv);
    float angle = atan2(uv.y, uv.x);

    // Polar coordinates as the noise domain so the marbling swirls around the ring instead of
    // sliding across it like the flat-UV trail/sprite shaders do.
    float2 polar = float2(angle * 1.5, radius * 4.0);

    float2 warpSeed = float2(uSeed, uSeed * 1.7);
    float2 warp = float2(
        ValueNoise(polar * 2.0 + warpSeed),
        ValueNoise(polar * 2.0 + warpSeed + 7.3)
    ) * 0.35;

    float n = ValueNoise((polar + warp) * float2(1.5, 3.0) + float2(uTime * 0.3, 0.0) + warpSeed);

    float3 marble = uColor0;
    marble = lerp(marble, uColor1, Band(n, 0.0714));
    marble = lerp(marble, uColor2, Band(n, 0.2143));
    marble = lerp(marble, uColor3, Band(n, 0.3571));
    marble = lerp(marble, uColor4, Band(n, 0.5000));
    marble = lerp(marble, uColor5, Band(n, 0.6429));
    marble = lerp(marble, uColor6, Band(n, 0.7857));
    marble = lerp(marble, uColor7, Band(n, 0.9286));

    // The band itself: a soft pulse centered on uRingRadius, width uRingWidth. With uRingRadius = 0
    // and a wide uRingWidth this degenerates into a plain center-bright radial falloff (the halo); with
    // a narrow uRingWidth and an animated uRingRadius it reads as a traveling ring (the shockwave).
    float band = 1.0 - smoothstep(0.0, uRingWidth, abs(radius - uRingRadius));

    // Independent outer cutoff so the disc mesh's own edge (radius approaching 1) never shows a hard
    // silhouette, regardless of how uRingRadius/uRingWidth are tuned.
    float outerFade = 1.0 - smoothstep(0.85, 1.0, radius);

    float alpha = band * outerFade * uOpacity * input.Color.a;

    float4 outColor;
    outColor.rgb = marble * alpha; // premultiplied — additive blend on the C# side
    outColor.a   = alpha;
    return outColor;
}

technique Technique1
{
    pass AuraRingPass
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PS();
    }
}
