// VoidBlackHole.fx — Void form's ultimate cast.
//
// FULL REWORK. Every earlier version of this file faked the shape: a Y-squashed ellipse for the disk,
// plus two hand-placed "wrap" arcs above/below the core standing in for the lensed images, plus a
// separate cross-band. That approach was chased through many rounds and could never match the
// reference, because the reference ISN'T an ellipse with decorations — it's ONE flat disk, and every
// arc in it is the SAME disk seen more than once, because gravity bends the light.
//
// So this version actually does the lensing instead of imitating its output.
//
// Per pixel:
//   1. The pixel's distance from centre IS the light ray's impact parameter b.
//   2. That ray, traced backwards from the camera, is deflected by alpha(b) ~ 2*rs/b (Schwarzschild
//      weak-field), diverging as b approaches the photon-capture radius so rays near the shadow wind
//      around many times.
//   3. The ray lives in the plane spanned by the view axis and the pixel's own screen direction. It
//      sweeps a total angle of (PI + alpha) through that plane, following r(phi) = b / sin(phi*k),
//      k = PI/(PI+alpha) — a "stretched straight line": exact when alpha=0, and carrying the correct
//      total deflection otherwise.
//   4. The accretion disk is a flat plane through the origin, tilted near edge-on. Solve for the phi
//      where the ray crosses that plane; r(phi) there is which part of the disk we're looking at.
//
// Because the sweep is longer than PI, the ray crosses the disk plane MORE THAN ONCE — and that's the
// whole trick. Crossing 0 gives both the bright band passing in front of the shadow (near side) and
// the arc lifted up and over the top (far side, which would be hidden without lensing). Crossing 1
// gives the thin bright ring hugging the shadow. They're automatically one continuous ribbon, because
// they're literally the same disk. Nothing here is placed by hand.
//
// The one optional departure from physics is uFrontBand — see the comment at `valid0`.
//
// Mesh built in C# (VoidVisuals.DrawBlackHole) — unit-circle fan, UV.xy is local [-1,1], pre-scale.
// The mesh may be scaled anisotropically to give an eye-shaped silhouette; UVs stay a unit circle, so
// the whole calculation below happens in undistorted circular space and never learns about it. That
// separation matters: warping the radius metric IN HERE to make an ellipse was tried at length and
// always failed, because the radius that forms the lensing is the radius that forms the silhouette.

#define PI 3.14159265

float4x4 uViewProj;
float    uTime;
float    uSeed;
float    uCoreRadius;  // radius of the black shadow, in UV units
float    uDiskOuter;   // outer radius of the accretion disk, in units of the shadow radius
float    uOpacity;
float    uFrontBand;   // 1 = disk's near side crosses in front of the shadow (physical); 0 = shadow
                       // left clean, for when something else occupies it. See `valid0`.

// Ramp runs uColor3 (dark ember) -> uColor1 (deep) -> uColor0 (mid/bright) -> uColor2 (hottest).
float3   uColor0;
float3   uColor1;
float3   uColor2;
float3   uColor3;

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

// Local stand-in for saturate() — this compiler has a known quirk where saturate() wrapping anything
// derived from a uniform parameter can fail to compile (see reference_tmodloader_shader_tooling
// memory); min/max against literals achieves the same clamp without touching that landmine.
float Clamp01(float x)
{
    return min(max(x, 0.0), 1.0);
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

// How much light the disk emits at radius r: nothing inside the innermost stable orbit, falling off
// with radius, fading out at the outer rim.
float DiskRadial(float r, float rIn, float rOut)
{
    // Kept deliberately tight. Seen nearly edge-on, the inner rim's NEAR side projects to a horizontal
    // line across the shadow, and this fade lands on only a pixel or two there, so that line reads
    // crisp rather than soft. Widening it was tried and looked worse — the crisp rim is wanted.
    // (Only visible when uFrontBand is on.)
    float inEdge  = smoothstep(rIn, rIn * 1.30, r);
    float outEdge = 1.0 - smoothstep(rOut * 0.45, rOut, r);
    float rn   = max(r / max(rIn, 0.0001), 1.0001);
    float emis = pow(rn, -1.15);
    return inEdge * outEdge * emis;
}

// Thin bright filaments wound into the disk, sheared by a Keplerian rate (inner material orbits
// faster) so the threads wind up over time the way the reference's do. "Ridged" noise (1 - |2n-1|)
// gives thin bright LINES rather than soft blobs, which is what reads as those parallel filaments.
//
// Seam-free by construction: the texture is sampled along a CIRCLE through the noise field rather
// than by feeding the azimuth in as a linear coordinate. A linear azimuth wraps at +-PI and leaves a
// visible seam down one side of the disk; a closed loop has no ends to mismatch. (Wrapping the noise
// lattice instead also works and was tried first, but the extra per-corner wrap arithmetic blew the
// ps_3_0 instruction budget.) Each octave rides a wider circle, for more detail around the disk, and
// drifts across the field with log-radius so neighbouring radii don't repeat the same pattern.
float DiskFilaments(float r, float xi, float rIn, float t, float seed)
{
    float rn   = max(r / max(rIn, 0.0001), 1.0001);
    float spin = t * 1.3 * pow(rn, -1.5);
    float lr   = log(rn);
    float ang  = xi + spin;
    float2 ring = float2(cos(ang), sin(ang));

    float n1 = ValueNoise(ring * 3.0  + float2(lr * 9.0 + seed,        lr * 2.0));
    float n2 = ValueNoise(ring * 6.5  + float2(lr * 4.0,               lr * 13.0 + seed * 1.7));
    float n3 = ValueNoise(ring * 13.0 + float2(lr * 21.0 + seed * 2.3, lr * 6.0));
    float fb = n1 * 0.50 + n2 * 0.32 + n3 * 0.18;

    // Low floor and a sharp ridge power on purpose: the gaps between threads need to go genuinely dark
    // for the bright ones to read as blazing rather than as a uniform glowing sheet.
    float ridge = 1.0 - abs(fb * 2.0 - 1.0);
    return 0.14 + 1.55 * pow(Clamp01(ridge), 2.6);
}

// Temperature ramp, picking only the HUE for a given intensity — the brightness itself is applied
// separately by multiplying through by the raw intensity (see the composite at the end). No black stop
// is needed at the bottom any more: that multiply takes faint material to zero on its own.
float3 Ramp(float t)
{
    float3 c = lerp(uColor3, uColor1, Clamp01(t / 0.35));
    c = lerp(c, uColor0, Clamp01((t - 0.35) / 0.30));
    c = lerp(c, uColor2, Clamp01((t - 0.65) / 0.35));
    return c;
}

float4 PS(VSOutput input) : COLOR0
{
    float2 uv = input.UV;

    // Screen-space ray geometry. UV.y runs DOWN (Terraria screen convention), flip so +Y is up.
    float2 P   = float2(uv.x, -uv.y);
    float  b   = max(length(P), 0.00001);       // impact parameter
    float2 dir = P / b;
    float  sinOm = dir.y;                        // sin/cos of the pixel's screen angle, taken directly
    float  cosOm = dir.x;                        // from the normalized direction (no atan2 needed)

    float bc   = max(uCoreRadius, 0.0001);       // photon-capture radius == the black shadow's edge
    float rs   = bc * 0.38490;                   // Schwarzschild radius (shadow sits at 2.598 * rs)
    float rIn  = 3.0 * rs;                       // innermost stable circular orbit
    float rOut = max(uDiskOuter, 0.001) * bc;

    // Disk tilt. The normal leans toward the camera by `inc` from straight up, so a small value means
    // we're looking at the disk nearly edge-on, just barely from above — which is why the far side
    // lenses over the TOP and the near side crosses in front of the shadow's LOWER half.
    float inc  = 0.13;                            // ~7.5 degrees off edge-on
    float sinI = sin(inc);
    float cosI = cos(inc);

    // Solve for where the ray's plane meets the disk plane: cos(phi)*sinI + sin(phi)*sinOm*cosI = 0.
    // Taking (sin, cos) straight from the coefficients avoids a second round of trig later.
    float Ac   = sinI;
    float Bc   = sinOm * cosI;
    float nrm  = max(sqrt(Ac * Ac + Bc * Bc), 0.00001);
    float sinP = Ac / nrm;                        // > 0, so phi1 lands in (0, PI)
    float cosP = -Bc / nrm;
    float phi1 = atan2(sinP, cosP);

    float outside = step(bc, b);

    // Deflection. Weak-field 2*rs/b far out, diverging at the capture radius so rays skimming the
    // shadow wrap around repeatedly — that pile-up is what crams the higher-order images into a thin
    // bright ring right at the shadow's edge. Clamped for numerical sanity. Inside the shadow the ray
    // is doomed anyway, so it's treated as a straight line up until it's swallowed.
    float alpha = min(2.0 * rs / max(b - bc, 0.004), 14.0) * outside;
    float sweep = PI + alpha;
    float kk    = PI / sweep;

    // --- crossing 0: the direct image (front band + the arc lensed over the top) ---
    float s0 = sin(phi1 * kk);
    float r0 = b / max(s0, 0.0005);
    // Inside the shadow only the NEAR side counts: a far-side crossing there sits behind the hole and
    // never reaches us, which is exactly why the shadow's upper half stays black while its lower half
    // is covered by the disk passing in front.
    //
    // uFrontBand can switch that near-side band off, which deliberately breaks physics. It exists for
    // the Void field's core, where the shadow is an eye's PUPIL and a bright line ruled across it reads
    // as a scratch on the lens rather than as part of an eye.
    //
    // It must stay ON (1) for an ordinary black hole. There the band is what joins the left and right
    // halves of the horizontal ribbon into one continuous streak, so culling it punches a black gap
    // through the middle of the disk. Only turn it off when something else is going to occupy the
    // shadow. Either way the disk outside the capture radius is untouched.
    float valid0 = lerp(step(phi1, PI * 0.5) * uFrontBand, 1.0, outside);

    // --- crossing 1: the first wrap-around image (thin ring hugging the shadow) ---
    float phi2 = phi1 + PI;
    float s1 = sin(phi2 * kk);
    float r1 = b / max(s1, 0.0005);
    float valid1 = outside * smoothstep(0.0, 0.30, sweep - phi2); // only once the ray sweeps far enough

    // Path-length brightening: a ray skimming ALONG the disk plane travels through far more glowing
    // material than one punching across it. This is why the edge-on band is so much more brilliant
    // than the arc over the top — physical, not a hand-placed brightness difference.
    float Gp    = -sinP * sinI + cosP * sinOm * cosI;
    float graze = pow(1.0 / max(abs(Gp), 0.055), 0.55);

    // Azimuth within the disk plane, so the filaments follow the lensing — threads bend over the top
    // arc correctly instead of being painted on in screen space.
    float xi0 = atan2(sinP * sinOm * sinI - cosP * cosI, sinP * cosOm);

    // The secondary image gets no filament detail: it renders as a thin ring where that detail isn't
    // resolvable anyway, and skipping it buys back the instruction budget the primary needs. (It is
    // also genuinely close to a smooth Einstein ring, so a clean one isn't wrong.)
    float e0 = DiskRadial(r0, rIn, rOut) * DiskFilaments(r0, xi0, rIn, uTime, uSeed) * valid0;
    float e1 = DiskRadial(r1, rIn, rOut) * 0.8 * valid1; // held back: it carries no filament detail, so at
                                                          // full weight it blooms into a featureless mass

    // Mild relativistic beaming — the side rotating toward the viewer reads brighter. Kept subtle;
    // set the 0.22 to 0 for a perfectly symmetric disk. The second image sits half a turn round the
    // disk from the first, so its beaming is exactly the mirror of it (cos flips sign).
    float beamC = 0.22 * cos(xi0);
    float emission = (e0 * (1.0 - beamC) + e1 * (1.0 + beamC)) * graze * 1.35;

    // Photon ring: stand-in for every remaining higher-order image, all of which pile into a sliver
    // just outside the capture radius. (q*q, not pow(q,2) — q goes negative and this compiler's pow
    // is exp(y*log(x)), which would produce NaN.)
    float q = (b - bc * 1.035) / (bc * 0.05);
    emission += exp(-q * q) * outside * 0.9;

    // EXPOSURE — the master brightness dial. Emission is kept linear and UNBOUNDED here rather than
    // being squashed into 0..1, because the colour below is multiplied by it and allowed to run past
    // white, which is what actually makes this look like it's glowing:
    //
    //   dim   -> tint * small  -> dark blue, barely there
    //   mid   -> tint * ~2     -> vivid blue (the blue channel clips first, which SATURATES the hue)
    //   hot   -> tint * ~10    -> every channel clips, so the core goes pure white
    //
    // That channel-by-channel clipping is exactly why the reference's orange disk has white-hot cores
    // (red saturates, then green, then blue). In blue it simply runs the other way — blue, then cyan,
    // then white — which is why no extra "make it whiter" term is needed anywhere.
    //
    // The previous version output Ramp() at full strength no matter how faint the emission was, so
    // intensity only ever shifted the hue and never the brightness. That's what made the whole disk
    // read as one flat blue cloud instead of something luminous.
    float e = emission * 3.0;
    float tone = e / (1.0 + e);   // 0..1, used ONLY to choose the hue
    float3 col = Ramp(tone) * e;  // brightness carried by e; clips past white where it's hot

    float coreAlpha = 1.0 - smoothstep(bc * 0.96, bc, b);

    // Premultiplied output (BlendState.AlphaBlend on the C# side is premultiplied): full-strength RGB
    // with a partial alpha reads as an additive glow over the sky, while the shadow's alpha of 1 with
    // black RGB punches a genuinely opaque hole. Emission in front of the shadow still draws over it,
    // because its colour is added on top rather than being masked out by the core.
    float alphaOut = max(coreAlpha, Clamp01(tone * 0.6));

    float meshFade = 1.0 - smoothstep(0.93, 1.0, length(uv)); // safety fade at the mesh's own rim
    float vis = meshFade * uOpacity * input.Color.a;

    float4 outColor;
    outColor.rgb = col * vis;
    outColor.a   = alphaOut * vis;
    return outColor;
}

technique Technique1
{
    pass BlackHolePass
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PS();
    }
}
