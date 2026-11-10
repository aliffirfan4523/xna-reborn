using XnaReborn.Migrator.Content;

namespace XnaReborn.Tests.Migrator.Content;

/// <summary>Processor parameter mapping.</summary>
[Trait("Category", "Migrator")]
public class ProcessorParamMapperTests
{
    [Fact]
    public void Empty_input_gives_empty_output()
    {
        var result = ProcessorParamMapper.Map("TextureImporter", "TextureProcessor", new Dictionary<string, string>());

        Assert.Empty(result.Params);
        Assert.Empty(result.Dropped);
    }

    [Fact]
    public void Texture_params_pass_through_sorted_with_values_unchanged()
    {
        var input = new Dictionary<string, string>
        {
            ["TextureFormat"] = "Color",
            ["PremultiplyAlpha"] = "True",
            ["MakeSquare"] = "false",
            ["ColorKeyColor"] = "255,0,255,255",
            ["ColorKeyEnabled"] = "False",
            ["GenerateMipmaps"] = "True",
            ["ResizeToPowerOfTwo"] = "True",
        };

        var result = ProcessorParamMapper.Map("TextureImporter", "TextureProcessor", input);

        Assert.Equal(
            ["ColorKeyColor", "ColorKeyEnabled", "GenerateMipmaps", "MakeSquare", "PremultiplyAlpha", "ResizeToPowerOfTwo", "TextureFormat"],
            result.Params.Select(p => p.Key));
        Assert.All(result.Params, p => Assert.Equal(input[p.Key], p.Value));
        Assert.Empty(result.Dropped);
    }

    [Theory]
    [InlineData("FontDescriptionProcessor", "PremultiplyAlpha")]
    [InlineData("FontDescriptionProcessor", "TextureFormat")]
    [InlineData("SoundEffectProcessor", "Quality")]
    [InlineData("SongProcessor", "Quality")]
    [InlineData("ModelProcessor", "RotationX")]
    [InlineData("ModelProcessor", "RotationY")]
    [InlineData("ModelProcessor", "RotationZ")]
    [InlineData("ModelProcessor", "Scale")]
    [InlineData("ModelProcessor", "SwapWindingOrder")]
    [InlineData("ModelProcessor", "GenerateTangentFrames")]
    [InlineData("ModelProcessor", "DefaultEffect")]
    [InlineData("ModelProcessor", "ColorKeyEnabled")]
    [InlineData("ModelProcessor", "PremultiplyTextureAlpha")]
    [InlineData("EffectProcessor", "Defines")]
    [InlineData("EffectProcessor", "DebugMode")]
    public void Supported_params_are_kept(string processor, string name)
    {
        var result = ProcessorParamMapper.Map(null, processor, new Dictionary<string, string> { [name] = "Best" });

        Assert.Equal(new KeyValuePair<string, string>(name, "Best"), Assert.Single(result.Params));
        Assert.Empty(result.Dropped);
    }

    [Fact]
    public void Unknown_params_and_unknown_processors_pass_through()
    {
        var input = new Dictionary<string, string> { ["Outline"] = "3" };

        Assert.Single(ProcessorParamMapper.Map("TextureImporter", "OutlineProcessor", input).Params);
        Assert.Single(ProcessorParamMapper.Map(null, null, input).Params);
        Assert.Single(ProcessorParamMapper.Map(null, "TextureProcessor", input).Params);
    }

    [Fact]
    public void Font_default_character_is_dropped_with_a_reason()
    {
        var input = new Dictionary<string, string> { ["DefaultCharacter"] = "?", ["PremultiplyAlpha"] = "True" };

        var result = ProcessorParamMapper.Map("FontDescriptionImporter", "FontDescriptionProcessor", input);

        Assert.Equal("PremultiplyAlpha", Assert.Single(result.Params).Key);
        var dropped = Assert.Single(result.Dropped);
        Assert.StartsWith("DefaultCharacter: ", dropped);
        Assert.Contains("spritefont", dropped);
    }

    [Fact]
    public void Effect_default_effect_is_dropped_but_model_default_effect_is_kept()
    {
        var input = new Dictionary<string, string> { ["DefaultEffect"] = "BasicEffect" };

        var effect = ProcessorParamMapper.Map("EffectImporter", "EffectProcessor", input);
        var model = ProcessorParamMapper.Map("FbxImporter", "ModelProcessor", input);

        Assert.Empty(effect.Params);
        Assert.StartsWith("DefaultEffect: ", Assert.Single(effect.Dropped));
        Assert.Single(model.Params);
        Assert.Empty(model.Dropped);
    }

    [Fact]
    public void Processor_name_ignores_case_but_param_name_does_not()
    {
        var lower = ProcessorParamMapper.Map(null, "effectprocessor", new Dictionary<string, string> { ["DefaultEffect"] = "x" });
        var wrongCase = ProcessorParamMapper.Map(null, "EffectProcessor", new Dictionary<string, string> { ["defaulteffect"] = "x" });

        Assert.Single(lower.Dropped);
        Assert.Single(wrongCase.Params);
    }

    [Fact]
    public void Mapping_is_sorted_and_deterministic()
    {
        var input = new Dictionary<string, string> { ["PremultiplyAlpha"] = "True", ["DefaultCharacter"] = "?", ["TextureFormat"] = "Color" };

        var first = ProcessorParamMapper.Map(null, "FontDescriptionProcessor", input);
        var second = ProcessorParamMapper.Map(null, "FontDescriptionProcessor", input);

        Assert.Equal(first.Params, second.Params);
        Assert.Equal(first.Dropped, second.Dropped);
        Assert.Equal(["PremultiplyAlpha", "TextureFormat"], first.Params.Select(p => p.Key));
    }

    [Fact]
    public void Null_params_throw()
    {
        Assert.Throws<ArgumentNullException>(() => ProcessorParamMapper.Map(null, null, null!));
    }
}
