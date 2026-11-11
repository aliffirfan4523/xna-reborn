float4x4 World < string UIName = "World"; >;
float Strength = 1;

float4 MainPS(uniform bool useStrength) : COLOR0
{
    return useStrength ? float4(Strength, 0, 0, 1) : float4(0, 0, 0, 1);
}

technique Annotated
{
    pass P0
    {
        PixelShader = compile ps_2_0 MainPS(true);
    }
}

technique Legacy
{
    pass P0
    {
        SetPixelShader(CompileShader(ps_2_0, MainPS(false)));
    }
}