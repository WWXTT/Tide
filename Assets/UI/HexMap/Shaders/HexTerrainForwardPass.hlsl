#ifndef HEX_TERRAIN_FORWARD_PASS_INCLUDED
#define HEX_TERRAIN_FORWARD_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Lighting.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 color        : COLOR;          // splat 权重 (RGB)
    float3 terrainIndices : TEXCOORD1;    // splat 3 个地形索引 (UV1)
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float3 positionWS               : TEXCOORD0;
    float3 normalWS                 : TEXCOORD1;
    float3 terrainData              : TEXCOORD2;   // splat 权重
    half3 vertexSH                  : TEXCOORD3;

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight   : TEXCOORD4;
#else
    half fogFactor                  : TEXCOORD4;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD5;
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion           : TEXCOORD6;
#endif

    float3 terrainIndices           : TEXCOORD7;   // splat 索引

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// ---------- Vertex / Fragment ----------

void InitializeHexInputData(Varyings input, half3 normalWS, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    #if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
    #endif

    inputData.normalWS = NormalizeNormalPerPixel(normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    #else
    inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
    #else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
    #endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
}

Varyings HexTerrainVert(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

    output.positionWS = vertexInput.positionWS;
    output.positionCS = vertexInput.positionCS;
    output.normalWS = normalInput.normalWS;
    // splat 权重走顶点色 RGB；贴图 UV 全部由 shader 从世界坐标现算（三平面投影）
    output.terrainData = input.color.rgb;
    output.terrainIndices = input.terrainIndices;

    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
    fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
    #else
    output.fogFactor = fogFactor;
    #endif

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
    #endif

    return output;
}

void HexTerrainFrag(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    #ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
    #endif

    // splat：3 个地形索引（UV1）+ 3 个权重（顶点色 RGB，已插值，需归一化）
    uint3 idx = DecodeSplatIndices(input.terrainIndices);
    float3 weights = input.terrainData;
    weights /= (weights.x + weights.y + weights.z + 1e-4);

    float3 normalWS = SafeNormalize(input.normalWS);

    // 三平面映射：顶面 XZ + 侧面X + 侧面Z 按逐轴 |法线| 权重混合（见
    // SampleSplatSurfaceTriplanar）。贴图自循环直铺
    half3 finalNormalWS, finalAlbedo;
    half  finalMetallic, finalSmoothness, finalOcclusion;
    SampleSplatSurfaceTriplanar(input.positionWS, normalWS, idx, weights,
        finalAlbedo, finalNormalWS, finalMetallic, finalSmoothness, finalOcclusion);

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = finalAlbedo;
    surfaceData.metallic = finalMetallic * _Metallic;
    surfaceData.smoothness = finalSmoothness * _Smoothness;
    surfaceData.normalTS = half3(0, 0, 1);
    surfaceData.occlusion = lerp(1.0, finalOcclusion, _OcclusionStrength);
    surfaceData.emission = half3(0, 0, 0);
    surfaceData.alpha = 1.0;
    surfaceData.clearCoatMask = 0;
    surfaceData.clearCoatSmoothness = 1;

    InputData inputData;
    InitializeHexInputData(input, finalNormalWS, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
