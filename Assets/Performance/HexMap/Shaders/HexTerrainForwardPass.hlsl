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
    float4 terrainData              : TEXCOORD2;   // splat 权重
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

    float3 terrainIndices           : TEXCOORD7;   // splat 3 个地形索引（同三角形恒定）

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// ---------- Helpers (forward-only) ----------

// Biplanar mapping: only the two dominant projection planes are sampled
// (top + dominant side) instead of all three, cutting slope sampling by ~1/3.
// For terrain the top (XZ) plane is almost always dominant, so we pair it with
// whichever side plane (YZ or XY) the normal leans into.
struct BiplanarUV
{
    float2 uvTop;
    float2 uvSide;
    half   wTop;
    half   wSide;
};

BiplanarUV ComputeBiplanarUV(float3 positionWS, float3 normalWS, float2 invSize)
{
    BiplanarUV bp;
    const float sharpness = 4.0;
    float3 an = abs(normalWS);
    float3 w = pow(an, sharpness);

    bool sideIsX = an.x > an.z;
    bp.uvTop  = positionWS.xz * invSize;
    bp.uvSide = sideIsX ? positionWS.yz * invSize : positionWS.xy * invSize;

    half wTop  = w.y;
    half wSide = sideIsX ? w.x : w.z;
    half inv = 1.0h / (wTop + wSide + 1e-4h);
    bp.wTop  = wTop  * inv;
    bp.wSide = wSide * inv;
    return bp;
}

// Biplanar surface sample (no height) for ONE terrain: 2 surface fetch-sets.
void SampleTerrainBiplanarSurface(
    BiplanarUV bp, uint idx,
    out half3 albedo, out half3 normalWS_out,
    out half metallic, out half smoothness, out half occlusion)
{
    half3 aT, nT;  half mT, sT, oT;
    half3 aS, nS;  half mS, sS, oS;

    SampleTerrainSurface(bp.uvTop,  idx, aT, nT, mT, sT, oT);
    SampleTerrainSurface(bp.uvSide, idx, aS, nS, mS, sS, oS);

    albedo     = aT * bp.wTop + aS * bp.wSide;
    metallic   = mT * bp.wTop + mS * bp.wSide;
    smoothness = sT * bp.wTop + sS * bp.wSide;
    occlusion  = oT * bp.wTop + oS * bp.wSide;
    normalWS_out = normalize(nT * bp.wTop + nS * bp.wSide);
}

// Splat biplanar surface: blend up to 3 terrains via height-aware weights.
// Height uses a cheap single-axis (top plane) sample per terrain.
void SampleSplatBiplanar(
    BiplanarUV bp, uint3 idx, float3 weights,
    out half3 albedo, out half3 normalWS_out,
    out half metallic, out half smoothness, out half occlusion)
{
    half3 a0, n0; half m0, s0, o0;
    half3 a1, n1; half m1, s1, o1;
    half3 a2, n2; half m2, s2, o2;
    SampleTerrainBiplanarSurface(bp, idx.x, a0, n0, m0, s0, o0);
    SampleTerrainBiplanarSurface(bp, idx.y, a1, n1, m1, s1, o1);
    SampleTerrainBiplanarSurface(bp, idx.z, a2, n2, m2, s2, o2);

#ifdef _TERRAIN_HEIGHT_MAP
    half h0 = SampleTerrainHeight(bp.uvTop, idx.x);
    half h1 = SampleTerrainHeight(bp.uvTop, idx.y);
    half h2 = SampleTerrainHeight(bp.uvTop, idx.z);
    float3 w = HeightBlend3(h0, h1, h2, weights, _HeightBlendStrength, _HeightBlendOffset);
#else
    // 无高度数组：高度混合退化为线性归一化权重
    float3 w = weights / (weights.x + weights.y + weights.z + 1e-4);
#endif

    albedo     = a0 * w.x + a1 * w.y + a2 * w.z;
    metallic   = m0 * w.x + m1 * w.y + m2 * w.z;
    smoothness = s0 * w.x + s1 * w.y + s2 * w.z;
    occlusion  = o0 * w.x + o1 * w.y + o2 * w.z;
    normalWS_out = SafeNormalize(n0 * w.x + n1 * w.y + n2 * w.z);
}

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
    output.terrainData = input.color;
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
    float3 weights = input.terrainData.rgb;
    weights /= (weights.x + weights.y + weights.z + 1e-4);

    bool isFlat = abs(input.normalWS.y) > 0.7;

    half3 finalNormalWS;
    half3 finalAlbedo;
    half  finalMetallic, finalSmoothness, finalOcclusion;

    float3 normalWS = SafeNormalize(input.normalWS);

    if (isFlat)
    {
        float2 uv = ChunkUV(input.positionWS.xz);
        half3 albedo, normalTS; half metal, smth, occ;
        SampleSplatSurface(uv, idx, weights, albedo, normalTS, metal, smth, occ);

        float3x3 TBN = CreateTangentFrame(normalWS);
        finalNormalWS = SafeNormalize(TransformTangentToWorld(normalTS, TBN));
        finalAlbedo = albedo;
        finalMetallic = metal;
        finalSmoothness = smth;
        finalOcclusion = occ;
    }
    else
    {
        BiplanarUV bp = ComputeBiplanarUV(input.positionWS, normalWS, 1.0 / _ChunkWorldSize.xy);
        SampleSplatBiplanar(bp, idx, weights, finalAlbedo, finalNormalWS, finalMetallic, finalSmoothness, finalOcclusion);
        finalNormalWS = SafeNormalize(finalNormalWS);
    }

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
