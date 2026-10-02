#ifndef BLACKHOLE_PROCEDURAL_SKY_INCLUDED
#define BLACKHOLE_PROCEDURAL_SKY_INCLUDED

// Mobile quality tier: GLES / Android-Vulkan builds get cheaper noise. Defined here so both
// BlackHole/Gargantua and BlackHole/Skybox pick the same tier and stay seamless per build.
#if defined(SHADER_API_MOBILE) || defined(SHADER_API_GLES3) || (defined(SHADER_API_VULKAN) && defined(UNITY_ANDROID))
    #define BLACKHOLE_MOBILE_TIER 1
#else
    #define BLACKHOLE_MOBILE_TIER 0
#endif

#define SKY_GRID_NEAR 90.0
#define SKY_STAR_OFFSET_SPREAD 0.7
#define SKY_STAR_TINT float3(0.85, 0.9, 1.0)
#define SKY_NEBULA_SCALE 1.1
#define SKY_NEBULA_CONTRAST 1.9
#define SKY_NEBULA_BAND_TIGHTNESS 1.0

// Sky look knobs, pushed as shader globals by BlackHoleSkyTuner (Assets/Scripts/BlackHole).
// Globals on purpose: Gargantua's lensed escape rays and the skybox must render the exact
// same sky or the silhouette seam cracks — per-material copies would drift apart.
float4 _SkyNebulaColorA;
float4 _SkyNebulaColorB;
float _SkyNebulaIntensity;
float _SkyStarIntensity;
float _SkyStarSize;
float _SkyStarDensity;
float _SkyFlowSpeed;

float SkyHashScalar(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
}

float3 SkyHashVector3(float3 p)
{
    p = float3(
        dot(p, float3(127.1, 311.7, 74.7)),
        dot(p, float3(269.5, 183.3, 246.1)),
        dot(p, float3(113.5, 271.9, 124.6)));
    return frac(sin(p) * 43758.5453123);
}

float SkySmoothValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(SkyHashScalar(i), SkyHashScalar(i + float2(1, 0)), u.x),
        lerp(SkyHashScalar(i + float2(0, 1)), SkyHashScalar(i + float2(1, 1)), u.x),
        u.y);
}

float SkyFractionalBrownianMotion(float2 p)
{
    #if BLACKHOLE_MOBILE_TIER
    // One octave fewer on mobile; the 1/0.875 normalization keeps the average level
    // matched to the desktop path so brightness does not shift between tiers.
    float v = 0.0;
    v += 0.5000 * SkySmoothValueNoise(p);
    v += 0.2500 * SkySmoothValueNoise(p * 2.03);
    v += 0.1250 * SkySmoothValueNoise(p * 4.01);
    return v * 1.142857;
    #else
    float v = 0.0;
    v += 0.5000 * SkySmoothValueNoise(p);
    v += 0.2500 * SkySmoothValueNoise(p * 2.03);
    v += 0.1250 * SkySmoothValueNoise(p * 4.01);
    v += 0.0625 * SkySmoothValueNoise(p * 8.07);
    return v * 1.0666;
    #endif
}

float SkyStarLayer(float3 direction, float gridSize, float coreRadius, float probability)
{
    float3 scaled = direction * gridSize;
    float3 cellIndex = floor(scaled);
    float3 cellPosition = frac(scaled) - 0.5;
    float3 random = SkyHashVector3(cellIndex);
    float distanceToStar = length(cellPosition - (random - 0.5) * SKY_STAR_OFFSET_SPREAD);
    float core = smoothstep(coreRadius, 0.0, distanceToStar);
    float existence = step(1.0 - probability, random.z);
    return core * existence * (0.3 + 0.7 * random.y);
}

float3 SampleProceduralSky(float3 direction)
{
    // Single star layer (2026-10-02: far layer cut — stars are the supporting act, not the
    // main effect; bigger, denser single-layer stars also read better when lensed into arcs).
    float stars = SkyStarLayer(direction, SKY_GRID_NEAR, _SkyStarSize, _SkyStarDensity);
    float3 starColor = SKY_STAR_TINT * stars * _SkyStarIntensity;

    // The nebula is skipped entirely when its intensity is zero — the branch is uniform, so
    // the full-screen skybox and every escape ray drop three FBMs each at no divergence cost.
    float3 nebulaColor = 0.0;
    if (_SkyNebulaIntensity > 0.001)
    {
        float cloudA = SkyFractionalBrownianMotion(direction.xy * SKY_NEBULA_SCALE);
        float cloudB = SkyFractionalBrownianMotion(direction.zy * SKY_NEBULA_SCALE + 7.31);
        float cloud = saturate(cloudA * cloudB * SKY_NEBULA_CONTRAST);
        float band = exp(-direction.y * direction.y * SKY_NEBULA_BAND_TIGHTNESS * SKY_NEBULA_BAND_TIGHTNESS);
        float tint = SkyFractionalBrownianMotion(direction.xz * SKY_NEBULA_SCALE * 0.5 + 3.17);
        nebulaColor = lerp(_SkyNebulaColorA.rgb, _SkyNebulaColorB.rgb, tint) * cloud * band * _SkyNebulaIntensity;
    }

    return starColor + nebulaColor;
}

// Shared by BlackHole/Gargantua (lensed escape rays) and BlackHole/Skybox (plain background)
// so the hole's silhouette stays seamless against the sky while the starfield drifts.
#define SKY_FLOW_AXIS normalize(float3(0.22, 1.0, 0.14))

float3 SkyFlowDirection(float3 direction, float time)
{
    float s, c;
    sincos(time * _SkyFlowSpeed, s, c);
    return direction * c + cross(SKY_FLOW_AXIS, direction) * s + SKY_FLOW_AXIS * dot(SKY_FLOW_AXIS, direction) * (1.0 - c);
}

#endif
