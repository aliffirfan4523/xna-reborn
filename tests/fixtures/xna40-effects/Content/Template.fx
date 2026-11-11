#define VS_SHADERMODEL vs_2_0
#define PS_SHADERMODEL ps_2_0

float4x4 WorldViewProjection;
float4 Tint = float4(1, 1, 1, 1);

float4 MainVS(float4 position : POSITION0) : POSITION0
{
    return mul(position, WorldViewProjection);
}

float4 MainPS() : COLOR0
{
    return Tint;
}

technique Template
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}