#ifndef HEX_TERRAIN_INPUT_INCLUDED
#define HEX_TERRAIN_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// 全局常量（由 HexMapAuthoring.Install / HexMapShaderGlobalsSystem 通过
// Shader.SetGlobalVector 写入，整张地图共用）。放在 UnityPerMaterial 之外，
// 作为全局 uniform 不影响 SRP Batcher。
// xy = 贴图平铺区域在世界 XZ 上的尺寸（单 cell 脚印，顶面 ChunkUV 用）；
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
    // ---- 六边形单元变异（反平铺）：变换常量在 mesh 顶点流（TexCoord2），
    // 这里只有开关与层间去相关强度（距离淡出已删——重复感恰在远处最刺眼，
    // 淡出会把变异在最需要的视角杀掉；闪烁风险经评估不成立，见权重点） ----
    float _HexVariationEnabled;
    float _LayerDecorrelate;
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

// 世界平面坐标 → chunk UV：整图铺满一个 chunk（单 cell 脚印的世界区域）。
// 顶面取世界 XZ；侧面投影见 SampleSplatSurfaceTriplanar。
float2 ChunkUV(float2 worldXZ)
{
    return worldXZ / _ChunkWorldSize.xy;
}

// ---------- 六边形单元变异（反平铺）----------
//
// 逐格 UV 变换 (θ,s,ox,oy) 在 mesh 构建期按格坐标哈希烘焙进 TexCoord2
// (s·cosθ, s·sinθ, ox, oy)，shader 只应用（复数乘 = 旋转+缩放，加平移），
// 不做任何逐像素决策。变异权重（顶点色 alpha）：板心 1 → 板缘 0，
// 坡 0→peak→0，过渡区（桥/角/墙）恒 0。
// 无缝保证：所有共享线两侧权重同为 0 → 双方都落纯平铺采样，逐点相等。
// （不做距离淡出：重复感恰在远处最刺眼，淡出会把变异在最需要的视角杀掉。）
// 层间去相关 = 黄金比常量偏移，
// 纯/变换两条路径共用同一偏移 → 两路径之间无缝，且远处也生效。

// 变换 UV：M = s·R(θ)（v.xy 即 (s·cosθ, s·sinθ)），v.zw 平移（Repeat wrap 下自由）
float2 HexVarUV(float2 uv, float4 v)
{
    return float2(uv.x * v.x - uv.y * v.y, uv.x * v.y + uv.y * v.x) + v.zw;
}

// 梯度走同一线性映射：旋转+缩放对梯度是同一变换，平移不影响梯度。
// 保证变换采样的 mip 与纯采样口径一致（各向异性正确）
float2 HexVarGrad(float2 g, float4 v)
{
    return float2(g.x * v.x - g.y * v.y, g.x * v.y + g.y * v.x);
}

// 层间去相关：世界常量、逐层偏移（idx = splat 地形索引）
float2 LayerOffset(uint idx)
{
    return (idx + 0.5) * float2(0.61803399, 0.75487767) * _LayerDecorrelate;
}

// Surface sample WITHOUT height. Height is only needed when two terrain
// types blend (HeightBlend); single-type paths skip the height fetch entirely.
// 贴图自循环直铺：不镜像、无导数翻转，隐式导数即可。
// （HexTerrainDebugCommon 仍依赖本隐式导数版）
void SampleTerrainSurface(
    float2 uv, uint idx,
    out half3 albedo, out half3 normalTS,
    out half metallic, out half smoothness, out half occlusion)
{
    albedo     = SAMPLE_TEXTURE2D_ARRAY(_TerrainAlbedoArray, sampler_TerrainAlbedoArray, uv, idx).rgb;

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
    metallic   = 1.0;
    smoothness = 1.0;
#endif

#ifdef _TERRAIN_OCCLUSION_MAP
    occlusion  = SAMPLE_TEXTURE2D_ARRAY(_TerrainOcclusionArray, sampler_TerrainOcclusionArray, uv, idx).r;
#else
    occlusion  = 1.0;   // 无遮蔽
#endif
}

// 显式梯度版（变异采样路径）：变换/偏移后的 UV 必须显式传梯度，
// 否则隐式导数在旋转/缩放下会把 mip 选错（斜向闪烁）。
void SampleTerrainSurfaceGrad(
    float2 uv, float2 ddxUV, float2 ddyUV, uint idx,
    out half3 albedo, out half3 normalTS,
    out half metallic, out half smoothness, out half occlusion)
{
    albedo     = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainAlbedoArray, sampler_TerrainAlbedoArray, uv, idx, ddxUV, ddyUV).rgb;

#ifdef _TERRAIN_NORMAL_MAP
    normalTS   = UnpackNormalScale(SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainNormalArray, sampler_TerrainNormalArray, uv, idx, ddxUV, ddyUV), _NormalScale);
#else
    normalTS   = half3(0, 0, 1);
#endif

#ifdef _TERRAIN_MS_MAP
    half4 ms   = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainMetallicSmoothnessArray, sampler_TerrainMetallicSmoothnessArray, uv, idx, ddxUV, ddyUV);
    metallic   = ms.r;
    smoothness = ms.a;
#else
    metallic   = 1.0;
    smoothness = 1.0;
#endif

#ifdef _TERRAIN_OCCLUSION_MAP
    occlusion  = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainOcclusionArray, sampler_TerrainOcclusionArray, uv, idx, ddxUV, ddyUV).r;
#else
    occlusion  = 1.0;
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

half SampleTerrainHeightGrad(float2 uv, float2 ddxUV, float2 ddyUV, uint idx)
{
#ifdef _TERRAIN_HEIGHT_MAP
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainHeightArray, sampler_TerrainHeightArray, uv, idx, ddxUV, ddyUV).r;
#else
    return 0.5;
#endif
}

// 视差预取用：显式 LOD0。偏移前的 uv 处在「先偏移再采样」的反馈链上，
// 不能用隐式导数采样；且视差预取在 uniform 分支里，导数未定义。
// 视差始终在纯 UV 上估算（先于变异变换）。
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

// ---------- Splat helpers ----------

// UV1 携带的 3 个地形索引（整数编码，float32 可精确表示），round 还原为 uint3。
// 层数不靠手写属性：GetDimensions 直接从绑定的 albedo 数组读真实层数
// （等价 C# 的 Texture2DArray.depth），数组换了层数自动跟随。
// 越界索引饱和到最后一层——绝不采样未分配的层（那是显存垃圾 = 噪点）。
uint3 DecodeSplatIndices(float3 rawIndices)
{
    uint width, height, elements, mips;
    _TerrainAlbedoArray.GetDimensions(0, width, height, elements, mips);
    uint last = elements > 0u ? elements - 1u : 0u;
    uint3 idx = (uint3)(rawIndices + 0.5);
    return min(idx, uint3(last, last, last));
}

// 三向 splat 表面采样（单一 UV，隐式导数版——调试通道用）：
// 对 3 个地形各采一次，用 height-aware 归一化权重混合。
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
    float3 w = weights / (weights.x + weights.y + weights.z + 1e-4);
#endif

    albedo     = a0 * w.x + a1 * w.y + a2 * w.z;
    metallic   = m0 * w.x + m1 * w.y + m2 * w.z;
    smoothness = s0 * w.x + s1 * w.y + s2 * w.z;
    occlusion  = o0 * w.x + o1 * w.y + o2 * w.z;
    normalTS   = SafeNormalize(n0 * w.x + n1 * w.y + n2 * w.z);
}

// 三向 splat 表面采样（显式梯度版 + 层间去相关偏移）：
// 每层 uv_i = uv + LayerOffset(idx_i)（常量偏移，梯度不变）。
void SampleSplatSurfaceGrad(
    float2 uv, float2 ddxUV, float2 ddyUV, uint3 idx, float3 weights,
    out half3 albedo, out half3 normalTS,
    out half metallic, out half smoothness, out half occlusion)
{
    half3 a0, n0; half m0, s0, o0;
    half3 a1, n1; half m1, s1, o1;
    half3 a2, n2; half m2, s2, o2;
    SampleTerrainSurfaceGrad(uv + LayerOffset(idx.x), ddxUV, ddyUV, idx.x, a0, n0, m0, s0, o0);
    SampleTerrainSurfaceGrad(uv + LayerOffset(idx.y), ddxUV, ddyUV, idx.y, a1, n1, m1, s1, o1);
    SampleTerrainSurfaceGrad(uv + LayerOffset(idx.z), ddxUV, ddyUV, idx.z, a2, n2, m2, s2, o2);

#ifdef _TERRAIN_HEIGHT_MAP
    half h0 = SampleTerrainHeightGrad(uv + LayerOffset(idx.x), ddxUV, ddyUV, idx.x);
    half h1 = SampleTerrainHeightGrad(uv + LayerOffset(idx.y), ddxUV, ddyUV, idx.y);
    half h2 = SampleTerrainHeightGrad(uv + LayerOffset(idx.z), ddxUV, ddyUV, idx.z);
    float3 w = HeightBlend3(h0, h1, h2, weights, _HeightBlendStrength, _HeightBlendOffset);
#else
    float3 w = weights / (weights.x + weights.y + weights.z + 1e-4);
#endif

    albedo     = a0 * w.x + a1 * w.y + a2 * w.z;
    metallic   = m0 * w.x + m1 * w.y + m2 * w.z;
    smoothness = s0 * w.x + s1 * w.y + s2 * w.z;
    occlusion  = o0 * w.x + o1 * w.y + o2 * w.z;
    normalTS   = SafeNormalize(n0 * w.x + n1 * w.y + n2 * w.z);
}

// 三向 splat 表面采样（隐式导数 + 层间去相关偏移）——纯平铺主路径专用：
// 纯平铺 UV 是全局连续的（世界坐标投影），隐式导数处处合法，
// 且保留硬件各向异性过滤（GRAD 采样在部分驱动上不吃 aniso，
// 掠射陡坡会沿纹理长轴拉丝）。变换采样路径仍走 GRAD。
void SampleSplatSurfaceImplicit(
    float2 uv, uint3 idx, float3 weights,
    out half3 albedo, out half3 normalTS,
    out half metallic, out half smoothness, out half occlusion)
{
    half3 a0, n0; half m0, s0, o0;
    half3 a1, n1; half m1, s1, o1;
    half3 a2, n2; half m2, s2, o2;
    SampleTerrainSurface(uv + LayerOffset(idx.x), idx.x, a0, n0, m0, s0, o0);
    SampleTerrainSurface(uv + LayerOffset(idx.y), idx.y, a1, n1, m1, s1, o1);
    SampleTerrainSurface(uv + LayerOffset(idx.z), idx.z, a2, n2, m2, s2, o2);

#ifdef _TERRAIN_HEIGHT_MAP
    half h0 = SampleTerrainHeight(uv + LayerOffset(idx.x), idx.x);
    half h1 = SampleTerrainHeight(uv + LayerOffset(idx.y), idx.y);
    half h2 = SampleTerrainHeight(uv + LayerOffset(idx.z), idx.z);
    float3 w = HeightBlend3(h0, h1, h2, weights, _HeightBlendStrength, _HeightBlendOffset);
#else
    float3 w = weights / (weights.x + weights.y + weights.z + 1e-4);
#endif

    albedo     = a0 * w.x + a1 * w.y + a2 * w.z;
    metallic   = m0 * w.x + m1 * w.y + m2 * w.z;
    smoothness = s0 * w.x + s1 * w.y + s2 * w.z;
    occlusion  = o0 * w.x + o1 * w.y + o2 * w.z;
    normalTS   = SafeNormalize(n0 * w.x + n1 * w.y + n2 * w.z);
}

// ---------- 三平面（顶面 XZ + 侧面X + 侧面Z，直铺 + 六边形变异）----------

// 单平面采样：纯平铺采样恒走（隐式导数=硬件 aniso），变异权重 > ε 时 [branch]
// 追加变换采样（显式梯度=旋转缩放后的梯度）并 lerp（lerp 的是完整表面输出——
// HeightBlend3 在两条路径里各自完整跑一遍）。隐式导数在 [branch] 之前的
// uniform 流里取用，合法。
void SamplePlaneSplat(
    float2 uv, uint3 idx, float3 weights, float4 hexVar, half wVar,
    out half3 albedo, out half3 normalTS,
    out half metallic, out half smoothness, out half occlusion)
{
    // 纯平铺路径：隐式导数（全局连续 UV，aniso 生效）
    SampleSplatSurfaceImplicit(uv, idx, weights, albedo, normalTS, metallic, smoothness, occlusion);

    [branch]
    if (wVar > 1e-3)
    {
        // 变换路径：显式梯度（格级常量变换对梯度做同一线性映射）
        float2 g0 = ddx(uv);
        float2 g1 = ddy(uv);
        half3 aV; half3 nV; half mV, sV, oV;
        SampleSplatSurfaceGrad(HexVarUV(uv, hexVar), HexVarGrad(g0, hexVar), HexVarGrad(g1, hexVar),
            idx, weights, aV, nV, mV, sV, oV);
        albedo     = lerp(albedo, aV, wVar);
        normalTS   = SafeNormalize(lerp(normalTS, nV, wVar));
        metallic   = lerp(metallic, mV, wVar);
        smoothness = lerp(smoothness, sV, wVar);
        occlusion  = lerp(occlusion, oV, wVar);
    }
}

// 经典三平面：贴图按三个世界轴向投影，pow(|法线|, sharp) 逐轴权重混合。
// 顶面 uv=(x,z)（ChunkUV）；侧面X uv=(z,y)、侧面Z uv=(x,y)
// （同除 _ChunkWorldSize.x，墙面纹素方形）。贴图自循环，直铺不镜像。
//
// 法线不走任意切线架，每平面的切线/副切线就是其 UV 的两个世界轴：
//   顶面 uv=(x,z)：T=+X B=+Z N=±Y → (n.x, n.z·sy, n.y)
//   侧面X uv=(z,y)：T=+Z B=+Y N=±X → (n.z·sx, n.y, n.x)
//   侧面Z uv=(x,y)：T=+X B=+Y N=±Z → (n.x, n.y, n.z·sz)
// sx/sy/sz 只把各平面 out-of-plane 分量翻到表面外侧。
// 三平面各自独立采样后线性混合（角落三角世界投影，无恒定 U 拉伸）。
// 视差每平面独立（_Parallax 为材质常量 → uniform 分支），先于变异变换。
void SampleSplatSurfaceTriplanar(
    float3 positionWS, float3 normalWS, float3 blendNormalWS, uint3 idx, float3 splatWeights,
    float4 hexVar, half variationWeight,
    out half3 albedo, out half3 normalWSOut,
    out half metallic, out half smoothness, out half occlusion)
{
    // 双法线：blendNormalWS（TANGENT 通道，rim 融合）驱动贴图投影权重——
    // 纹理跨棱线连续；normalWS（NORMAL 通道，纯表面法线）驱动输出法线的权重与
    // 面朝向符号——光照/SH/阴影锚定真实几何（融合法线污染光照会造成
    // 背光面阴影边界偏移一截）。
    // 逐轴权重：x=侧面X（朝±X 的面）、y=顶面、z=侧面Z（朝±Z 的面）
    float3 aw = pow(abs(blendNormalWS), _TriplanarBlendSharpness);
    float wSum = aw.x + aw.y + aw.z + 1e-4;
    float3 w = aw / wSum;

    float3 awN = pow(abs(normalWS), _TriplanarBlendSharpness);
    float wSumN = awN.x + awN.y + awN.z + 1e-4;
    float3 wN = awN / wSumN;

    float sx = normalWS.x >= 0.0 ? 1.0 : -1.0;
    float sy = normalWS.y >= 0.0 ? 1.0 : -1.0;
    float sz = normalWS.z >= 0.0 ? 1.0 : -1.0;

    // 有效变异权重 = 顶点烘焙权重（板心 1 → 板缘 0；边界两侧同为 0 → 纯平铺无缝）
    half wVar = (_HexVariationEnabled > 0.5) ? variationWeight : 0.0;

    float2 uvTop = ChunkUV(positionWS.xz);
    float2 uvX   = float2(positionWS.z, positionWS.y) / _ChunkWorldSize.x;
    float2 uvZ   = float2(positionWS.x, positionWS.y) / _ChunkWorldSize.x;

    // 视差（需要高度数组）：每平面预取 splat 混合高度做一次 UV 偏移，
    // 之后该平面的全部采样都走偏移后的 UV（变异变换在视差之后）。
#if defined(_TERRAIN_HEIGHT_MAP)
    if (_Parallax > 1e-4)
    {
        float3 viewWS = GetWorldSpaceNormalizeViewDir(positionWS);
        uvTop = ApplyTriplanarParallax(uvTop, float3(viewWS.x, viewWS.z, viewWS.y * sy), idx, splatWeights);
        uvX   = ApplyTriplanarParallax(uvX,   float3(viewWS.z, viewWS.y, viewWS.x * sx), idx, splatWeights);
        uvZ   = ApplyTriplanarParallax(uvZ,   float3(viewWS.x, viewWS.y, viewWS.z * sz), idx, splatWeights);
    }
#endif

    half3 aT, nT; half mT, sT, oT;
    half3 aX, nX; half mX, sX, oX;
    half3 aZ, nZ; half mZ, sZ, oZ;
    SamplePlaneSplat(uvTop, idx, splatWeights, hexVar, wVar, aT, nT, mT, sT, oT);
    SamplePlaneSplat(uvX,   idx, splatWeights, hexVar, wVar, aX, nX, mX, sX, oX);
    SamplePlaneSplat(uvZ,   idx, splatWeights, hexVar, wVar, aZ, nZ, mZ, sZ, oZ);

    albedo     = aT * w.y + aX * w.x + aZ * w.z;
    metallic   = mT * w.y + mX * w.x + mZ * w.z;
    smoothness = sT * w.y + sX * w.x + sZ * w.z;
    occlusion  = oT * w.y + oX * w.x + oZ * w.z;

    half3 nWTop = half3(nT.x, nT.z * sy, nT.y);
    half3 nWX   = half3(nX.z * sx, nX.y, nX.x);
    half3 nWZ   = half3(nZ.x, nZ.y, nZ.z * sz);
    // 输出法线按纯法线权重混合：纹理细节叠加在真实几何朝向上
    normalWSOut = SafeNormalize(nWTop * wN.y + nWX * wN.x + nWZ * wN.z);
}

#endif
