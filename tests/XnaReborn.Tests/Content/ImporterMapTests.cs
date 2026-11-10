using XnaReborn.Core.Content;

namespace XnaReborn.Tests.Content;

/// <summary>The shared importer and processor map and its stock/custom judgement.</summary>
[Trait("Category", "Content")]
public sealed class ImporterMapTests
{
    private const string _valid = """
        {"version":1,
         "pairs":[{"importer":"TextureImporter","processor":"TextureProcessor"},
                  {"importer":"EffectImporter","processor":"EffectProcessor","note":"n"}],
         "stockUnsupported":[{"importer":"XactImporter","processor":"XactProcessor","note":"no MonoGame"}]}
        """;

    /// <summary>The embedded map has the key stock pairs, the XACT exception and a source list.</summary>
    [Fact]
    public void Default_LoadsEmbeddedMap()
    {
        var map = ImporterMap.Default;

        Assert.Same(map, ImporterMap.Default);
        Assert.True(map.TryGetPair("TextureImporter", "TextureProcessor", out _));
        Assert.True(map.TryGetPair("FbxImporter", "ModelProcessor", out _));
        Assert.True(map.TryGetPair("XImporter", "ModelProcessor", out _));
        Assert.True(map.TryGetPair("EffectImporter", "EffectProcessor", out _));
        Assert.True(map.TryGetPair("FontDescriptionImporter", "FontDescriptionProcessor", out _));
        Assert.True(map.TryGetPair("XmlImporter", "PassThroughProcessor", out _));
        Assert.True(map.TryGetPair("Mp3Importer", "SongProcessor", out _));
        Assert.True(map.TryGetPair("WavImporter", "SoundEffectProcessor", out _));
        Assert.True(map.TryGetPair("WmaImporter", "SongProcessor", out _));
        Assert.True(map.TryGetPair("WmvImporter", "VideoProcessor", out _));
        var xact = Assert.Single(map.StockUnsupported);
        Assert.Equal(("XactImporter", "XactProcessor"), (xact.Importer, xact.Processor));
        Assert.False(map.TryGetPair("XactImporter", "XactProcessor", out _));
        Assert.DoesNotContain(map.Pairs, p => p.Processor == "SkinnedModelProcessor");
    }

    /// <summary>The map file in the repository parses and lists its sources.</summary>
    [Fact]
    public void RepositoryFile_HasSources()
    {
        var path = Path.GetFullPath(Path.Combine(Tests.Fixtures.FixturePaths.Root, "..", "..", "rules", "importer-map.json"));
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));

        var sources = document.RootElement.GetProperty("sources").EnumerateArray().Select(e => e.GetString()).ToList();

        Assert.NotEmpty(sources);
        Assert.All(sources, s => Assert.StartsWith("https://", s, StringComparison.Ordinal));
        Assert.Equal(ImporterMap.Default.Pairs.Count, ImporterMap.Parse(File.ReadAllText(path)).Pairs.Count);
    }

    /// <summary>A well-formed map parses; note is optional and stockUnsupported may be absent.</summary>
    [Fact]
    public void Parse_ValidMap_ReadsPairs()
    {
        var map = ImporterMap.Parse(_valid);

        Assert.Equal(2, map.Pairs.Count);
        Assert.Null(map.Pairs[0].Note);
        Assert.Equal("n", map.Pairs[1].Note);
        Assert.Single(map.StockUnsupported);
        Assert.Empty(ImporterMap.Parse("""{"version":1,"pairs":[]}""").StockUnsupported);
    }

    /// <summary>Bad JSON, a wrong version or a wrong shape throws FormatException with a clear message.</summary>
    /// <param name="json">The map text.</param>
    /// <param name="message">A part of the expected message.</param>
    [Theory]
    [InlineData("not json", "not valid JSON")]
    [InlineData("[]", "must be a JSON object")]
    [InlineData("""{"pairs":[]}""", "'version' must be 1")]
    [InlineData("""{"version":2,"pairs":[]}""", "'version' must be 1")]
    [InlineData("""{"version":"1","pairs":[]}""", "'version' must be 1")]
    [InlineData("""{"version":1}""", "missing 'pairs'")]
    [InlineData("""{"version":1,"pairs":{}}""", "'pairs' must be an array")]
    [InlineData("""{"version":1,"pairs":[1]}""", "'pairs[0]' must be an object")]
    [InlineData("""{"version":1,"pairs":[{"processor":"P"}]}""", "needs 'importer'")]
    [InlineData("""{"version":1,"pairs":[{"importer":"I"}]}""", "needs 'processor'")]
    [InlineData("""{"version":1,"pairs":[{"importer":"","processor":"P"}]}""", "non-empty string")]
    [InlineData("""{"version":1,"pairs":[{"importer":"I","processor":3}]}""", "non-empty string")]
    [InlineData("""{"version":1,"pairs":[{"importer":"I","processor":"P","note":5}]}""", "pairs[0].note")]
    [InlineData("""{"version":1,"pairs":[],"stockUnsupported":"x"}""", "'stockUnsupported' must be an array")]
    public void Parse_BadInput_Throws(string json, string message)
    {
        var exception = Assert.Throws<FormatException>(() => ImporterMap.Parse(json));

        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Null text is an argument error.</summary>
    [Fact]
    public void Parse_Null_Throws() => Assert.Throws<ArgumentNullException>(() => ImporterMap.Parse(null!));

    /// <summary>IsStock and IsCustom over importer-only, processor-only, both, mismatched and null inputs.</summary>
    /// <param name="importer">The importer name.</param>
    /// <param name="processor">The processor name.</param>
    /// <param name="stock">The expected IsStock result.</param>
    /// <param name="custom">The expected IsCustom result.</param>
    [Theory]
    [InlineData(null, null, true, false)]
    [InlineData("TextureImporter", null, true, false)]
    [InlineData("MyImporter", null, false, true)]
    [InlineData(null, "TextureProcessor", true, false)]
    [InlineData(null, "OutlineProcessor", false, true)]
    [InlineData(null, "SkinnedModelProcessor", false, true)]
    [InlineData("TextureImporter", "TextureProcessor", true, false)]
    [InlineData("TextureImporter", "OutlineProcessor", false, true)]
    [InlineData("MyImporter", "TextureProcessor", false, true)]
    [InlineData("TextureImporter", "ModelProcessor", false, true)]
    [InlineData("FbxImporter", "TextureProcessor", false, true)]
    [InlineData("XactImporter", "XactProcessor", true, false)]
    [InlineData("XactImporter", null, true, false)]
    [InlineData("XactImporter", "TextureProcessor", false, true)]
    [InlineData("texturEIMPORTER", "textureprocessor", true, false)]
    [InlineData("TEXTUREIMPORTER", "outlineprocessor", false, true)]
    [InlineData("", null, false, true)]
    public void IsStock_And_IsCustom_Matrix(string? importer, string? processor, bool stock, bool custom)
    {
        Assert.Equal(stock, ImporterMap.Default.IsStock(importer, processor));
        Assert.Equal(custom, ImporterMap.Default.IsCustom(importer, processor));
    }

    /// <summary>TryGetPair ignores case and returns the entry with its note.</summary>
    [Fact]
    public void TryGetPair_IgnoresCase_AndReturnsEntry()
    {
        var map = ImporterMap.Parse(_valid);

        Assert.True(map.TryGetPair("effectimporter", "EFFECTPROCESSOR", out var pair));
        Assert.Equal("n", pair.Note);
        Assert.False(map.TryGetPair("EffectImporter", "TextureProcessor", out _));
        Assert.False(map.TryGetPair("XactImporter", "XactProcessor", out _));
    }
}
