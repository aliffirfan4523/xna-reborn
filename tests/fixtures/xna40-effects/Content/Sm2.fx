float4x4 WorldViewProjection;
texture Tex;
sampler2D TexSampler = sampler_state
{
    Texture = <Tex>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VSInput { float4 Position : POSITION0; float2 UV : TEXCOORD0; };
struct VSOutput { float4 Position : POSITION0; float2 UV : TEXCOORD0; };

VSOutput MainVS(VSInput input)
{
    VSOutput output;
    output.Position = mul(input.Position, WorldViewProjection);
    output.UV = input.UV;
    return output;
}
float4 MainPS(VSOutput input) : COLOR0
{
    return tex2D(TexSampler, input.UV);
}

technique Sm2
{
    pass P0
    {
        VertexShader = compile vs_2_0 MainVS();
        PixelShader = compile ps_2_0 MainPS();
    }
}