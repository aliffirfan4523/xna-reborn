float4 MainPS(float2 screen : VPOS) : COLOR0
{
    return float4(screen.x / 800.0, screen.y / 600.0, 0, 1);
}

technique Vpos
{
    pass P0
    {
        PixelShader = compile ps_3_0 MainPS();
    }
}