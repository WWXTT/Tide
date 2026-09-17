#ifndef HEX_TERRAIN_INPUT_INCLUDED
#define HEX_TERRAIN_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/SurfaceInput.hlsl"

// 全局常量（由 HexMapAuthoring.Install 通过 Shader.SetGlobalVector 写入，整张地图共用）。
// 放在 UnityPerMaterial 之外，作为全局 uniform 不影响 SRP Batcher。
// xy = 贴图平铺区域在世界 XZ 上的尺寸（N×N cell，顶面 ChunkUV 用）；
// x 同时作为侧面周期（墙面纹素方形）。
float4 _ChunkWorldSize;

CBUFFER_START(UnityPerMaterial)
    float _HeightBlendStrength;
    float _HeightBlendOffset;
    float _TriplanarBlendSharpness;
    float _Metallic;
    float _Smoothness;
    float _NormalScale;
    float _OcclusionStrength;
    float _Parallax;
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

// 三向高度混合（经典 heightmap splatting 公式的三通道版）：
//   ha = h + w * strength —— 权重只做整体抬升，逐像素的高度差决定层与层
//   互相入侵出的锯齿边缘（岩石A的高峰穿过岩石B的低谷，而不是 50/50 混泥）。
//   b  = max(ha - max(ha) + offset, 0)
// offset = 过渡带宽度：越大越软（≈1 时近似退回线性混合），越小边缘越碎越锐，
// 岩石互侵的自然观感通常在 0.2~0.3。
// 注意 b 不要再乘 w —— 权重二次计入会让混合退回纯线性，高度差就白采了。
// 权重为 0 的槽位（不在本三角形 splat 集合内）高度压 0，退出竞争。
float3 HeightBlend3(float h0, float h1, float h2, float3 w, float strength, float offset)
{
    float3 ha = float3(h0, h1, h2) + w * strength;
    ha *= step(1e-5, w);
    float ma = max(max(ha.x, ha.y), ha.z) - offset;
    float3 b = max(ha - ma, 0.0);
    float sum = b.x + b.y + b.z + 1e-4;
    return b / sum;
}

// 世界平面坐标 → chunk UV：整图铺满一个 chunk（N×N cell 的世界区域）。
// 顶面取世界 XZ；侧面投影见 SampleSplatSurfaceTriplanar。
float2 ChunkUV(float2 worldXZ)
{
    return worldXZ / _ChunkWorldSize.xy;
}

// Surface sample WITHOUT height. Height is only needed when two terrain
// types blend (HeightBlend); single-type paths skip the height fetch entirely.
// 贴图自循环直铺：不镜像、无导数翻转，隐式导数即可。
void SampleTerrainSurface(
    float2 uv, uint idx,
    out half3 albedo, out half3 normalTS,
    out half metallic, out half smoothness, out half occlusion)
{
    albedo     = SAMPLE_TEXTURE2D_ARRAY(_TerrainAlbedoArray, sampler_TerrainAlbedoArray, uv, idx).rgb;

    // 可选数组：keyword 关闭时给中性常量，禁止采样空纹理槽
#ifdef _TERRAIN_NORMAL_MAP
    normalTS   = UnpackNormalScale(SAMPLE_TEXTURE2D_ARRAY(_TerrainNormalArray, sampler_TerrainNormalArray, uv, idx), _NormalScale);
#else
    normalTS   = half3(0, 0, 1);
#endif

#ifdef _TERRAIN_MS_MAP
    half4 ms   = SAMPLE_TEXTURE2D_ARRAY(_TerrainMetallicSmoothnessArray, sampler_TerrainMetallicSmoothnessArray, uv, idx);
    metallic   = ms.r;
    smoothness = ms.a;
#else
    // 无 MS 数组时两者都回退 1：最终值完全由 _Metallic/_Smoothness 滑条直接决定
    // （metallic 若回退 0，滑条恒等于 0×x，永远无效）
    metallic   = 1.0;
    smoothness = 1.0;
#endif

#ifdef _TERRAIN_OCCLUSION_MAP
    occlusion  = SAMPLE_TEXTURE2D_ARRAY(_TerrainOcclusionArray, sampler_TerrainOcclusionArray, uv, idx).r;
#else
    occlusion  = 1.0;   // 无遮蔽
#endif
}

half SampleTerrainHeight(float2 uv, uint idx)
{
#ifdef _TERRAIN_HEIGHT_MAP
    return SAMPLE_TEXTURE2D_ARRAY(_TerrainHeightArray, sampler_TerrainHeightArray, uv, idx).r;
#else
    return 0.5;
#endif
}

// 视差预取用：显式 LOD0。偏移前的 uv 处在「先偏移再采样」的反馈链上，
// 不能用隐式导数采样；且视差预取在 uniform 分支里，导数未定义。
half SampleTerrainHeightLOD0(float2 uv, uint idx)
{
#ifdef _TERRAIN_HEIGHT_MAP
    return SAMPLE_TEXTURE2D_ARRAY_LOD(_TerrainHeightArray, sampler_TerrainHeightArray, uv, idx, 0).r;
#else
    return 0.5;
#endif
}

// ---------- Parallax ----------

// 与 RP Lit 的 _Parallax("Scale") 同公式（core 的 ParallaxOffset1Step）同单位
// （UV 单位）：offset = (h*scale - scale/2) * viewTS.xy / max(viewTS.z, 0.42)。
// 在等大小立方体上用 Lit 调好的值可直接平移过来。
// plane 的 (u,v,n) 三元组：u/v 是该投影平面 UV 的两个世界轴，n 是带面朝向符号的
// 面法线轴 —— viewTS = (dot(view,u), dot(view,v), dot(view,n))。
float2 ParallaxOffsetTerrain(float blendedHeight, float amplitude, float3 viewTS)
{
    float h = blendedHeight * amplitude - amplitude * 0.5;
    float2 v = viewTS.xy / max(viewTS.z, 0.42);
    return h * v;
}

// 平面预取 splat 混合高度（LOD0），做一次视差偏移。需要高度数组。
float2 ApplyTriplanarParallax(float2 uv, float3 viewTS, uint3 idx, float3 weights)
{
    half h = SampleTerrainHeightLOD0(uv, idx.x) * weights.x
           + SampleTerrainHeightLOD0(uv, idx.y) * weights.y
           + SampleTerrainHeightLOD0(uv, idx.z) * weights.z;
    return uv + ParallaxOffsetTerrain(h, _Parallax, viewTS);
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

// 三向 splat 表面采样（单一 UV）：对 3 个地形各采一次，
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

// ---------- 三平面（顶面 XZ + 侧面X + 侧面Z，直铺） ----------

// 经典三平面：贴图按三个世界轴向投影，pow(|法线|, sharp) 逐轴权重混合。
// 顶面 uv=(x,z)（ChunkUV，整图铺满平铺区域）；侧面X uv=(z,y)、侧面Z uv=(x,y)
// （同除 _ChunkWorldSize.x，墙面纹素方形——ChunkUV 的 xy 是 X/Z 两个不同周期，
// 直接复用会让墙面竖向纹素拉伸 ~15%）。贴图自循环，直铺不镜像。
//
// 已知特性：六边形侧面法线落在 60° 方向上，侧面 X/Z 两根 90° 投影轴的切换
// 发生在 4 个角（其中 3 个在相机背面）；相机单方向（yaw 45°）下唯一可见的
// 切换在 SW–W 共享角，表现为软混合带——任意轴向旋转/加权都无法把全部切换
// 赶到背面（前向三面墙切向跨 180°，两根正交轴必有边界），按原三平面观感接受。
//
// 法线不走任意切线架，每平面的切线/副切线就是其 UV 的两个世界轴：
//   顶面 uv=(x,z)：T=+X B=+Z N=±Y → (n.x, n.z·sy, n.y)
//   侧面X uv=(z,y)：T=+Z B=+Y N=±X → (n.z·sx, n.y, n.x)
//   侧面Z uv=(x,y)：T=+X B=+Y N=±Z → (n.x, n.y, n.z·sz)
// sx/sy/sz 只把各平面 out-of-plane 分量翻到表面外侧。
// 三平面各自独立采样后线性混合（角落三角世界投影，无恒定 U 拉伸）。
// 视差每平面独立（_Parallax 为材质常量 → uniform 分支）。
void SampleSplatSurfaceTriplanar(
    float3 positionWS, float3 normalWS, uint3 idx, float3 splatWeights,
    out half3 albedo, out half3 normalWSOut,
    out half metallic, out half smoothness, out half occlusion)
{
    // 逐轴权重：x=侧面X（朝±X 的面）、y=顶面、z=侧面Z（朝±Z 的面）
    float3 aw = pow(abs(normalWS), _TriplanarBlendSharpness);
    float wSum = aw.x + aw.y + aw.z + 1e-4;
    float3 w = aw / wSum;

    float sx = normalWS.x >= 0.0 ? 1.0 : -1.0;
    float sy = normalWS.y >= 0.0 ? 1.0 : -1.0;
    float sz = normalWS.z >= 0.0 ? 1.0 : -1.0;

    float2 uvTop = ChunkUV(positionWS.xz);
    float2 uvX   = float2(positionWS.z, positionWS.y) / _ChunkWorldSize.x;
    float2 uvZ   = float2(positionWS.x, positionWS.y) / _ChunkWorldSize.x;

    // 视差（需要高度数组）：每平面预取 splat 混合高度做一次 UV 偏移，
    // 之后该平面的全部采样都走偏移后的 UV。各平面用自己的切线架。
#if defined(_TERRAIN_HEIGHT_MAP)
    if (_Parallax > 1e-4)
    {
        float3 viewWS = GetWorldSpaceNormalizeViewDir(positionWS);
        // 顶面 T=+X B=+Z N=±Y；侧面X T=+Z B=+Y N=±X；侧面Z T=+X B=+Y N=±Z
        uvTop = ApplyTriplanarParallax(uvTop, float3(viewWS.x, viewWS.z, viewWS.y * sy), idx, splatWeights);
        uvX   = ApplyTriplanarParallax(uvX,   float3(viewWS.z, viewWS.y, viewWS.x * sx), idx, splatWeights);
        uvZ   = ApplyTriplanarParallax(uvZ,   float3(viewWS.x, viewWS.y, viewWS.z * sz), idx, splatWeights);
    }
#endif

    half3 aT, nT; half mT, sT, oT;
    half3 aX, nX; half mX, sX, oX;
    half3 aZ, nZ; half mZ, sZ, oZ;
    SampleSplatSurface(uvTop, idx, splatWeights, aT, nT, mT, sT, oT);
    SampleSplatSurface(uvX, idx, splatWeights, aX, nX, mX, sX, oX);
    SampleSplatSurface(uvZ, idx, splatWeights, aZ, nZ, mZ, sZ, oZ);

    albedo     = aT * w.y + aX * w.x + aZ * w.z;
    metallic   = mT * w.y + mX * w.x + mZ * w.z;
    smoothness = sT * w.y + sX * w.x + sZ * w.z;
    occlusion  = oT * w.y + oX * w.x + oZ * w.z;

    half3 nWTop = half3(nT.x, nT.z * sy, nT.y);
    half3 nWX   = half3(nX.z * sx, nX.y, nX.x);
    half3 nWZ   = half3(nZ.x, nZ.y, nZ.z * sz);
    normalWSOut = SafeNormalize(nWTop * w.y + nWX * w.x + nWZ * w.z);
}

#endif
