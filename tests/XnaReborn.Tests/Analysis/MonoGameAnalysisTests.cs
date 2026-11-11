using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Analyzer.Reporting;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Analysis;

/// <summary>Task 5: Content.mgcb reading, catalogs that apply to one family, the list overload, and report shape.</summary>
public sealed class MonoGameAnalysisTests : IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), "xnareborn-mganalysis-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    private static RuleCatalog Catalog(string name, string appliesTo, string id, string referenceName, string severity = "Warning", string? weights = null)
    {
        var applies = appliesTo.Length == 0 ? string.Empty : $",\"appliesTo\":\"{appliesTo}\"";
        var scoring = weights is null ? string.Empty : $",\"scoring\":{weights}";
        return RuleCatalogLoader.Parse($$"""
            {"version":1,"name":"{{name}}"{{applies}}{{scoring}},"rules":[
              {"id":"{{id}}","title":"t","severity":"{{severity}}","classification":"Manual",
               "match":{"type":"reference","name":"{{referenceName}}"},"explanation":"e","resolution":"r"}]}
            """);
    }

    private static readonly string _sampleWeights =
        """{"severityWeights":{"Info":1,"Warning":10,"Error":20,"Blocker":30},"classificationFactors":{"AutoFixable":0.5,"Manual":1,"Unsupported":2}}""";

    private static Task<AnalysisReport> AnalyzeAsync(string path, params RuleCatalog[] catalogs) =>
        new ProjectAnalyzer().AnalyzeAsync(path, catalogs, null, CancellationToken.None);

    private string Folder(string name, string project)
    {
        var folder = Path.Combine(_work, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name + ".csproj"), project);
        return folder;
    }

    private static string XnaProject => """<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><ItemGroup><Reference Include="Microsoft.Xna.Framework, Version=4.0.0.0" /></ItemGroup></Project>""";

    private static string MonoGameProject => """<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><ItemGroup><Reference Include="MonoGame.Framework" /></ItemGroup></Project>""";

    [Fact]
    public async Task An_xna_catalog_never_fires_on_a_monogame_project_and_the_reverse()
    {
        // Each rule would match the other family's project if the engine did not filter by appliesTo.
        var xnaCatalog = Catalog("xna", string.Empty, "XNA901", "MonoGame.Framework");
        var mgCatalog = Catalog("mg", "monogame", "MG901", "Microsoft.Xna.Framework");

        var onMonoGame = await AnalyzeAsync(Folder("A", MonoGameProject), xnaCatalog, mgCatalog);
        var onXna = await AnalyzeAsync(Folder("B", XnaProject), xnaCatalog, mgCatalog);

        Assert.Empty(onMonoGame.Findings);
        Assert.Empty(onXna.Findings);
    }

    [Fact]
    public async Task Each_catalog_fires_on_its_own_family()
    {
        var xnaCatalog = Catalog("xna", string.Empty, "XNA901", "Microsoft.Xna.Framework");
        var mgCatalog = Catalog("mg", "monogame", "MG901", "MonoGame.Framework");

        var onMonoGame = await AnalyzeAsync(Folder("A", MonoGameProject), xnaCatalog, mgCatalog);
        var onXna = await AnalyzeAsync(Folder("B", XnaProject), xnaCatalog, mgCatalog);

        Assert.Equal(["MG901"], onMonoGame.Findings.Select(f => f.RuleId));
        Assert.Equal(["XNA901"], onXna.Findings.Select(f => f.RuleId));
        Assert.Equal(ProjectFamily.MonoGameLegacy, Assert.Single(onMonoGame.Projects).Family);
        Assert.Equal("xna + mg", onMonoGame.CatalogName);
    }

    [Fact]
    public async Task A_solution_with_both_families_keeps_the_family_on_each_project_and_scores_each_rule_once()
    {
        var folder = Path.Combine(_work, "Both");
        Directory.CreateDirectory(Path.Combine(folder, "X"));
        Directory.CreateDirectory(Path.Combine(folder, "M"));
        File.WriteAllText(Path.Combine(folder, "X", "X.csproj"), XnaProject);
        File.WriteAllText(Path.Combine(folder, "M", "M.csproj"), MonoGameProject);
        var xnaCatalog = Catalog("xna", string.Empty, "XNA901", "Microsoft.Xna.Framework", weights: _sampleWeights);
        var mgCatalog = Catalog("mg", "monogame", "MG901", "MonoGame.Framework", weights: _sampleWeights);

        var report = await AnalyzeAsync(folder, xnaCatalog, mgCatalog);

        Assert.Equal([ProjectFamily.MonoGameLegacy, ProjectFamily.Xna], report.Projects.Select(p => p.Family));
        Assert.Equal(80, report.Score);
    }

    [Fact]
    public async Task Each_rule_is_scored_with_the_weights_of_its_own_catalog()
    {
        var folder = Path.Combine(_work, "Weights");
        Directory.CreateDirectory(Path.Combine(folder, "X"));
        Directory.CreateDirectory(Path.Combine(folder, "M"));
        File.WriteAllText(Path.Combine(folder, "X", "X.csproj"), XnaProject);
        File.WriteAllText(Path.Combine(folder, "M", "M.csproj"), MonoGameProject);
        var xnaCatalog = Catalog("xna", string.Empty, "XNA901", "Microsoft.Xna.Framework");
        var mgCatalog = Catalog("mg", "monogame", "MG901", "MonoGame.Framework", weights: _sampleWeights);

        var report = await AnalyzeAsync(folder, xnaCatalog, mgCatalog);

        // XNA901 uses the defaults (Warning 3), MG901 uses its own (Warning 10): 100 - 3 - 10.
        Assert.Equal(87, report.Score);
    }

    [Fact]
    public async Task The_old_overload_still_works_and_analyzes_only_xna_projects()
    {
        var catalog = Catalog("xna", string.Empty, "XNA901", "Microsoft.Xna.Framework");

        var report = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Root, catalog, null, CancellationToken.None);

        Assert.NotEmpty(report.Projects);
        Assert.All(report.Projects, p => Assert.Equal(ProjectFamily.Xna, p.Family));
        Assert.Equal("xna", report.CatalogName);
    }

    [Fact]
    public async Task A_monogame_folder_with_only_an_xna_catalog_says_no_catalog_was_loaded()
    {
        var catalog = Catalog("xna", string.Empty, "XNA901", "Microsoft.Xna.Framework");

        var report = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of("mg34-windowsgl"), catalog, null, CancellationToken.None);

        Assert.Empty(report.Projects);
        Assert.Equal("Found 1 MonoGame project(s), but no rule catalog for them was loaded.", report.NoProjectReason);
    }

    [Fact]
    public async Task The_same_rule_id_in_two_catalogs_is_rejected()
    {
        var a = Catalog("a", string.Empty, "XNA901", "x");
        var b = Catalog("b", "monogame", "XNA901", "y");

        await Assert.ThrowsAsync<RuleCatalogException>(() => AnalyzeAsync(FixturePaths.Of("mg38-current"), a, b));
    }

    [Fact]
    public async Task Current_only_projects_are_analyzed_and_the_reason_says_nothing_to_migrate()
    {
        var mg = Catalog("mg", "monogame", "MG901", "OpenTK");

        var report = await AnalyzeAsync(FixturePaths.Of("mg38-current"), mg);

        Assert.Equal(ProjectFamily.MonoGameCurrent, Assert.Single(report.Projects).Family);
        Assert.Equal("All MonoGame projects are already current. Nothing to migrate.", report.NoProjectReason);
        Assert.Equal(100, report.Score);
        var text = MarkdownReportWriter.Write(report);
        Assert.Contains("## Projects", text, StringComparison.Ordinal);
        Assert.Contains("## Nothing to migrate\n\nAll MonoGame projects are already current. Nothing to migrate.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("## No XNA project found", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_for_an_xna_project_has_no_monogame_fields_and_for_a_monogame_project_it_adds_them()
    {
        var xnaCatalog = Catalog("xna", string.Empty, "XNA901", "Microsoft.Xna.Framework");
        var mgCatalog = Catalog("mg", "monogame", "MG901", "MonoGame.Framework");

        var xnaJson = JsonReportWriter.Write(await AnalyzeAsync(FixturePaths.Of("xna40-basic"), xnaCatalog, mgCatalog));
        var mgJson = JsonReportWriter.Write(await AnalyzeAsync(FixturePaths.Of("mg37-windowsdx-pkgconfig"), xnaCatalog, mgCatalog));

        Assert.Contains("\"schemaVersion\": 1", xnaJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"family\"", xnaJson, StringComparison.Ordinal);
        Assert.DoesNotContain("monoGame", xnaJson, StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\": 1", mgJson, StringComparison.Ordinal);
        Assert.Contains("\"family\": \"MonoGameLegacy\"", mgJson, StringComparison.Ordinal);
        Assert.Contains("\"monoGameVersion\"", mgJson, StringComparison.Ordinal);
        Assert.Contains("\"exact\": \"3.7.0.1708\"", mgJson, StringComparison.Ordinal);
        Assert.Contains("\"monoGamePlatform\": \"WindowsDX\"", mgJson, StringComparison.Ordinal);
        Assert.Contains("\"isXna\": false", mgJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Markdown_adds_the_family_and_monogame_version_columns_only_for_non_xna_projects()
    {
        var xnaCatalog = Catalog("xna", string.Empty, "XNA901", "Microsoft.Xna.Framework");
        var mgCatalog = Catalog("mg", "monogame", "MG901", "MonoGame.Framework");

        var xnaText = MarkdownReportWriter.Write(await AnalyzeAsync(FixturePaths.Of("xna40-basic"), xnaCatalog, mgCatalog));
        var mgText = MarkdownReportWriter.Write(await AnalyzeAsync(FixturePaths.Of("mg37-windowsdx-pkgconfig"), xnaCatalog, mgCatalog));

        Assert.Contains("| Project | XNA version | Platform | Profile | Target framework |", xnaText, StringComparison.Ordinal);
        Assert.Contains("| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |", mgText, StringComparison.Ordinal);
        Assert.Contains("| Mg37WindowsDx.csproj | MonoGameLegacy | - | 3.7.0.1708 (High) | WindowsDX | - | v4.7 |", mgText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_mgcb_listed_by_the_project_is_read_with_references_and_entries()
    {
        var root = FixturePaths.Of("mg37-desktopgl-installdir");
        var read = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(root, "Mg37DesktopGl.csproj"), root, CancellationToken.None);

        var mgcb = Assert.Single(read.MgcbFiles!);
        Assert.Equal("Content/Content.mgcb", mgcb.Path);
        Assert.Equal(["Content/Content.mgcb"], read.Info.MgcbFiles);
        Assert.Equal(2, mgcb.File.References.Count);
        Assert.Equal(2, mgcb.File.Entries.Count);
        Assert.Equal("DesktopGL", mgcb.File.Setting("platform")!.Value);
    }

    [Fact]
    public async Task An_sdk_project_gets_its_mgcb_by_convention()
    {
        var root = FixturePaths.Of("mg38-current");
        var read = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(root, "Mg38Current.csproj"), root, CancellationToken.None);

        Assert.Equal(["Content/Content.mgcb"], read.Info.MgcbFiles);
    }

    [Fact]
    public async Task A_listed_mgcb_that_is_missing_gives_ANA013_and_the_project_is_still_read()
    {
        var folder = Folder("MissingMgcb", """<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><ItemGroup><Reference Include="MonoGame.Framework" /><MonoGameContentReference Include="Content\Content.mgcb" /></ItemGroup></Project>""");

        var read = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(folder, "MissingMgcb.csproj"), folder, CancellationToken.None);

        Assert.Equal(ProjectFamily.MonoGameLegacy, read.Info.Family);
        Assert.Null(read.Info.MgcbFiles);
        Assert.Contains(read.Diagnostics, d => d.Code == "ANA013" && d.File == "Content/Content.mgcb");
    }

    [Fact]
    public async Task A_malformed_mgcb_line_is_reported_with_the_file_path()
    {
        var folder = Folder("BadMgcb", """<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><ItemGroup><Reference Include="MonoGame.Framework" /></ItemGroup></Project>""");
        Directory.CreateDirectory(Path.Combine(folder, "Content"));
        File.WriteAllText(Path.Combine(folder, "Content", "Content.mgcb"), "/platform:Windows\nnot an option\n");

        var read = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(folder, "BadMgcb.csproj"), folder, CancellationToken.None);

        Assert.Contains(read.Diagnostics, d => d.Code == "ANA010" && d.File == "Content/Content.mgcb");
        Assert.Single(read.MgcbFiles!);
    }

    [Fact]
    public async Task An_xna_project_reads_no_mgcb_even_when_the_conventional_file_exists()
    {
        var folder = Folder("XnaWithMgcb", XnaProject);
        Directory.CreateDirectory(Path.Combine(folder, "Content"));
        File.WriteAllText(Path.Combine(folder, "Content", "Content.mgcb"), "/platform:Windows\n");

        var read = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(folder, "XnaWithMgcb.csproj"), folder, CancellationToken.None);

        Assert.Empty(read.MgcbFiles!);
        Assert.Null(read.Info.MgcbFiles);
    }
}
