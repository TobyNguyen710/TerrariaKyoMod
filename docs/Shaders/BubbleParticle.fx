// BubbleParticle.fx — a soft circular particle dot for BubblingOblivionParticleSystem, replacing
// vanilla Dust for Bubble form's ambient/pop/collapse particles.
//
// Vanilla Dust.color is a straight multiply against each dust type's own baked-in texture color (see
// that field's own doc comment: "works best with sprites with low saturation... not as effective with
// colored sprites"). Tried DustID.Water (pre-baked blue), then DustID.TintableDust (turned out to have
// a dim/grey-ish base despite the name, producing a muddy result) — neither could reproduce this
// weapon's palette accurately. This shader sidesteps the problem entirely: it's plain vertex-colored
// geometry with a procedural radial falloff, no texture/base-color multiply involved at all, so the
// color passed in is the color that comes out.
//
// Pixel-shader-only technique, sprite-based — see BubbleRim.fx's header for why this is the correct
// tModLoader on-sprite-shader pattern. Drawn via the shared 1x1 white pixel (BubblingOblivionDebug's
// GetPixel) stretched into a quad, same trick as the earlier stretched-quad version of the impact star
// — TEXCOORD0 still interpolates 0..1 across the drawn quad regardless of the source texture's own
// tiny size, so the radial distance math below works the same as it would on a real texture.

sampler uImage0 : register(s0);
float3  uColor;

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float2 centered = coords - 0.5;
    float  dist  = length(centered) * 2.0; // 0 at center, 1 at the quad's edge
    float  alpha = 1.0 - smoothstep(0.55, 1.0, dist); // solid core, soft falloff toward the edge

    float4 outColor;
    outColor.rgb = uColor * alpha * color.a; // premultiplied, matches this project's other shaders
    outColor.a   = alpha * color.a;
    return outColor;
}

technique Technique1
{
    pass ParticlePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
