using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;

namespace XnaReborn.Tests.Reading;

/// <summary>A ProjectReference to a .contentproj is read like a NestedContentProject.</summary>
public sealed class ProjectReaderContentReferenceTests : IDisposable
{
    private const string _content =
        """<Project><ItemGroup><Compile Include="hero.png"><Name>hero</Name><Importer>TextureImporter</Importer><Processor>TextureProcessor</Processor></Compile></ItemGroup></Project>""";

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xnareborn-cref-" + Guid.NewGuid().ToString("N"));

    public ProjectReaderContentReferenceTests() => Directory.CreateDirectory(_temp);

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_temp, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private Task<ProjectReadResult> Read(string gameXml, string? contentXml = _content)
    {
        Write("Game/Game.csproj", gameXml);
        if (contentXml is not null)
        {
            Write("Content/Content.contentproj", contentXml);
        }

        return new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(_temp, "Game", "Game.csproj"), _temp, CancellationToken.None);
    }

    private static string Game(params string[] items) => "<Project><ItemGroup>" + string.Concat(items) + "</ItemGroup></Project>";

    [Fact]
    public async Task Reference_to_a_content_project_is_followed()
    {
        var result = await Read(Game("""<ProjectReference Include="..\Content\Content.contentproj"><XnaReferenceType>Content</XnaReferenceType></ProjectReference>"""));

        Assert.Equal(["Content/Content.contentproj"], result.Info.ContentProjects);
        var item = Assert.Single(result.Info.ContentItems);
        Assert.Equal("hero.png", item.SourcePath);
        Assert.Equal("TextureImporter", item.Importer);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Extension_comparison_ignores_case()
    {
        var result = await Read(Game("""<ProjectReference Include="..\Content\Content.CONTENTPROJ" />"""));

        Assert.Single(result.Info.ContentItems);
    }

    [Fact]
    public async Task Both_forms_to_the_same_project_are_read_once()
    {
        var result = await Read(Game(
            """<NestedContentProject Include="..\Content\Content.contentproj" />""",
            """<ProjectReference Include="..\Content\Content.contentproj" />"""));

        Assert.Equal(["Content/Content.contentproj"], result.Info.ContentProjects);
        Assert.Single(result.Info.ContentItems);
    }

    [Fact]
    public async Task Reference_listed_before_nested_form_is_read_once()
    {
        var result = await Read(Game(
            """<ProjectReference Include="..\Content\Content.contentproj" />""",
            """<NestedContentProject Include="../Content/Content.contentproj" />"""));

        Assert.Equal(["Content/Content.contentproj"], result.Info.ContentProjects);
        Assert.Single(result.Info.ContentItems);
    }

    [Fact]
    public async Task Document_order_is_kept_across_both_forms()
    {
        Write("Other/Other.contentproj", """<Project><ItemGroup><Compile Include="b.png" /></ItemGroup></Project>""");
        var result = await Read(Game(
            """<ProjectReference Include="..\Other\Other.contentproj" />""",
            """<NestedContentProject Include="..\Content\Content.contentproj" />"""));

        Assert.Equal(["Other/Other.contentproj", "Content/Content.contentproj"], result.Info.ContentProjects);
        Assert.Equal(["b.png", "hero.png"], result.Info.ContentItems.Select(i => i.SourcePath));
    }

    [Fact]
    public async Task Reference_to_a_normal_project_is_not_content()
    {
        Write("Lib/Lib.csproj", "<Project />");
        var result = await Read(Game("""<ProjectReference Include="..\Lib\Lib.csproj" />"""));

        Assert.Empty(result.Info.ContentProjects);
        Assert.Empty(result.Info.ContentItems);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Other_item_type_with_a_contentproj_include_is_not_followed()
    {
        var result = await Read(Game("""<None Include="..\Content\Content.contentproj" />"""));

        Assert.Empty(result.Info.ContentProjects);
        Assert.Empty(result.Info.ContentItems);
    }

    [Fact]
    public async Task Missing_content_project_gives_ANA001()
    {
        var result = await Read(Game("""<ProjectReference Include="..\Content\Content.contentproj" />"""), contentXml: null);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ANA001", diagnostic.Code);
        Assert.Equal("Content project could not be read: file not found", diagnostic.Message);
        Assert.Equal("Content/Content.contentproj", diagnostic.File);
        Assert.Equal(["Content/Content.contentproj"], result.Info.ContentProjects);
        Assert.Empty(result.Info.ContentItems);
    }
}
