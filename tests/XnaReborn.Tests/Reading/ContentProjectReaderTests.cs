using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Reading;

/// <summary>Reading content projects with <see cref="ContentProjectReader"/>.</summary>
public sealed class ContentProjectReaderTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xnareborn-content-" + Guid.NewGuid().ToString("N"));

    public ContentProjectReaderTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public void Basic_fixture_content_items_are_read_in_order()
    {
        var root = FixturePaths.Of("xna40-basic");
        var result = new ContentProjectReader(new ReadOnlyFileSystem()).Read(Path.Combine(root, "Content", "Content.contentproj"), root);

        Assert.Empty(result.Diagnostics);
        Assert.Collection(
            result.Items,
            hero =>
            {
                Assert.Equal("Textures/hero.png", hero.SourcePath);
                Assert.Equal("hero", hero.NameOverride);
                Assert.Equal("TextureImporter", hero.Importer);
                Assert.Equal("TextureProcessor", hero.Processor);
                Assert.Equal("True", Assert.Single(hero.ProcessorParams).Value);
            },
            font =>
            {
                Assert.Equal("Fonts/Font.spritefont", font.SourcePath);
                Assert.Equal("FontDescriptionProcessor", font.Processor);
                Assert.Empty(font.ProcessorParams);
            });
        Assert.Equal(6, result.References.Count);
    }

    [Fact]
    public void Item_without_name_importer_or_processor_gets_nulls()
    {
        var path = Path.Combine(_temp, "C.contentproj");
        File.WriteAllText(path, """<Project><ItemGroup><Compile Include="a\b.png" /></ItemGroup></Project>""");

        var item = Assert.Single(new ContentProjectReader(new ReadOnlyFileSystem()).Read(path, _temp).Items);

        Assert.Equal("a/b.png", item.SourcePath);
        Assert.Null(item.NameOverride);
        Assert.Null(item.Importer);
        Assert.Null(item.Processor);
    }

    [Fact]
    public void Missing_file_gives_ANA001()
    {
        var result = new ContentProjectReader(new ReadOnlyFileSystem()).Read(Path.Combine(_temp, "Nope.contentproj"), _temp);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ANA001", diagnostic.Code);
        Assert.Equal("Nope.contentproj", diagnostic.File);
        Assert.Empty(result.Items);
    }
}
