float4 MainPS() : COLOR0
{
    return float4(1, 1, 1, 0.5);
}

technique AlphaTest
{
    pass P0
    {
        AlphaBlendEnable = true;
        SrcBlend = SrcAlpha;
        DestBlend = InvSrcAlpha;
        AlphaTestEnable = true;
        AlphaFunc = Greater;
        AlphaRef = 128;
        PixelShader = compile ps_2_0 MainPS();
    }
}