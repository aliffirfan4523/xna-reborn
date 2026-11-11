#include "Common.fxh"

float4 MainVS(float4 position : POSITION0) : POSITION0
{
    return Transform(position);
}

float4 MainPS() : COLOR0
{
    return float4(1, 0, 1, 1);
}

technique WithInclude
{
    pass P0
    {
        VertexShader = compile vs_2_0 MainVS();
        PixelShader = compile ps_2_0 MainPS();
    }
}