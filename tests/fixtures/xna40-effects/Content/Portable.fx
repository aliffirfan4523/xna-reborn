#if OPENGL
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 WorldViewProjection;

float4 MainVS(float4 position : POSITION0) : POSITION0
{
    return mul(position, WorldViewProjection);
}

float4 MainPS() : COLOR0
{
    return float4(0, 1, 0, 1);
}

technique Portable
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}