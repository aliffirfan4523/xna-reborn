using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Reading;

/// <summary>Reading fixture projects and edge cases with <see cref="ProjectReader"/>.</summary>
public sealed class ProjectReaderTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xnareborn-reader-" + Guid.NewGuid().ToString("N"));

    public ProjectReaderTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static Task<ProjectReadResult> ReadFixture(string fixture, string projectRelative)
    {
        var root = FixturePaths.Of(fixture);
        return new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(root, projectRelative), root, CancellationToken.None);
    }

    private Task<ProjectReadResult> ReadTemp(string name, string xml, CancellationToken token = default)
    {
        var path = Path.Combine(_temp, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, xml);
        return new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(path, _temp, token);
    }

    [Fact]
    public async Task Xna40_basic_is_read_in_full()
    {
        var result = await ReadFixture("xna40-basic", "Xna40Basic.csproj");
        var info = result.Info;

        Assert.Equal("Xna40Basic.csproj", info.Path);
        Assert.Equal("Xna40Basic", info.AssemblyName);
        Assert.True(info.IsXna);
        Assert.Equal(XnaVersion.V40, info.XnaVersion);
        Assert.Equal(XnaPlatform.Windows, info.Platform);
        Assert.Equal("HiDef", info.Profile);
        Assert.Equal("v4.0", info.TargetFramework);
        Assert.Equal(["DEBUG", "TRACE", "WINDOWS"], info.DefineConstants);
        Assert.Contains("Microsoft.Xna.Framework", info.References);
        Assert.Contains("Microsoft.Xna.Framework.Graphics", info.References);
        Assert.Equal(["Game1.cs", "Program.cs", "Properties/AssemblyInfo.cs"], info.CompileFiles);
        Assert.Equal(["Content/Content.contentproj"], info.ContentProjects);
        Assert.Equal(2, info.ContentItems.Count);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Xna31_legacy_has_no_profile_and_a_35_framework()
    {
        var info = (await ReadFixture("xna31-legacy", "Xna31Legacy.csproj")).Info;

        Assert.Equal(XnaVersion.V31, info.XnaVersion);
        Assert.Null(info.Profile);
        Assert.Equal("v3.5", info.TargetFramework);
        Assert.Empty(info.ContentProjects);
    }

    [Fact]
    public async Task Gamer_services_reference_is_listed()
    {
        var info = (await ReadFixture("xna40-gamerservices", "Xna40GamerServices.csproj")).Info;

        Assert.Contains("Microsoft.Xna.Framework.GamerServices", info.References);
    }

    [Fact]
    public async Task Custom_content_reads_five_items_with_overrides_and_pipeline_reference()
    {
        var result = await ReadFixture("xna40-custom-content", "CustomContent.csproj");
        var items = result.Info.ContentItems;

        Assert.Equal(5, items.Count);
        var hero = items[0];
        Assert.Equal("Textures/hero.png", hero.SourcePath);
        Assert.Equal("hero", hero.NameOverride);
        Assert.Equal("True", hero.ProcessorParams["PremultiplyAlpha"]);
        Assert.Equal("OutlineProcessor", items[1].Processor);
        Assert.Equal("XactImporter", Assert.Single(items, i => i.SourcePath.EndsWith(".xap", StringComparison.Ordinal)).Importer);

        var content = new ContentProjectReader(new ReadOnlyFileSystem())
            .Read(Path.Combine(FixturePaths.Of("xna40-custom-content"), "Content", "Content.contentproj"), FixturePaths.Of("xna40-custom-content"));
        var pipeline = Assert.Single(content.References, r => r.Include == "MyPipeline");
        Assert.Equal(@"..\Pipeline\MyPipeline.dll", pipeline.Metadata["HintPath"]);
        Assert.Equal("v4.0", content.Properties["XnaFrameworkVersion"]);
    }

    [Fact]
    public async Task Multi_project_game_resolves_its_content_project_relative_to_the_root()
    {
        var result = await ReadFixture("multi-project-sln", Path.Combine("Game", "Game.csproj"));

        Assert.Equal("Game/Game.csproj", result.Info.Path);
        Assert.Equal(["GameContent/GameContent.contentproj"], result.Info.ContentProjects);
        Assert.Equal("Textures/icon.png", Assert.Single(result.Info.ContentItems).SourcePath);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Not_xna_project_has_no_references_and_a_modern_framework()
    {
        var info = (await ReadFixture("not-xna", "NotXna.csproj")).Info;

        Assert.False(info.IsXna);
        Assert.Equal("net8.0", info.TargetFramework);
        Assert.Empty(info.References);
        Assert.Equal("NotXna", info.AssemblyName);
    }

    [Fact]
    public async Task Invalid_xml_gives_ANA001_and_minimal_info()
    {
        var result = await ReadTemp("Bad.csproj", "<Project><oops");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ANA001", diagnostic.Code);
        Assert.StartsWith("Project file could not be read:", diagnostic.Message);
        Assert.Equal("Bad.csproj", diagnostic.File);
        Assert.False(result.Info.IsXna);
        Assert.Equal("Bad", result.Info.AssemblyName);
        Assert.Equal(XnaVersion.Unknown, result.Info.XnaVersion);
        Assert.Empty(result.Info.CompileFiles);
    }

    [Fact]
    public async Task Wildcard_compile_include_is_expanded()
    {
        Directory.CreateDirectory(Path.Combine(_temp, "sub"));
        File.WriteAllText(Path.Combine(_temp, "a.cs"), "");
        File.WriteAllText(Path.Combine(_temp, "sub", "b.cs"), "");
        File.WriteAllText(Path.Combine(_temp, "sub", "c.txt"), "");

        var result = await ReadTemp("P.csproj", """<Project><ItemGroup><Compile Include="**\*.cs" /></ItemGroup></Project>""");

        Assert.Equal(["a.cs", "sub/b.cs"], result.Info.CompileFiles);
    }

    [Fact]
    public async Task Non_wildcard_compile_include_is_kept_even_when_the_file_is_missing()
    {
        var result = await ReadTemp("P.csproj", """<Project><ItemGroup><Compile Include="Missing\X.cs" /></ItemGroup></Project>""");

        Assert.Equal(["Missing/X.cs"], result.Info.CompileFiles);
    }

    [Fact]
    public async Task Conflicting_signals_give_ANA002_and_the_property_wins()
    {
        var result = await ReadTemp(
            "P.csproj",
            """
            <Project>
              <PropertyGroup><XnaFrameworkVersion>v4.0</XnaFrameworkVersion></PropertyGroup>
              <ItemGroup><Reference Include="Microsoft.Xna.Framework, Version=3.1.0.0, Culture=neutral" /></ItemGroup>
            </Project>
            """);

        Assert.Equal(XnaVersion.V40, result.Info.XnaVersion);
        Assert.Contains(result.Diagnostics, d => d.Code == "ANA002");
    }

    [Fact]
    public async Task Missing_nested_content_project_gives_ANA001()
    {
        var result = await ReadTemp(
            "Game/P.csproj",
            """<Project><ItemGroup><NestedContentProject Include="..\Content\C.contentproj" /></ItemGroup></Project>""");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ANA001", diagnostic.Code);
        Assert.Equal("Content project could not be read: file not found", diagnostic.Message);
        Assert.Equal("Content/C.contentproj", diagnostic.File);
        Assert.Equal(["Content/C.contentproj"], result.Info.ContentProjects);
    }

    [Fact]
    public async Task Invalid_nested_content_project_gives_ANA001_with_the_reason()
    {
        Directory.CreateDirectory(Path.Combine(_temp, "Content"));
        File.WriteAllText(Path.Combine(_temp, "Content", "C.contentproj"), "<nope");

        var result = await ReadTemp(
            "P.csproj",
            """<Project><ItemGroup><NestedContentProject Include="Content\C.contentproj" /></ItemGroup></Project>""");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ANA001", diagnostic.Code);
        Assert.StartsWith("Content project could not be read:", diagnostic.Message);
        Assert.Equal("Content/C.contentproj", diagnostic.File);
    }

    [Fact]
    public async Task Define_constants_are_united_across_property_groups()
    {
        var result = await ReadTemp(
            "P.csproj",
            """
            <Project>
              <PropertyGroup><DefineConstants>DEBUG;TRACE</DefineConstants></PropertyGroup>
              <PropertyGroup Condition="'$(X)'=='1'"><DefineConstants> TRACE ; WINDOWS </DefineConstants></PropertyGroup>
            </Project>
            """);

        Assert.Equal(["DEBUG", "TRACE", "WINDOWS"], result.Info.DefineConstants);
        Assert.Equal("DEBUG;TRACE", result.Properties["DefineConstants"]);
    }

    [Fact]
    public async Task First_property_value_wins()
    {
        var result = await ReadTemp(
            "P.csproj",
            """
            <Project>
              <PropertyGroup><AssemblyName>First</AssemblyName></PropertyGroup>
              <PropertyGroup><AssemblyName>Second</AssemblyName></PropertyGroup>
            </Project>
            """);

        Assert.Equal("First", result.Properties["assemblyname"]);
        Assert.Equal("First", result.Info.AssemblyName);
    }

    [Fact]
    public async Task Items_include_import_entries()
    {
        var result = await ReadTemp(
            "P.csproj",
            """
            <Project>
              <ItemGroup><Compile Include="A.cs" /></ItemGroup>
              <Import Project="$(X)\Some.targets" />
            </Project>
            """);

        Assert.Collection(
            result.Items,
            i => Assert.Equal("Compile", i.ItemType),
            i =>
            {
                Assert.Equal("Import", i.ItemType);
                Assert.Equal(@"$(X)\Some.targets", i.Include);
                Assert.Empty(i.Metadata);
            });
    }

    [Fact]
    public async Task Cancelled_token_throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReadTemp("P.csproj", "<Project />", cts.Token));
    }
}
