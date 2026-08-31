#ifndef HEX_TERRAIN_DEBUG_COMMON_INCLUDED
#define HEX_TERRAIN_DEBUG_COMMON_INCLUDED

// 通用地形调试可视化。
//
// 用法：材质换成 Custom/HexTerrainDebug，用 Debug Mode 下拉切换要看的中间量。
// 所有模式都是「把中间数据直接输出成颜色」，不参与光照，因此看到的颜色就是数据本身。
//
// 新增模式的方法：在 HexDebugMode 里加一个枚举值，在 HexTerrainDebugColor 的
// switch 里加一个 case，再到 HexTerrainDebug.shader 的 [Enum(...)] 里补上名字。
// 两个 pass（Forward / GBuffer）共用下面这一个函数，不要各自复制实现——
// 之前在正式 shader 里按 pass 复制调试分支导致过 brdfData 重定义的编译错误。

// 必须在 include HexTerrainInput.hlsl 之前定义，才能拿到 _DebugMode 等调试属性
#define HEX_TERRAIN_DEBUG_PROPS
#include "HexTerrainInput.hlsl"

#define HEX_DEBUG_OFF                0
#define HEX_DEBUG_ALBEDO             1
#define HEX_DEBUG_CHUNK_UV           2
#define HEX_DEBUG_WORLD_POS          3
#define HEX_DEBUG_NORMAL_WS          4
#define HEX_DEBUG_SPLAT_INDICES      5
#define HEX_DEBUG_SPLAT_WEIGHTS      6
#define HEX_DEBUG_WEIGHT_SUM         7
#define HEX_DEBUG_INDEX_BOUNDS       8
#define HEX_DEBUG_CHUNK_SIZE_PROBE   9
#define HEX_DEBUG_HEIGHT             10
#define HEX_DEBUG_MIP_LEVEL          11

// 片元级调试输入：所有模式需要的中间量都打包在这里，
// 两个 pass 各自填好后调用同一个 HexTerrainDebugColor。
struct HexDebugInput
{
    float3 positionWS;
    float3 normalWS;
    float3 splatIndicesRaw;   // UV1 原始值（未 decode）
    float3 splatWeights;      // 顶点色 RGB（未归一化）
};

// 坏值哨兵：NaN / Inf 一律显示洋红，避免 frac 把坏值伪装成正常颜色
bool HexDebugIsBad(float2 v)
{
    return any(isnan(v)) || any(isinf(v));
}

bool HexDebugIsBad3(float3 v)
{
    return any(isnan(v)) || any(isinf(v));
}

// 整数索引 → 可辨识颜色。除以 _DebugRange 把索引归一到 [0,1]，
// 默认 range=8；如果地形种类更多，把 Debug Range 调大即可。
half3 HexDebugIndexToColor(uint3 idx)
{
    float range = max(1.0, _DebugRange);
    return half3(idx.x / range, idx.y / range, idx.z / range);
}

half3 HexTerrainDebugColor(HexDebugInput dbg)
{
    int mode = (int)(_DebugMode + 0.5);

    uint3 idx = DecodeSplatIndices(dbg.splatIndicesRaw);
    float3 rawWeights = dbg.splatWeights;
    float weightSum = rawWeights.x + rawWeights.y + rawWeights.z;
    float3 weights = rawWeights / (weightSum + 1e-4);
    float2 uv = ChunkUV(dbg.positionWS.xz);

    switch (mode)
    {
        // 正常 albedo，不打光。用来区分「贴图/权重本身有问题」和「光照有问题」
        case HEX_DEBUG_ALBEDO:
        {
            half3 albedo, normalTS;
            half metallic, smoothness, occlusion;
            SampleSplatSurface(uv, idx, weights, albedo, normalTS, metallic, smoothness, occlusion);
            return albedo;
        }

        // chunk UV：正常应是重复的红绿渐变方块。洋红 = UV 是 NaN/Inf
        case HEX_DEBUG_CHUNK_UV:
        {
            if (HexDebugIsBad(uv))
                return half3(1, 0, 1);
            return half3(frac(uv.x), frac(uv.y), 0);
        }

        // 世界坐标：应是覆盖整图的缓慢渐变，不随相机变化
        case HEX_DEBUG_WORLD_POS:
            return half3(frac(dbg.positionWS.x / 100.0), frac(dbg.positionWS.z / 100.0), 0);

        // 世界法线：平地应是稳定的浅绿（+Y），坡面按朝向变化
        case HEX_DEBUG_NORMAL_WS:
        {
            if (HexDebugIsBad3(dbg.normalWS))
                return half3(1, 0, 1);
            return half3(normalize(dbg.normalWS) * 0.5 + 0.5);
        }

        // splat 索引：整图同一种地形时应是均匀纯色
        case HEX_DEBUG_SPLAT_INDICES:
            return HexDebugIndexToColor(idx);

        // splat 权重：应是 0..1 的平滑插值
        case HEX_DEBUG_SPLAT_WEIGHTS:
            return half3(rawWeights);

        // 权重和：绿 = 约等于 1（健康）；红 = 偏离太多（顶点色没写对）
        case HEX_DEBUG_WEIGHT_SUM:
        {
            float err = abs(weightSum - 1.0);
            return half3(saturate(err * 4.0), saturate(1.0 - err * 4.0), 0);
        }

        // 索引越界检查：红 = 索引 >= 数组层数（会读到未定义显存 → 闪烁噪点）
        // Debug Array Depth 要填 _TerrainAlbedoArray 的真实 depth
        case HEX_DEBUG_INDEX_BOUNDS:
        {
            uint depth = (uint)max(1.0, _DebugArrayDepth);
            bool oob = (idx.x >= depth || idx.y >= depth || idx.z >= depth);
            return oob ? half3(1, 0, 0) : half3(0, 1, 0);
        }

        // _ChunkWorldSize 探针：与几何无关的纯色。红 = 是 0（会导致除零 → Inf UV）
        case HEX_DEBUG_CHUNK_SIZE_PROBE:
        {
            bool zero = abs(_ChunkWorldSize.x) < 1e-6 || abs(_ChunkWorldSize.y) < 1e-6;
            return zero ? half3(1, 0, 0) : half3(0, 1, 0);
        }

        // 高度图采样值（灰度）。没开 _TERRAIN_HEIGHT_MAP 时是恒定 0.5 的中灰
        case HEX_DEBUG_HEIGHT:
        {
            half h = SampleTerrainHeight(uv, idx.x);
            return half3(h, h, h);
        }

        // mip 层级：按 UV 导数估算。近处偏蓝、远处偏红；
        // 出现突变条纹说明 UV 在该处不连续（接缝 / 越界）
        case HEX_DEBUG_MIP_LEVEL:
        {
            float2 dx = ddx(uv * 1024.0);
            float2 dy = ddy(uv * 1024.0);
            float d = max(dot(dx, dx), dot(dy, dy));
            float mip = 0.5 * log2(max(d, 1e-8));
            return half3(saturate(mip / 8.0), 0, saturate(1.0 - mip / 8.0));
        }

        default:
            return half3(0, 0, 0);
    }
}

#endif
