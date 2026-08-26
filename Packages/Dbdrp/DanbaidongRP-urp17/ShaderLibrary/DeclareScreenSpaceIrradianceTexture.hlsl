#ifndef UNITY_DECLARE_SCREEN_SPACE_IRRADIANCE_TEXTURE_INCLUDED
#define UNITY_DECLARE_SCREEN_SPACE_IRRADIANCE_TEXTURE_INCLUDED
#include "Packages/com.unity.render-pipelines.danbaidong/ShaderLibrary/Core.hlsl"

TEXTURE2D_X_HALF(_ScreenSpaceIrradiance);

half3 SampleScreenSpaceIrradiance(float2 positionCS)
{
    float2 uv = positionCS * _ScreenParams.zw;
    return SAMPLE_TEXTURE2D_X(_ScreenSpaceIrradiance, sampler_PointClamp, UnityStereoTransformScreenSpaceTex(uv)).xyz;
}

#endif /* UNITY_DECLARE_SCREEN_SPACE_IRRADIANCE_TEXTURE_INCLUDED */
