texture Tex;
sampler2D TexSampler = sampler_state
{
    Texture = <Tex>;
    MinFilter = GaussianQuad;
    MagFilter = PyramidalQuad;
    AddressU = Wrap;
    AddressV = Wrap;
};

float4 MainPS(float2 uv : TEXCOORD0) : COLOR0
{
    return tex2D(TexSampler, uv);
}

technique Gaussian
{
    pass P0
    {
        PixelShader = compile ps_2_0 MainPS();
    }
}