#ifndef HEX_TERRAIN_INPUT_INCLUDED
#define HEX_TERRAIN_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/SurfaceInput.hlsl"

// 全局常量（由 HexMapAuthoring.Install 通过 Shader.SetGlobalVector 写入，整张地图共用）。
// 放在 UnityPerMaterial 之外，作为全局 uniform 不影响 SRP Batcher。
// xy = 贴图平铺区域在世界 XZ 上的尺寸（N×N cell）；贴图整图铺满该区域。
float4 _ChunkWorldSize;

CBUFFER_START(UnityPerMaterial)
    float _HeightBlendStrength;
    float _HeightBlendOffset;
    float _Metallic;
    float _Smoothness;
    float _NormalScale;
    float _OcclusionStrength;
    // 调试用属性：只有 HexTerrainDebugCommon.hlsl 会打开这个开关。
    // 必须在此 cbuffer 内，否则 Entities Graphics 的 BatchRendererGroup 会拒绝该 pass。
#ifdef HEX_TERRAIN_DEBUG_PROPS
    float _DebugMode;
    float _DebugArrayDepth;
    float _DebugRange;
#endif
CBUFFER_END

// Albedo 数组必绑。其余四个数组是可选的：
// 对应 keyword（_TERRAIN_NORMAL_MAP / _TERRAIN_HEIGHT_MAP / _TERRAIN_MS_MAP / _TERRAIN_OCCLUSION_MAP）
// 未开启时不采样，走中性回退——采样未绑定的纹理槽结果是未定义的显存垃圾，
// 会产生随视角闪烁的彩色噪点（垃圾高度 → splat 权重乱跳 / 垃圾法线 → 光照闪烁）。
TEXTURE2D_ARRAY(_TerrainAlbedoArray);
SAMPLER(sampler_TerrainAlbedoArray);

#ifdef _TERRAIN_NORMAL_MAP
TEXTURE2D_ARRAY(_TerrainNormalArray);
SAMPLER(sampler_TerrainNormalArray);
#endif

#ifdef _TERRAIN_HEIGHT_MAP
TEXTURE2D_ARRAY(_TerrainHeightArray);
SAMPLER(sampler_TerrainHeightArray);
#endif

#ifdef _TERRAIN_MS_MAP
TEXTURE2D_ARRAY(_TerrainMetallicSmoothnessArray);
SAMPLER(sampler_TerrainMetallicSmoothnessArray);
#endif

#ifdef _TERRAIN_OCCLUSION_MAP
TEXTURE2D_ARRAY(_TerrainOcclusionArray);
SAMPLER(sampler_TerrainOcclusionArray);
#endif

// ---------- Shared helpers ----------

float HeightBlend(float h0, float h1, float blendWeight, float strength, float offset)
{
    float h0a = h0 + (1.0 - blendWeight) * strength;
    float h1a = h1 + blendWeight * strength;
    float ma = max(h0a, h1a) - offset;
    float b0 = max(h0a - ma, 0.0);
    float b1 = max(h1a - ma, 0.0);
    return b1 / (b0 + b1 + 0.0001);
}

// 三向高度混合：把 3 个 splat 权重按各自高度做 height-aware 再归一化。
// 权重为 0 的地形其高度被压到 0，自然退出竞争；返回归一化后的 3 个权重。
float3 HeightBlend3(float h0, float h1, float h2, float3 w, float strength, float offset)
{
    float3 ha = float3(h0, h1, h2) + w * strength;
    // 权重为 0 的通道不参与高度抬升，避免无关地形入侵
    ha *= step(1e-5, w);
    float ma = max(max(ha.x, ha.y), ha.z) - offset;
    float3 b = max(ha - ma, 0.0) * w;
    float sum = b.x + b.y + b.z + 1e-4;
    return b / sum;
}

// 世界 XZ → chunk UV：整图铺满一个 chunk。MirrorTileUV 在 chunk 边界折叠，
// 实现“以 chunk 为单位镜像”，且每个六边形只采样它在 chunk 内的 1/25。
float2 ChunkUV(float2 worldXZ)
{
    return worldXZ / _ChunkWorldSize.xy;
}

float3x3 CreateTangentFrame(float3 normalWS)
{
    float3 up = abs(normalWS.y) < 0.999 ? float3(0, 1, 0) : float3(1, 0, 0);
    float3 tangent = normalize(cross(up, normalWS));
    float3 bitangent = cross(normalWS, tangent);
    return float3x3(tangent, bitangent, normalWS);
}

// World-tile mirroring: even tiles run UV forward, odd tiles run it reversed,
// so texture values stay continuous across tile borders (no content seam).
// Returns the mirrored UV and outputs the PRE-mirror derivatives, which stay
// continuous across the whole plane and feed GRAD sampling to kill the faint
// mip line the derivative flip would otherwise leave at the border.
float2 MirrorTileUV(float2 uv, out float2 outDdx, out float2 outDdy)
{
    outDdx = ddx(uv);
    outDdy = ddy(uv);

    float2 cell = floor(uv);
    float2 f    = uv - cell;                 // [0,1)
    float2 odd  = frac(cell * 0.5) * 2.0;    // 0 or 1: is this an odd tile
    float2 mUV  = lerp(f, 1.0 - f, odd);     // odd tiles flip f -> 1-f
    return cell + mUV;
}

// Surface sample WITHOUT height. Height is only needed when two terrain
// types blend (HeightBlend); single-type paths skip the height fetch entirely.
// One mirror calc is shared across all four surface fetches.
//
// Scalars + albedo are sampled through the MIRRORED uv (mirroring keeps them
// continuous across seams while breaking up tiling). The NORMAL is the one
// channel that must NOT be mirrored: mirroring flips tangent handedness and
// leaves a sign-flip crease at every tile/chunk seam (two opposing speculars
// under high smoothness). Sampling the normal with plain tiled uv keeps the
// vector field continuous across seams. PRE-mirror derivatives feed GRAD so
// the plain-tiled normal still gets clean mips.
void SampleTerrainSurface(
    float2 uv, uint idx,
    out half3 albedo, out half3 normalTS,
    out half metallic, out half smoothness, out half occlusion)
{
    float2 dx, dy;
    float2 muv = MirrorTileUV(uv, dx, dy);

    albedo     = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainAlbedoArray, sampler_TerrainAlbedoArray, muv, idx, dx, dy).rgb;

    // 可选数组：keyword 关闭时给中性常量，禁止采样空纹理槽
#ifdef _TERRAIN_NORMAL_MAP
    normalTS   = UnpackNormalScale(SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainNormalArray, sampler_TerrainNormalArray, uv, idx, dx, dy), _NormalScale);
#else
    normalTS   = half3(0, 0, 1);
#endif

#ifdef _TERRAIN_MS_MAP
    half4 ms   = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainMetallicSmoothnessArray, sampler_TerrainMetallicSmoothnessArray, muv, idx, dx, dy);
    metallic   = ms.r;
    smoothness = ms.a;
#else
    metallic   = 0.0;
    smoothness = 1.0;   // 回退为 1，最终光滑度完全由 _Smoothness 滑条决定
#endif

#ifdef _TERRAIN_OCCLUSION_MAP
    occlusion  = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainOcclusionArray, sampler_TerrainOcclusionArray, muv, idx, dx, dy).r;
#else
    occlusion  = 1.0;   // 无遮蔽
#endif
}

half SampleTerrainHeight(float2 uv, uint idx)
{
#ifdef _TERRAIN_HEIGHT_MAP
    float2 dx, dy;
    float2 muv = MirrorTileUV(uv, dx, dy);
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainHeightArray, sampler_TerrainHeightArray, muv, idx, dx, dy).r;
#else
    return 0.5;
#endif
}

// Full sample (surface + height). Kept for callers that genuinely need both.
void SampleTerrainSingle(
    float2 uv, uint idx,
    out half3 albedo, out half3 normalTS, out half height,
    out half metallic, out half smoothness, out half occlusion)
{
    SampleTerrainSurface(uv, idx, albedo, normalTS, metallic, smoothness, occlusion);
    height = SampleTerrainHeight(uv, idx);
}

// ---------- Splat helpers ----------

// UV1 携带的 3 个地形索引（整数编码，float32 可精确表示），round 还原为 uint3。
// 层数不靠手写属性：GetDimensions 直接从绑定的 albedo 数组读真实层数
// （等价 C# 的 Texture2DArray.depth），数组换了层数自动跟随，
// 不存在「mesh 侧总数 / shader 侧总数」两张皮的问题。
// 越界索引饱和到最后一层——绝不采样未分配的层（那是显存垃圾 = 噪点）。
uint3 DecodeSplatIndices(float3 rawIndices)
{
    uint width, height, elements, mips;
    _TerrainAlbedoArray.GetDimensions(0, width, height, elements, mips);
    uint last = elements > 0u ? elements - 1u : 0u;
    uint3 idx = (uint3)(rawIndices + 0.5);
    return min(idx, uint3(last, last, last));
}

// 三向 splat 表面采样（平地路径，单一 UV）：对 3 个地形各采一次，
// 用 height-aware 归一化权重混合。权重为 0 的地形其纹理结果仍被乘 0，
// 编译器无法跳过采样，但 splat 数量固定为 3，成本可控。
void SampleSplatSurface(
    float2 uv, uint3 idx, float3 weights,
    out half3 albedo, out half3 normalTS,
    out half metallic, out half smoothness, out half occlusion)
{
    half3 a0, n0; half m0, s0, o0;
    half3 a1, n1; half m1, s1, o1;
    half3 a2, n2; half m2, s2, o2;
    SampleTerrainSurface(uv, idx.x, a0, n0, m0, s0, o0);
    SampleTerrainSurface(uv, idx.y, a1, n1, m1, s1, o1);
    SampleTerrainSurface(uv, idx.z, a2, n2, m2, s2, o2);

#ifdef _TERRAIN_HEIGHT_MAP
    half h0 = SampleTerrainHeight(uv, idx.x);
    half h1 = SampleTerrainHeight(uv, idx.y);
    half h2 = SampleTerrainHeight(uv, idx.z);
    float3 w = HeightBlend3(h0, h1, h2, weights, _HeightBlendStrength, _HeightBlendOffset);
#else
    // 无高度数组：高度混合退化为线性归一化权重
    float3 w = weights / (weights.x + weights.y + weights.z + 1e-4);
#endif

    albedo     = a0 * w.x + a1 * w.y + a2 * w.z;
    metallic   = m0 * w.x + m1 * w.y + m2 * w.z;
    smoothness = s0 * w.x + s1 * w.y + s2 * w.z;
    occlusion  = o0 * w.x + o1 * w.y + o2 * w.z;
    normalTS   = SafeNormalize(n0 * w.x + n1 * w.y + n2 * w.z);
}

#endif
