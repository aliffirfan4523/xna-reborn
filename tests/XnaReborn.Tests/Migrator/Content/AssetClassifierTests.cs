using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Content;
using XnaReborn.Core.Models;
using XnaReborn.Migrator.Content;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator.Content;

/// <summary>Classification of content items, one test per rule.</summary>
[Trait("Category", "Migrator")]
public class AssetClassifierTests
{
    private static readonly string _project = Path.Combine(Path.GetTempPath(), "classifier-fake", "Content", "Content.contentproj");
    private static readonly string _dir = Path.GetDirectoryName(_project)!;
    private static readonly ImporterMap _map = ImporterMap.Default;

    private static ContentItem Item(string path, string? importer = null, string? processor = null) =>
        new(path, null, importer, processor, new Dictionary<string, string>());

    private static ConvertedAsset Classify(ContentItem item, bool conflict = false, params string[] existing)
    {
        var fs = new FakeFileSystem(existing.Select(e => Path.Combine(_dir, e.Replace('/', Path.DirectorySeparatorChar))));
        return new AssetClassifier(fs).Classify(item, "name", _project, _map, conflict);
    }

    [Fact]
    public void Rule1_missing_source_wins_over_everything()
    {
        var result = Classify(Item("a.xap", "XactImporter", "XactProcessor"), conflict: true);

        Assert.Equal(AssetStatus.Missing, result.Status);
        Assert.Contains("a.xap", result.Note);
        Assert.Equal("XactImporter", result.MonoGameImporter);
        Assert.Equal("XactProcessor", result.MonoGameProcessor);
    }

    [Fact]
    public void Rule1_missing_source_without_importer_gives_empty_names()
    {
        var result = Classify(Item("a.png"));

        Assert.Equal(AssetStatus.Missing, result.Status);
        Assert.Equal(string.Empty, result.MonoGameImporter);
        Assert.Equal(string.Empty, result.MonoGameProcessor);
    }

    [Fact]
    public void Rule2_conflict_wins_over_later_rules()
    {
        var result = Classify(Item("a.xap", "XactImporter", "XactProcessor"), conflict: true, "a.xap");

        Assert.Equal(AssetStatus.Conflict, result.Status);
        Assert.Contains("Both items are kept", result.Note);
    }

    [Theory]
    [InlineData("a.xap")]
    [InlineData("a.xgs")]
    [InlineData("a.xwb")]
    [InlineData("a.XSB")]
    public void Rule3_xact_extensions_are_manual_work(string path)
    {
        var result = Classify(Item(path), conflict: false, path);

        Assert.Equal(AssetStatus.ManualWork, result.Status);
        Assert.Contains("re-export", result.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rule3_unsupported_stock_pair_is_manual_work()
    {
        var result = Classify(Item("sounds.bin", "XactImporter", "XactProcessor"), conflict: false, "sounds.bin");

        Assert.Equal(AssetStatus.ManualWork, result.Status);
        Assert.Contains("XACT", result.Note);
    }

    [Fact]
    public void Rule4_custom_processor_is_manual_work_and_keeps_names()
    {
        var result = Classify(Item("ship.png", "TextureImporter", "OutlineProcessor"), conflict: false, "ship.png");

        Assert.Equal(AssetStatus.ManualWork, result.Status);
        Assert.Contains("custom pipeline assembly", result.Note);
        Assert.Equal("OutlineProcessor", result.MonoGameProcessor);
    }

    [Fact]
    public void Rule4_custom_importer_is_manual_work()
    {
        var result = Classify(Item("level.lvl", "LevelImporter", "PassThroughProcessor"), conflict: false, "level.lvl");

        Assert.Equal(AssetStatus.ManualWork, result.Status);
        Assert.Contains("custom pipeline assembly", result.Note);
    }

    [Fact]
    public void Rule5_xnb_without_importer_and_source_is_prebuilt()
    {
        var result = Classify(Item("Sounds/boom.xnb"), conflict: false, "Sounds/boom.xnb");

        Assert.Equal(AssetStatus.Prebuilt, result.Status);
        Assert.Contains("cannot be rebuilt", result.Note);
        Assert.Contains("not guaranteed", result.Note);
    }

    [Fact]
    public void Rule5_xnb_next_to_a_source_of_the_same_name_is_not_prebuilt()
    {
        var result = Classify(Item("Sounds/boom.xnb"), conflict: false, "Sounds/boom.xnb", "Sounds/boom.wav");

        Assert.Equal(AssetStatus.ManualWork, result.Status);
        Assert.Equal("Unknown asset type", result.Note);
    }

    [Theory]
    [InlineData("a.png", "TextureImporter", "TextureProcessor")]
    [InlineData("a.JPG", "TextureImporter", "TextureProcessor")]
    [InlineData("a.jpeg", "TextureImporter", "TextureProcessor")]
    [InlineData("a.bmp", "TextureImporter", "TextureProcessor")]
    [InlineData("a.tga", "TextureImporter", "TextureProcessor")]
    [InlineData("a.dds", "TextureImporter", "TextureProcessor")]
    [InlineData("a.spritefont", "FontDescriptionImporter", "FontDescriptionProcessor")]
    [InlineData("a.wav", "WavImporter", "SoundEffectProcessor")]
    [InlineData("a.mp3", "Mp3Importer", "SongProcessor")]
    [InlineData("a.wma", "WmaImporter", "SongProcessor")]
    [InlineData("a.fbx", "FbxImporter", "ModelProcessor")]
    [InlineData("a.x", "XImporter", "ModelProcessor")]
    [InlineData("a.fx", "EffectImporter", "EffectProcessor")]
    [InlineData("a.xml", "XmlImporter", "PassThroughProcessor")]
    public void Rule6_names_are_inferred_from_the_extension(string path, string importer, string processor)
    {
        var inferred = Classify(Item(path), conflict: false, path);
        var explicitPair = Classify(Item(path, importer, processor), conflict: false, path);

        foreach (var result in new[] { inferred, explicitPair })
        {
            Assert.Equal(AssetStatus.Converted, result.Status);
            Assert.Equal(importer, result.MonoGameImporter);
            Assert.Equal(processor, result.MonoGameProcessor);
        }
    }

    [Fact]
    public void Rule6_missing_processor_is_inferred_from_the_extension()
    {
        var result = Classify(Item("a.png", "TextureImporter"), conflict: false, "a.png");

        Assert.Equal(AssetStatus.Converted, result.Status);
        Assert.Equal("TextureProcessor", result.MonoGameProcessor);
    }

    [Fact]
    public void Rule6_stock_importer_with_unknown_extension_and_no_processor_is_unknown()
    {
        var result = Classify(Item("a.xyz", "TextureImporter"), conflict: false, "a.xyz");

        Assert.Equal(AssetStatus.ManualWork, result.Status);
        Assert.Equal("Unknown asset type", result.Note);
    }

    [Fact]
    public void Rule6_stock_pair_on_an_unusual_extension_is_converted()
    {
        var result = Classify(Item("a.hdr", "TextureImporter", "TextureProcessor"), conflict: false, "a.hdr");

        Assert.Equal(AssetStatus.Converted, result.Status);
        Assert.Null(result.Note);
    }

    [Fact]
    public void Rule6_inferred_name_clashing_with_the_set_one_is_manual_work()
    {
        var result = Classify(Item("a.png", "WavImporter"), conflict: false, "a.png");

        Assert.Equal(AssetStatus.ManualWork, result.Status);
        Assert.Contains("not a stock pair", result.Note);
    }

    [Fact]
    public void Rule6_fx_gets_the_shader_note_and_stays_converted()
    {
        var result = Classify(Item("Shaders/Glow.fx", "EffectImporter", "EffectProcessor"), conflict: false, "Shaders/Glow.fx");

        Assert.Equal(AssetStatus.Converted, result.Status);
        Assert.Equal("Shader review needed.", result.Note);
    }

    [Theory]
    [InlineData("a.unknown")]
    [InlineData("noext")]
    public void Rule7_unknown_type_without_importer_is_manual_work(string path)
    {
        var result = Classify(Item(path), conflict: false, path);

        Assert.Equal(AssetStatus.ManualWork, result.Status);
        Assert.Equal("Unknown asset type", result.Note);
    }

    [Fact]
    public void Result_carries_item_and_asset_name()
    {
        var item = Item("a.png");

        var result = Classify(item, conflict: false, "a.png");

        Assert.Same(item, result.Item);
        Assert.Equal("name", result.AssetName);
    }

    [Fact]
    public void Classification_is_deterministic()
    {
        var item = Item("a.fx");

        Assert.Equal(Classify(item, false, "a.fx"), Classify(item, false, "a.fx"));
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var classifier = new AssetClassifier(new FakeFileSystem([]));
        var item = Item("a.png");

        Assert.Throws<ArgumentNullException>(() => new AssetClassifier(null!));
        Assert.Throws<ArgumentNullException>(() => classifier.Classify(null!, "n", _project, _map, false));
        Assert.Throws<ArgumentNullException>(() => classifier.Classify(item, null!, _project, _map, false));
        Assert.Throws<ArgumentNullException>(() => classifier.Classify(item, "n", null!, _map, false));
        Assert.Throws<ArgumentNullException>(() => classifier.Classify(item, "n", _project, null!, false));
    }

    [Fact]
    public void Custom_content_fixture_is_classified_as_the_spec_says()
    {
        var root = FixturePaths.Of("xna40-custom-content");
        var projectPath = Path.Combine(root, "Content", "Content.contentproj");
        var items = AssetNameResolverTests.ReadFixture("xna40-custom-content");
        var resolution = AssetNameResolver.Resolve(items, "Content");
        var classifier = new AssetClassifier(new ReadOnlyFileSystem());

        var assets = resolution.Names
            .Select(n => classifier.Classify(n.Item, n.AssetName, projectPath, _map, false))
            .ToDictionary(a => a.Item.SourcePath);

        var hero = assets["Textures/hero.png"];
        Assert.Equal(AssetStatus.Converted, hero.Status);
        Assert.Equal("Textures/hero", hero.AssetName);
        Assert.Equal(AssetStatus.ManualWork, assets["Textures/ship.png"].Status);
        Assert.Equal(AssetStatus.ManualWork, assets["Audio/Sounds.xap"].Status);
        Assert.Equal(AssetStatus.Converted, assets["Shaders/Glow.fx"].Status);
        Assert.Equal("Shader review needed.", assets["Shaders/Glow.fx"].Note);
        Assert.Equal(AssetStatus.Converted, assets["Fonts/Hud.spritefont"].Status);
    }

    [Fact]
    public void Basic_fixture_items_are_all_converted()
    {
        var root = FixturePaths.Of("xna40-basic");
        var projectPath = Path.Combine(root, "Content", "Content.contentproj");
        var classifier = new AssetClassifier(new ReadOnlyFileSystem());

        foreach (var item in AssetNameResolverTests.ReadFixture("xna40-basic"))
        {
            Assert.Equal(AssetStatus.Converted, classifier.Classify(item, "n", projectPath, _map, false).Status);
        }
    }

    private sealed class FakeFileSystem(IEnumerable<string> files) : IReadOnlyFileSystem
    {
        private readonly HashSet<string> _files = new(files, StringComparer.OrdinalIgnoreCase);

        public bool FileExists(string path) => _files.Contains(path);

        public bool DirectoryExists(string path) => _files.Any(f => Path.GetDirectoryName(f) == path);

        public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern) =>
            _files.Where(f => string.Equals(Path.GetDirectoryName(f), directory, StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal).ToList();

        public IReadOnlyList<string> EnumerateDirectories(string directory) => [];

        public Stream OpenRead(string path) => throw new NotSupportedException();

        public string ReadAllText(string path) => throw new NotSupportedException();
    }
}
