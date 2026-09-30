//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef SHADERVARIABLESSCREENSPACEAMBIENTOCCLUSION_CS_HLSL
#define SHADERVARIABLESSCREENSPACEAMBIENTOCCLUSION_CS_HLSL
// Generated from UnityEngine.Rendering.Universal.ShaderVariablesScreenSpaceAmbientOcclusion
// PackingRules = Exact
CBUFFER_START(ShaderVariablesScreenSpaceAmbientOcclusion)
    float4 _NDCToViewParams;
    float _Intensity;
    float _DirectLightingStrength;
    float _Radius;
    float _RadiusMultiplier;
    float _FalloffRange;
    float _SampleDistributionPower;
    float _ThinOccluderCompensation;
    float _FinalValuePower;
    float _DepthMIPSamplingOffset;
CBUFFER_END


#endif
