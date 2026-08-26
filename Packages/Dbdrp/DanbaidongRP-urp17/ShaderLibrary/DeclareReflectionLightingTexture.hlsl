#ifndef UNITY_DECLARE_REFLECTION_LIGHTING_TEXTURE_INCLUDED
#define UNITY_DECLARE_REFLECTION_LIGHTING_TEXTURE_INCLUDED
#include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Core.hlsl"

TEXTURE2D_X_FLOAT(_ReflectionLightingTexture);

float SampleSceneReflectionLighting(float2 uv)
{
    float reflectionLighting = SAMPLE_TEXTURE2D_X(_ReflectionLightingTexture, sampler_PointClamp, UnityStereoTransformScreenSpaceTex(uv)).r;
    return reflectionLighting;
}

float LoadSceneReflectionLighting(uint2 coordSS)
{
    float reflectionLighting = LOAD_TEXTURE2D_X(_ReflectionLightingTexture, coordSS).r;
    return reflectionLighting;
}

#endif /* UNITY_DECLARE_REFLECTION_LIGHTING_TEXTURE_INCLUDED */