using XnaReborn.Analyzer;
using XnaReborn.Analyzer.Discovery;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Analyzer.Reporting;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Reporting;
using XnaReborn.Tests.Safety;

namespace XnaReborn.Tests.Sdx;

/// <summary>SharpDX Toolkit fixtures, detection, and catalog routing (sharpdx-toolkit tasks 1 to 3).</summary>
[Trait("Category", "Sdx")]
public sealed class SdxFixtureTests
{
    private const string _toolkitCatalog = """
        {"version":1,"name":"sdx-test","appliesTo":"SharpDxToolkit","rules":[
          {"id":"SDX901","title":"Toolkit font","severity":"Warning","classification":"Manual",
           "match":{"type":"projectItem","itemType":"ToolkitFont"},"explanation":"e","resolution":"r"},
          {"id":"SDX902","title":"RH look at","severity":"Warning","classification":"Manual",
           "match":{"type":"member","names":["LookAtRH","PerspectiveFovRH"],"receiver":"Matrix","namespaces":["SharpDX"]},"explanation":"e","resolution":"r"}
        ]}
        """;

    private static readonly Dictionary<string, string[]> _expectedFiles = new(StringComparer.Ordinal)
    {
        ["sdx-basic"] = ["Content/Arial16.xml", "Content/Balls.dds", "Game1.cs", "packages.config", "Program.cs", "Properties/AssemblyInfo.cs", "README.md", "SdxBasic.csproj"],
        ["sdx-effects"] = ["Content/Arial16.xml", "Content/Tint.fx", "Game1.cs", "README.md", "SdxEffects.csproj"],
        ["sdx-lowlevel"] = ["Game1.cs", "README.md", "SdxLowLevel.csproj"],
        ["sdx-mixed"] = ["Mixed.sln", "README.md", "Toolkit/Content/Balls.dds", "Toolkit/Game1.cs", "Toolkit/ToolkitGame.csproj", "Xna/Content/Content.contentproj", "Xna/Content/Fonts/Font.spritefont", "Xna/Content/Textures/hero.png", "Xna/Game1.cs", "Xna/Program.cs", "Xna/Properties/AssemblyInfo.cs", "Xna/XnaGame.csproj"],
        ["sdx-negative"] = ["MonoGameGame/Game1.cs", "MonoGameGame/MonoGameGame.csproj", "PlainSharpDx/PlainSharpDx.csproj", "PlainSharpDx/Program.cs", "README.md"],
        ["sdx-prebuilt"] = ["Content/Balls.tkb", "Game1.cs", "README.md", "SdxPrebuilt.csproj"],
        ["sdx-store-app"] = ["Game1.cs", "README.md", "SdxStoreApp.csproj"],
    };

    public static IEnumerable<object[]> FixtureRows() => _expectedFiles.Keys.Select(k => new object[] { k });

    public static IEnumerable<object[]> ToolkitRows() =>
        new[] { "sdx-basic", "sdx-effects", "sdx-lowlevel", "sdx-prebuilt", "sdx-store-app" }.Select(k => new object[] { k });

    private static Task<DiscoveryResult> DiscoverAsync(string path) =>
        new ProjectDiscovery(new ReadOnlyFileSystem()).DiscoverAsync(path, CancellationToken.None);

    private static RuleCatalog ToolkitCatalog() => RuleCatalogLoader.Parse(_toolkitCatalog);

    // ---------- task 1: fixtures ----------
    [Theory]
    [MemberData(nameof(FixtureRows))]
    public void Fixture_has_the_expected_file_list(string name)
    {
        var root = FixturePaths.Of(name);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);

        Assert.Equal(_expectedFiles[name].Order(StringComparer.Ordinal), files);
    }

    [Theory]
    [MemberData(nameof(FixtureRows))]
    public void Fixture_readme_says_what_it_proves_and_that_it_is_synthetic(string name)
    {
        var readme = File.ReadAllText(Path.Combine(FixturePaths.Of(name), "README.md"));

        Assert.Contains("## Purpose", readme);
        Assert.Contains("## What the analyzer should find", readme);
        Assert.Contains("synthetic", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no third-party code or assets", readme, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- task 2: detection ----------
    [Theory]
    [MemberData(nameof(ToolkitRows))]
    public async Task Toolkit_fixture_is_a_toolkit_project_and_not_an_xna_project(string name)
    {
        var result = await DiscoverAsync(FixturePaths.Of(name));

        var project = Assert.Single(result.Projects);
        Assert.True(project.IsSharpDxToolkit);
        Assert.False(project.IsXna);
        Assert.Single(result.MigratableProjects);
        Assert.Empty(result.XnaProjects);
        Assert.Null(result.NoProjectReason);
    }

    [Fact]
    public async Task Mixed_solution_reports_one_toolkit_project_and_one_xna_project()
    {
        var result = await DiscoverAsync(FixturePaths.Of("sdx-mixed"));

        Assert.Equal(["Toolkit/ToolkitGame.csproj", "Xna/XnaGame.csproj"], result.Projects.Select(p => p.RelativePath));
        Assert.Equal([(false, true), (true, false)], result.Projects.Select(p => (p.IsXna, p.IsSharpDxToolkit)));
        Assert.Equal(2, result.MigratableProjects.Count());
    }

    [Theory]
    [InlineData("sdx-negative")]
    [InlineData("xna40-basic")]
    [InlineData("not-xna")]
    public async Task Plain_sharpdx_monogame_xna_and_plain_projects_are_not_toolkit_projects(string name)
    {
        var result = await DiscoverAsync(FixturePaths.Of(name));

        Assert.NotEmpty(result.Projects);
        Assert.DoesNotContain(result.Projects, p => p.IsSharpDxToolkit);
    }

    [Fact]
    public async Task Negative_fixture_has_a_monogame_project_and_a_plain_sharpdx_project_and_no_toolkit_project()
    {
        var result = await DiscoverAsync(FixturePaths.Of("sdx-negative"));

        Assert.Equal([ProjectFamily.MonoGameLegacy, ProjectFamily.Other], result.Projects.Select(p => p.Family));
        Assert.Empty(result.SharpDxToolkitProjects);
        Assert.Empty(result.MigratableProjects);
    }

    [Fact]
    public async Task A_project_with_xna_and_toolkit_signals_is_a_toolkit_project_and_says_so()
    {
        using var temp = new TempFolder();
        temp.Write("Game.csproj", """<Project><PropertyGroup><XnaFrameworkVersion>v4.0</XnaFrameworkVersion></PropertyGroup><ItemGroup><Reference Include="SharpDX.Toolkit" /></ItemGroup></Project>""");

        var result = await DiscoverAsync(temp.Path);

        Assert.Equal(ProjectFamily.SharpDxToolkit, Assert.Single(result.Projects).Family);
        Assert.Contains(result.Diagnostics, d => d.Code == "ANA020");
    }

    /// <summary>Each of the four signals alone is enough; SharpDX.targets alone is not.</summary>
    [Theory]
    [InlineData("""<Project><ItemGroup><Reference Include="SharpDX.Toolkit.Game" /></ItemGroup></Project>""", true)]
    [InlineData("""<Project><Import Project="..\packages\SharpDX.Toolkit.2.6.3\build\SharpDX.Toolkit.targets" /></Project>""", true)]
    [InlineData("""<Project><ItemGroup><ToolkitModel Include="a.fbx" /></ItemGroup></Project>""", true)]
    [InlineData("""<Project><Import Project="..\packages\SharpDX.2.6.3\build\SharpDX.targets" /><ItemGroup><Reference Include="SharpDX.Direct3D11" /></ItemGroup></Project>""", false)]
    [InlineData("""<Project><ItemGroup><Reference Include="System" /></ItemGroup></Project>""", false)]
    public async Task Each_project_file_signal_is_detected(string xml, bool expected)
    {
        using var temp = new TempFolder();
        temp.Write("Game.csproj", xml);

        var result = await DiscoverAsync(temp.Path);

        Assert.Equal(expected, Assert.Single(result.Projects).IsSharpDxToolkit);
    }

    [Theory]
    [InlineData("""<packages><package id="SharpDX.Toolkit.Game" version="2.6.3" /></packages>""", true)]
    [InlineData("""<packages><package id="SharpDX" version="2.6.3" /></packages>""", false)]
    [InlineData("<packages><unclosed>", false)]
    public async Task Packages_config_signal_is_detected_and_a_broken_file_is_ignored(string packages, bool expected)
    {
        using var temp = new TempFolder();
        temp.Write("Game.csproj", "<Project />");
        temp.Write("packages.config", packages);

        var result = await DiscoverAsync(temp.Path);

        Assert.Equal(expected, Assert.Single(result.Projects).IsSharpDxToolkit);
    }

    [Fact]
    public async Task Project_reader_records_the_family_of_toolkit_and_xna_projects()
    {
        var reader = new ProjectReader(new ReadOnlyFileSystem());
        var toolkit = await reader.ReadAsync(Path.Combine(FixturePaths.Of("sdx-basic"), "SdxBasic.csproj"), FixturePaths.Of("sdx-basic"), CancellationToken.None);
        var xna = await reader.ReadAsync(Path.Combine(FixturePaths.Of("xna40-basic"), "Xna40Basic.csproj"), FixturePaths.Of("xna40-basic"), CancellationToken.None);

        Assert.Equal(ProjectFamily.SharpDxToolkit, toolkit.Info.Family);
        Assert.False(toolkit.Info.IsXna);
        Assert.Equal(ProjectFamily.Xna, xna.Info.Family);
        Assert.True(xna.Info.IsXna);
    }

    [Theory]
    [MemberData(nameof(ToolkitRows))]
    public async Task Analyzing_a_toolkit_fixture_changes_no_file(string name)
    {
        var folder = FixturePaths.Of(name);
        var before = FolderState.Capture(folder);

        var report = await new ProjectAnalyzer().AnalyzeAsync(folder, ToolkitCatalog(), null, CancellationToken.None);

        Assert.Single(report.Projects);
        Assert.Equal(before, FolderState.Capture(folder));
    }

    // ---------- task 3.2: appliesTo ----------
    [Fact]
    public async Task Toolkit_catalog_runs_on_a_toolkit_fixture()
    {
        var report = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of("sdx-basic"), ToolkitCatalog(), null, CancellationToken.None);

        Assert.Equal(["SDX901", "SDX902"], report.Findings.Select(f => f.RuleId).Distinct().Order());
        var lookAt = Assert.Single(report.Findings, f => f.RuleId == "SDX902" && f.Snippet!.Contains("LookAtRH"));
        Assert.Equal("Game1.cs", lookAt.Location!.File);
    }

    [Fact]
    public async Task Xna_catalog_never_runs_on_a_toolkit_fixture_and_the_report_says_why()
    {
        var report = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of("sdx-basic"), SampleCatalog.Create(), null, CancellationToken.None);

        Assert.Empty(report.Projects);
        Assert.Empty(report.Findings);
        Assert.Equal("Found 1 SharpDX Toolkit project(s), but no rule catalog for them was loaded.", report.NoProjectReason);
    }

    [Fact]
    public async Task Both_catalogs_on_a_mixed_solution_each_run_only_on_their_own_project()
    {
        var report = await new ProjectAnalyzer().AnalyzeAsync(
            FixturePaths.Of("sdx-mixed"), [SampleCatalog.Create(), ToolkitCatalog()], [], null, CancellationToken.None);

        Assert.Equal(2, report.Projects.Count);
        Assert.All(report.Findings.Where(f => f.RuleId.StartsWith("SDX", StringComparison.Ordinal)), f => Assert.Equal("Toolkit/ToolkitGame.csproj", f.Project));
        Assert.All(report.Findings.Where(f => f.RuleId.StartsWith("XNA", StringComparison.Ordinal)), f => Assert.Equal("Xna/XnaGame.csproj", f.Project));
        Assert.Contains(report.Findings, f => f.RuleId == "SDX902");
        Assert.Contains(report.Findings, f => f.RuleId == "XNA903");
        Assert.Equal("xna-test-sample + sdx-test", report.CatalogName);
    }

    [Fact]
    public async Task Catalog_diagnostics_are_added_to_the_report()
    {
        var note = new Diagnostic("ANA021", "Toolkit catalog skipped.", "rules/sharpdx-toolkit-to-monogame.json");

        var report = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of("xna40-basic"), [SampleCatalog.Create()], [note], null, CancellationToken.None);

        Assert.Contains(note, report.Diagnostics);
        Assert.NotEmpty(report.Findings);
    }

    [Fact]
    public async Task A_rule_id_defined_in_two_catalogs_is_refused()
    {
        var twin = RuleCatalogLoader.Parse("""
            {"version":1,"name":"twin","appliesTo":"SharpDxToolkit","rules":[
              {"id":"XNA901","title":"t","severity":"Info","classification":"Manual",
               "match":{"type":"reference","name":"X"},"explanation":"e","resolution":"r"}]}
            """);

        await Assert.ThrowsAsync<RuleCatalogException>(() =>
            new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Root, [SampleCatalog.Create(), twin], [], null, CancellationToken.None));
    }

    // ---------- task 3.4: XNA output unchanged ----------
    [Fact]
    public async Task Xna_report_json_has_no_family_property()
    {
        var report = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of("xna40-basic"), SampleCatalog.Create(), null, CancellationToken.None);

        Assert.DoesNotContain("\"family\"", JsonReportWriter.Write(report), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xnareborn-sdx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Write(string name, string text) => File.WriteAllText(System.IO.Path.Combine(Path, name), text);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
