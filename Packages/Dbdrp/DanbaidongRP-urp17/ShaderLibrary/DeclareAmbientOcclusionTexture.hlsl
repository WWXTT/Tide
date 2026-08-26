#ifndef UNITY_DECLARE_AMBIENT_OCCLUSION_TEXTURE_INCLUDED
#define UNITY_DECLARE_AMBIENT_OCCLUSION_TEXTURE_INCLUDED
#include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Core.hlsl"

TEXTURE2D_X_FLOAT(_AmbientOcclusionTexture);

float SampleSceneAmbientOcclusion(float2 uv)
{
    float ambientOcclusion = SAMPLE_TEXTURE2D_X(_AmbientOcclusionTexture, sampler_PointClamp, UnityStereoTransformScreenSpaceTex(uv)).r;
    return ambientOcclusion;
}

float LoadSceneAmbientOcclusion(uint2 coordSS)
{
    float ambientOcclusion = LOAD_TEXTURE2D_X(_AmbientOcclusionTexture, coordSS).r;
    return ambientOcclusion;
}

#endif /* UNITY_DECLARE_AMBIENT_OCCLUSION_TEXTURE_INCLUDED */
