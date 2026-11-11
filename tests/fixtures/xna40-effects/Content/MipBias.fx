texture Tex;
sampler2D TexSampler = sampler_state
{
    Texture = <Tex>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    MipMapLodBias = -1;
};

float4 MainPS(float2 uv : TEXCOORD0) : COLOR0
{
    return tex2D(TexSampler, uv);
}

technique MipBias
{
    pass P0
    {
        PixelShader = compile ps_2_0 MainPS();
    }
}