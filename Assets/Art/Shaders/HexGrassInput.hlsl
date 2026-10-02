#ifndef HEX_GRASS_INPUT_INCLUDED
#define HEX_GRASS_INPUT_INCLUDED

// HexGrass 共享声明（材质属性/贴图/顶点数据/风摆+踩踏位移）。
// 每个 pass 各自 include（本工程 ShaderLab 不支持 HLSLINCLUDE，照
// HexTerrain 的 HexTerrainInput.hlsl 模式走 pass 级 include）。

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
// 天气/季节全局契约（TTFE 同名全局 + Apply 函数）
#include "HexWeather.hlsl"

// SRP Batcher 兼容铁律（Deferred 下常规渲染路径按引擎侧布局上传材质属性，
// BRG/DOTS 路径不受影响——错位时仅 BRG 正常、预览/普通 Renderer 全黑）：
// 1) UnityPerMaterial 只放 float4/float，16 字节对齐（禁 half4）；
// 2) 声明顺序与 shader Properties 块完全一致；
// 3) 贴图属性在其位置补 float4 _MainTex_ST（引擎侧自动追加）。
CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float4 _BaseColor;
    float _Cutoff;
    float _Smoothness;
    float4 _DryColor;
    float4 _SnowColor;
    float _SwayPower;
    float _BladeHeight;
    float _BendPower;
CBUFFER_END

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

struct HexGrassAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// 位移后世界位（风摆 + 踩踏）。全部 pass 走同一函数 → 深度/阴影/正向一致。
// bendWeight 用物体空间 y（草网格根部在 0）；石头 _SwayPower/_BendPower=0 天然静止。
float3 HexGrassDisplacedWS(HexGrassAttributes input)
{
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float bendWeight = saturate(input.positionOS.y / max(_BladeHeight, 1e-4));
    positionWS += HexWindDisplacement(positionWS, bendWeight, _SwayPower);
    positionWS += HexPlayerBend(positionWS, bendWeight, _BendPower);
    return positionWS;
}

float3 HexGrassNormalWS(HexGrassAttributes input)
{
    return TransformObjectToWorldNormal(input.normalOS);
}

#endif
