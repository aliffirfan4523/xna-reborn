float4x4 WorldViewProjection;

float4 Transform(float4 position)
{
    return mul(position, WorldViewProjection);
}