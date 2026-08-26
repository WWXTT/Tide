#ifndef UNITY_BLUENOISE_INCLUDED
#define UNITY_BLUENOISE_INCLUDED

Texture2DArray<float>  _STBNVec1Texture;
Texture2DArray<float2> _STBNVec2Texture;
Texture2DArray<float3> _STBNUnitVec3CosineTexture;

int _STBNIndex;

float GetSpatiotemporalBlueNoiseVec1(uint2 pixelCoord)
{
    return _STBNVec1Texture[uint3(pixelCoord.x % 128, pixelCoord.y % 128, _STBNIndex)].x;
}

float2 GetSpatiotemporalBlueNoiseVec2(uint2 pixelCoord)
{
    return _STBNVec2Texture[uint3(pixelCoord.x % 128, pixelCoord.y % 128, _STBNIndex)].xy;
}

float3 GetSpatiotemporalBlueNoiseUnitVec3Cosine(uint2 pixelCoord, int indexOffset = 0)
{
    float3 rayDir = _STBNUnitVec3CosineTexture[uint3(pixelCoord.x % 128, pixelCoord.y % 128, (_STBNIndex + indexOffset) % 64)].xyz;
    rayDir = rayDir * 2.0 - 1.0;
    return normalize(rayDir);
}

#endif //UNITY_BLUENOISE_INCLUDED