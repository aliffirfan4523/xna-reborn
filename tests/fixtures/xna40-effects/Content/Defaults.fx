float4 Color = float4(0.2, 0.4, 0.6, 1);
float Unused;
float4x4 AlsoUnused;

float4 MainPS() : COLOR0
{
    return Color;
}

technique Defaults
{
    pass P0
    {
        PixelShader = compile ps_2_0 MainPS();
    }
}