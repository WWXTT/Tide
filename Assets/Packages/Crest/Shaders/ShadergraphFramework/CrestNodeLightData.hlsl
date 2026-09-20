// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

#ifdef SHADERPASS

#include "../OceanGlobals.hlsl"

// Based on tutorial: https://connect.unity.com/p/adding-your-own-hlsl-code-to-shader-graph-the-custom-function-node

#include "OceanGraphConstants.hlsl"

#if CREST_URP
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "../OceanLightingHelpers.hlsl"
#endif

#if CREST_HDRP_FORWARD_PASS
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

#if UNITY_VERSION < 60000000
#define GetMeshRenderingLayerMask GetMeshRenderingLightLayer
#endif // UNITY_VERSION

#ifndef LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
#define LIGHTLOOP_DISABLE_TILE_AND_CLUSTER 1
#define d_Crest_LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
#endif // LIGHTLOOP_DISABLE_TILE_AND_CLUSTER

// Adapted from: com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoop.hlsl
half3 GetPunctualLights(float3 i_PositionWS, float2 i_PositionSS, const float4 i_ScreenPosition, const half3 i_Normal)
{
    half3 color = 0.0;

    BuiltinData builtinData;
    ZERO_INITIALIZE(BuiltinData, builtinData);

    LightLoopContext context;
    context.sampleReflection  = 0;
    context.shadowContext     = InitShadowContext();
    context.contactShadow     = 0;
    context.contactShadowFade = 0.0;
    context.shadowValue       = 1;
#if UNITY_VERSION < 60000000
    context.splineVisibility  = -1;
#endif
#ifdef APPLY_FOG_ON_SKY_REFLECTIONS
    context.positionWS        = i_PositionWS;
#endif

    float3 positionWS = GetCameraRelativePositionWS(i_PositionWS);
    ApplyCameraRelativeXR(positionWS);

    PositionInputs posInput;
    ZERO_INITIALIZE(PositionInputs, posInput);

    posInput.tileCoord = uint2(i_PositionSS) / GetTileSize();
    posInput.positionWS = positionWS;
    posInput.positionSS = i_PositionSS;
    posInput.positionNDC = i_ScreenPosition.xy / i_ScreenPosition.w;

    const uint renderingLayers = GetMeshRenderingLayerMask();

    uint lightCount, lightStart;

#ifndef LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
    GetCountAndStart(posInput, LIGHTCATEGORY_PUNCTUAL, lightStart, lightCount);
#else   // LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
    lightCount = _PunctualLightCount;
    lightStart = 0;
#endif

    bool fastPath = false;
#if SCALARIZE_LIGHT_LOOP
    uint lightStartLane0;
    fastPath = IsFastPath(lightStart, lightStartLane0);

    if (fastPath)
    {
        lightStart = lightStartLane0;
    }
#endif

    // Scalarized loop. All lights that are in a tile/cluster touched by any pixel in the wave are loaded (scalar load), only the one relevant to current thread/pixel are processed.
    // For clarity, the following code will follow the convention: variables starting with s_ are meant to be wave uniform (meant for scalar register),
    // v_ are variables that might have different value for each thread in the wave (meant for vector registers).
    // This will perform more loads than it is supposed to, however, the benefits should offset the downside, especially given that light data accessed should be largely coherent.
    // Note that the above is valid only if wave intriniscs are supported.
    uint v_lightListOffset = 0;
    uint v_lightIdx = lightStart;

#if NEED_TO_CHECK_HELPER_LANE
    // On some platform helper lanes don't behave as we'd expect, therefore we prevent them from entering the loop altogether.
    // IMPORTANT! This has implications if ddx/ddy is used on results derived from lighting, however given Lightloop is called in compute we should be
    // sure it will not happen.
    bool isHelperLane = WaveIsHelperLane();
    while (!isHelperLane && v_lightListOffset < lightCount)
#else
    while (v_lightListOffset < lightCount)
#endif
    {
        // Causes flickering. Manually increment below.
        // v_lightIdx = FetchIndex(lightStart, v_lightListOffset);

#if SCALARIZE_LIGHT_LOOP
        uint s_lightIdx = ScalarizeElementIndex(v_lightIdx, fastPath);
#else
        uint s_lightIdx = v_lightIdx;
#endif
        if (s_lightIdx == -1)
        {
            break;
        }

        LightData s_light = FetchLight(s_lightIdx);

        // If current scalar and vector light index match, we process the light. The v_lightListOffset for current thread is increased.
        // Note that the following should really be ==, however, since helper lanes are not considered by WaveActiveMin, such helper lanes could
        // end up with a unique v_lightIdx value that is smaller than s_lightIdx hence being stuck in a loop. All the active lanes will not have this problem.
        if (s_lightIdx >= v_lightIdx)
        {
            v_lightListOffset++;
            // Manually increment.
            v_lightIdx += 1;

            if (IsMatchingLightLayer(s_light.lightLayers, renderingLayers))
            {
                float3 L; float4 distances; // {d, d^2, 1/d, d_proj}
                GetPunctualLightVectors(positionWS, s_light, L, distances);

                // Is it worth evaluating the light?
                if (s_light.lightDimmer > 0)
                {
                    float4 lightColor = EvaluateLight_Punctual(context, posInput, s_light, L, distances);
                    lightColor.rgb *= lightColor.a;

                    SHADOW_TYPE shadow = EvaluateShadow_Punctual(context, posInput, s_light, builtinData, i_Normal, L, distances);

                    lightColor.rgb *= ComputeShadowColor(shadow, s_light.shadowTint, s_light.penumbraTint);

                    color += lightColor.rgb;
                }
            }
        }
    }

    return color;
}

#endif // CREST_HDRP_FORWARD_PASS

void CrestNodeLightData_half
(
	const float3 i_positionWS,
	const float4 i_screenPosition,
	out half3 o_direction,
	out half3 o_colour,
	out half3 o_additionalLight
)
{
	o_additionalLight = 0.0;

#ifdef SHADERGRAPH_PREVIEW
	// Hardcoded data, used for the preview shader inside the graph where light functions are not available.
	o_direction = -normalize(float3(-0.5, 0.5, -0.5));
	o_colour = float3(1.0, 1.0, 1.0);
#else
#if CREST_HDRP
	// Manually drive these in HDRP as I don't think there is a nicer way to do this yet.
	o_direction = _PrimaryLightDirection;
	o_colour = _PrimaryLightIntensity;
#if CREST_HDRP_FORWARD_PASS
	o_additionalLight = GetPunctualLights(i_positionWS, (i_screenPosition.xy / i_screenPosition.w) * _ScreenSize.xy, i_screenPosition, half3(0, 1, 0));
#endif
#else
	// Actual light data from the pipeline.
	Light light = GetMainLight();
	o_direction = light.direction;
	o_colour = light.color;
#if CREST_URP
	o_additionalLight = WaveHarmonic::Crest::AdditionalSoftLighting(i_positionWS, i_screenPosition);
#endif
#endif // CREST_HDRP
#endif // SHADERGRAPH_PREVIEW
}

#ifdef d_Crest_LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
#undef LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
#endif

#endif
