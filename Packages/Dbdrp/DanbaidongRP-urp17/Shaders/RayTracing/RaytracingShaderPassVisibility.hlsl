
#ifndef RAYTRACING_SHADERPASS_VISIBILITY_INCLUDED
#define RAYTRACING_SHADERPASS_VISIBILITY_INCLUDED

// Generic function that handles the reflection code
[shader("closesthit")]
void ClosestHitMain(inout RayIntersectionVisibility rayIntersection : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
{
    // Make sure to add the additional travel distance
    rayIntersection.t = RayTCurrent();

    // Hit point data.
    // IntersectionVertex currentVertex;
    // FragInputs fragInput;
    // GetCurrentVertexAndBuildFragInputs(attributeData, currentVertex, fragInput);
    // PositionInputs posInput = GetPositionInput(rayIntersection.pixelCoord, _ScreenSize.zw, fragInput.positionRWS);

    rayIntersection.color.x = 0;
}

// Generic function that handles the reflection code
[shader("anyhit")]
void AnyHitVisibility(inout RayIntersectionVisibility rayIntersection : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
{
    // Hit point data.
    IntersectionVertex currentVertex;
    FragInputs fragInput;
    GetCurrentVertexAndBuildFragInputs(attributeData, currentVertex, fragInput);

    // Compute the distance of the ray
    rayIntersection.t = RayTCurrent();

    bool isVisible = true;
    #if defined(_ALPHATEST_ON)
    float2 uv = fragInput.texCoord0.xy;
    uv = TRANSFORM_TEX(uv, _BaseMap);
    float4 albedoAlpha = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 0);
    isVisible = (albedoAlpha.a - _Cutoff) > 0;
    #endif

    // If this point is not visible, ignore the hit and force end the shader
    if (!isVisible)
    {
        IgnoreHit();
        return;
    }
    else
    {
        // If this fella is opaque, then we need to stop
        rayIntersection.color = float3(0.0, 0.0, 0.0);
        AcceptHitAndEndSearch();
    }
}

#endif /* RAYTRACING_SHADERPASS_VISIBILITY_INCLUDED */