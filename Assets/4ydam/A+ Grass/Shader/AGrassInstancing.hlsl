#ifndef AGRASS_INSTANCING_INCLUDED
#define AGRASS_INSTANCING_INCLUDED

TEXTURE2D(_AGrassInteractionMap);
SAMPLER(sampler_AGrassInteractionMap);
float2 _AGrassMapCenter;
float _AGrassMapSize;
float _AGrassMapTexelSize;

float3 AGrassComputeInteractionOffset(float3 worldPos, float4 vertexColor)
{
    float2 uv = (worldPos.xz - _AGrassMapCenter + _AGrassMapSize * 0.5) / _AGrassMapSize;

    float2 inside = step(float2(0, 0), uv) * step(uv, float2(1, 1));
    if (inside.x * inside.y < 0.5)
        return float3(0, 0, 0);

    float heightMask = vertexColor.r;
    float bendFactor = heightMask * heightMask;

    float strength = SAMPLE_TEXTURE2D_LOD(_AGrassInteractionMap, sampler_AGrassInteractionMap, uv, 0).r;

    if (strength < 0.001)
        return float3(0, 0, 0);

    float ts = _AGrassMapTexelSize;
    float sL = SAMPLE_TEXTURE2D_LOD(_AGrassInteractionMap, sampler_AGrassInteractionMap, uv - float2(ts, 0), 0).r;
    float sR = SAMPLE_TEXTURE2D_LOD(_AGrassInteractionMap, sampler_AGrassInteractionMap, uv + float2(ts, 0), 0).r;
    float sD = SAMPLE_TEXTURE2D_LOD(_AGrassInteractionMap, sampler_AGrassInteractionMap, uv - float2(0, ts), 0).r;
    float sU = SAMPLE_TEXTURE2D_LOD(_AGrassInteractionMap, sampler_AGrassInteractionMap, uv + float2(0, ts), 0).r;

    float2 grad = float2(sR - sL, sU - sD);
    float gradLen = length(grad);
    float2 bendDir = gradLen > 0.001 ? -grad / gradLen : float2(0, 0);

    float bendAmount = strength * _InteractionStrength * bendFactor * _InteractionMultiplier;

    float3 offset;
    offset.x = bendDir.x * bendAmount;
    offset.z = bendDir.y * bendAmount;
    offset.y = -strength * _PushDownAmount * bendFactor * _InteractionMultiplier;
    offset.y -= length(offset.xz) * 0.3;

    return offset;
}

float AGrassGetInteractionStrength(float3 worldPos, float4 vertexColor)
{
    float2 uv = (worldPos.xz - _AGrassMapCenter + _AGrassMapSize * 0.5) / _AGrassMapSize;

    float2 inside = step(float2(0, 0), uv) * step(uv, float2(1, 1));
    if (inside.x * inside.y < 0.5)
        return 0.0;

    float strength = SAMPLE_TEXTURE2D_LOD(_AGrassInteractionMap, sampler_AGrassInteractionMap, uv, 0).r;
    return strength;
}

#endif
