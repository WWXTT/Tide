#ifndef HEX_WEATHER_INCLUDED
#define HEX_WEATHER_INCLUDED

// HexMap 天气/季节全局契约 —— 变量名与 TTFE Global Shaders Controller
// (TobyFredson.TobyConstants) 完全同名：场景里的 TobyGlobalShadersController
// 写季节/雪/湿/风，HexPlayerBendDriver 写踩踏交互。全部是全局 uniform
// （UnityPerMaterial 之外，同 _ChunkWorldSize 模式），SRP Batcher 与
// Entities Graphics(BRG) 兼容；控制器不在场时全局缺省 0，所有 Apply* 均为 no-op。

float  _SeasonChangeGlobal;  // -2..2（负 = 春绿，正 = 秋枯）
float  _SnowAmount;          // 0..10
float  _Wetness;             // 0..1
float  _GlobalWindStrength;  // 0..1
float  _StrongWindSpeed;     // 1..3
float  _WindMotion;          // 0..1
float4 _WindDirection;       // xyz = 世界风向（控制器 Y 朝向）

float3 _PlayerPosition;      // 踩踏交互源位置
float  _BendRadius;          // 交互区域半径
float  _BendAmountGrass;     // 弯曲总强度（0 = 关闭）
float3 _BendDirection;       // 推开方向（世界空间）
float  _Disturbance;         // 0..1 扰动量（移动时 1，随时间衰减）

// ---------- 程序噪声（value noise，雪盖/阵风共用，无贴图依赖） ----------

float HexWeatherHash21(float2 p)
{
    p = frac(p * float2(0.1031, 0.1030));
    p += dot(p, p.yx + 33.33);
    return frac((p.x + p.y) * p.x);
}

float HexWeatherVNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = HexWeatherHash21(i);
    float b = HexWeatherHash21(i + float2(1, 0));
    float c = HexWeatherHash21(i + float2(0, 1));
    float d = HexWeatherHash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// ---------- 片元侧：季节 / 雪 / 湿 ----------

// 季节共用内核：秋 = 向枯色 lerp（枯色目标保留逐纹素变化但带亮度下限——
// HexMap 植被贴图整体偏暗（alpha 图集），无下限时目标色会被压到近黑）；
// 春 = 冷暖微调增绿。greenDom 掩码让地形上只有绿色占优的层（草地）被染色，
// 沙/岩/雪层天然免疫；植被传 mask = 1 整株生效。
void HexSeasonApply(inout half3 albedo, half3 dryColor, float greenDom)
{
    float season = _SeasonChangeGlobal;
    if (season > 1e-4)
    {
        float dry = saturate(season * 0.5) * greenDom;
        half3 dryTarget = dryColor * lerp(0.85, 1.35, saturate(albedo.g * 1.5));
        albedo = lerp(albedo, dryTarget, dry);
    }
    else if (season < -1e-4)
    {
        float lush = saturate(-season * 0.5) * greenDom;
        albedo *= lerp(half3(1, 1, 1), half3(0.88, 1.1, 0.82), lush);
    }
}

// 地形版：绿优掩码自动锁定草层
void ApplySeasonGrass(inout half3 albedo, half3 dryColor)
{
    float greenDom = saturate((albedo.g - max(albedo.r, albedo.b)) * 6.0);
    HexSeasonApply(albedo, dryColor, greenDom);
}

// 植被版：整株染色
void ApplySeasonVegetation(inout half3 albedo, half3 dryColor)
{
    HexSeasonApply(albedo, dryColor, 1.0);
}

// 雪：朝上掩码（侧壁/陡坡不吃雪）+ 双频噪声打破均匀边缘。
// _SnowAmount 0..10 归一到 0..1；upMask 用几何法线（非贴图法线）保持雪线平滑。
void ApplySnow(inout half3 albedo, inout half smoothness,
               float3 normalWS, float2 worldXZ, half3 snowColor)
{
    float amount = saturate(_SnowAmount * 0.1);
    if (amount <= 1e-4)
        return;

    float upMask = saturate(normalWS.y * 1.4);
    float n = HexWeatherVNoise(worldXZ * 0.23) * 0.6
            + HexWeatherVNoise(worldXZ * 1.1) * 0.4;
    float snow = saturate(amount * 1.7 * upMask * (0.55 + 0.9 * n));
    albedo = lerp(albedo, snowColor * (0.92 + 0.08 * n), snow);
    smoothness = lerp(smoothness, 0.5, snow * 0.7);
}

// 湿（雨）：变暗 + 光滑度抬升（湿地反光）
void ApplyWetness(inout half3 albedo, inout half smoothness)
{
    float wet = saturate(_Wetness);
    if (wet <= 1e-4)
        return;
    albedo *= 1.0 - 0.4 * wet;
    smoothness = lerp(smoothness, 0.92, wet * 0.7);
}

// ---------- 顶点侧：风摆 + 踩踏（植被专用） ----------

// 风摆位移（TTFE 核心移植·简化版）：
// 世界坐标噪声阵风场沿风向滚动（TTFE NoiseRotation 同式）× 高度权重方向弯曲，
// 叠加 _WindMotion 定向持续弯曲与高频颤动。世界噪声天然让相邻植株去相关。
// bendWeight：根 0 → 梢 1；swayPower：材质摆动强度（石头 0 = 完全静止）。
float3 HexWindDisplacement(float3 positionWS, float bendWeight, float swayPower)
{
    if (_GlobalWindStrength <= 1e-4 || swayPower <= 1e-4)
        return float3(0, 0, 0);

    float2 windXZ = _WindDirection.xz;
    float windLen = length(windXZ);
    windXZ = windLen < 1e-4 ? float2(1, 0) : windXZ / windLen;

    // 阵风场：噪声 UV 沿风向随时间滚动，平方制造静-强对比
    float2 noiseUV = positionWS.xz - windXZ * (_TimeParameters.x * _StrongWindSpeed);
    float gust = HexWeatherVNoise(noiseUV * 0.35) * 0.65
               + HexWeatherVNoise(noiseUV * 1.3) * 0.35;
    gust *= gust;

    float amp = _GlobalWindStrength * swayPower * bendWeight;

    // 主摆：沿风向 ×（基础 + 阵风增量）
    float3 disp = float3(windXZ.x, 0, windXZ.y) * (amp * (0.35 + 0.65 * gust));

    // 定向持续弯曲（_WindMotion）：风向 + 轻微下压
    disp += float3(windXZ.x, -0.15, windXZ.y) * (_WindMotion * 0.3 * amp);

    // 高频颤动：细碎抖动
    float jitter = sin(_TimeParameters.x * 5.0 + positionWS.x * 3.7 + positionWS.z * 2.9);
    disp.xz += windXZ * (jitter * 0.05 * amp);

    return disp;
}

// 踩踏弯曲（交互区域控制）：xz 球形遮罩，边缘平方软化；推开 + 下压。
// bendPower：材质响应强度（石头 0）。
float3 HexPlayerBend(float3 positionWS, float bendWeight, float bendPower)
{
    if (_BendAmountGrass <= 1e-5 || bendPower <= 1e-5)
        return float3(0, 0, 0);

    float2 d = positionWS.xz - _PlayerPosition.xz;
    float mask = saturate(1.0 - length(d) / max(_BendRadius, 1e-4));
    mask *= mask;
    float amt = mask * _BendAmountGrass * saturate(_Disturbance) * bendWeight * bendPower;

    float3 dir = _BendDirection;
    float dirLen = length(dir);
    dir = dirLen < 1e-4 ? float3(0.5, 0, 0.5) : dir / dirLen;
    return dir * amt + float3(0, -0.35, 0) * amt;
}

#endif
