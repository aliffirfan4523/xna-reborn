// Original effect written for this fixture. Toolkit syntax: no "compile" keyword.
Texture2D<float4> Texture;
SamplerState PointSampler
{
    Filter = MIN_MAG_MIP_POINT;
};

float4 PSTint(float4 position : SV_POSITION, float2 uv : TEXCOORD0) : SV_TARGET
{
    return Texture.Sample(PointSampler, uv) * float4(1.0, 0.5, 0.5, 1.0);
}

technique Tint
{
    pass P0
    {
        Profile = 9.1;
        PixelShader = PSTint;
    }
}