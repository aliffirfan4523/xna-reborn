using System.Text.Json;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Analysis;

/// <summary>Visual Basic projects: language model, discovery, project reading and the language aware rule fields.</summary>
[Trait("Category", "Analyzer")]
public sealed class VisualBasicAnalysisTests
{
    private static readonly Lazy<RuleCatalog> _catalog = new(() =>
        RuleCatalogLoader.LoadAsync(
            Path.Combine(FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json"),
            new ReadOnlyFileSystem(),
            CancellationToken.None).GetAwaiter().GetResult());

    private static Task<AnalysisReport> AnalyzeAsync(string fixture, RuleCatalog? catalog = null) =>
        new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of(fixture), catalog ?? _catalog.Value, null, CancellationToken.None);

    private static RuleCatalog Catalog(string rulesJson) =>
        RuleCatalogLoader.Parse("{ \"version\": 1, \"name\": \"t\", \"rules\": [ " + rulesJson + " ] }");

    private static string ProjectRule(string id, string classification, string extra) =>
        "{ \"id\": \"" + id + "\", \"title\": \"t\", \"severity\": \"Warning\", \"classification\": \"" + classification + "\", " + extra
        + " \"match\": { \"type\": \"projectProperty\", \"name\": \"XnaFrameworkVersion\" }, \"explanation\": \"e\", \"resolution\": \"r\" }";
    // ---------- language model ----------
    [Theory]
    [InlineData("a/Game.vbproj", SourceLanguage.VisualBasic)]
    [InlineData("a/Game.VBPROJ", SourceLanguage.VisualBasic)]
    [InlineData("a/Game.csproj", SourceLanguage.CSharp)]
    [InlineData("a/Game.contentproj", SourceLanguage.CSharp)]
    public void Language_comes_from_the_project_extension(string path, SourceLanguage expected) =>
        Assert.Equal(expected, ProjectLanguage.FromPath(path));

    [Fact]
    public void Language_round_trips_and_rejects_unknown_values()
    {
        Assert.Equal("\"vb\"", JsonSerializer.Serialize(SourceLanguage.VisualBasic));
        Assert.Equal("\"csharp\"", JsonSerializer.Serialize(SourceLanguage.CSharp));
        Assert.Equal(SourceLanguage.VisualBasic, JsonSerializer.Deserialize<SourceLanguage>("\"vb\""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SourceLanguage>("\"fsharp\""));
    }

    [Fact]
    public async Task Project_info_defaults_to_csharp_and_leaves_the_language_out_of_json()
    {
        var csharp = await AnalyzeAsync("xna40-basic");
        var vb = await AnalyzeAsync("vb-xna40-basic");

        Assert.Equal(SourceLanguage.CSharp, Assert.Single(csharp.Projects).Language);
        Assert.DoesNotContain("\"Language\"", JsonSerializer.Serialize(csharp), StringComparison.Ordinal);
        Assert.Contains("\"Language\":\"vb\"", JsonSerializer.Serialize(vb), StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_language_in_a_catalog_is_rejected()
    {
        var ex = Assert.Throws<RuleCatalogException>(() => Catalog(ProjectRule("XNA901", "Manual", "\"languages\": [\"cobol\"],")));

        Assert.NotEmpty(ex.Errors);
    }

    [Fact]
    public async Task Rule_with_languages_vb_is_skipped_for_csharp_and_runs_for_vb()
    {
        var catalog = Catalog(ProjectRule("XNA901", "Manual", "\"languages\": [\"vb\"],"));

        var csharp = await AnalyzeAsync("xna40-basic", catalog);
        var vb = await AnalyzeAsync("vb-xna40-basic", catalog);

        Assert.DoesNotContain(csharp.Findings, f => f.RuleId == "XNA901");
        Assert.Contains(vb.Findings, f => f.RuleId == "XNA901");
    }

    [Fact]
    public async Task Autofixable_rule_with_csharp_only_fix_counts_as_manual_for_vb_in_score_and_projection()
    {
        var catalog = Catalog(ProjectRule("XNA902", "AutoFixable", "\"autoFixLanguages\": [\"csharp\"], \"migratorTask\": \"x:1\","));

        var csharp = await AnalyzeAsync("xna40-basic", catalog);
        var vb = await AnalyzeAsync("vb-xna40-basic", catalog);

        Assert.Equal(Classification.AutoFixable, Assert.Single(csharp.Findings).Classification);
        Assert.Equal(Classification.Manual, Assert.Single(vb.Findings).Classification);
        Assert.Equal(100, csharp.ProjectedScore);
        Assert.True(vb.ProjectedScore < 100);
        Assert.Equal(vb.Score, vb.ProjectedScore);
    }

    [Fact]
    public void Effective_classification_is_unchanged_when_the_rule_lists_no_languages()
    {
        var rule = new Rule("XNA903", "t", Severity.Warning, Classification.AutoFixable, null, null, new ReferenceMatch("X"), "e", "r", "x:1");

        Assert.Equal(Classification.AutoFixable, rule.EffectiveClassification(SourceLanguage.VisualBasic));
        Assert.True(rule.AppliesTo(SourceLanguage.VisualBasic));
    }

    // ---------- discovery and reading ----------
    [Fact]
    public async Task Vb_40_fixture_is_an_xna_vb_project_with_imports_defines_and_compile_files()
    {
        var report = await AnalyzeAsync("vb-xna40-basic");

        var project = Assert.Single(report.Projects);
        Assert.Equal("VbXna40Basic.vbproj", project.Path);
        Assert.True(project.IsXna);
        Assert.Equal(SourceLanguage.VisualBasic, project.Language);
        Assert.Equal(XnaVersion.V40, project.XnaVersion);
        Assert.Equal(["WINDOWS", "DEBUG", "TRACE"], project.DefineConstants);
        Assert.Equal(["Game1.vb", "Program.vb", "My Project/AssemblyInfo.vb"], project.CompileFiles);
        Assert.Single(project.ContentProjects);
        Assert.DoesNotContain(report.Diagnostics, d => d.Code == "ANA002");
    }

    [Fact]
    public async Task Namespace_imports_are_kept_apart_from_msbuild_imports()
    {
        var path = Path.Combine(FixturePaths.Of("vb-xna40-basic"), "VbXna40Basic.vbproj");
        var read = await new XnaReborn.Analyzer.Reading.ProjectReader(new ReadOnlyFileSystem())
            .ReadAsync(path, FixturePaths.Of("vb-xna40-basic"), CancellationToken.None);

        Assert.Equal(
            ["Microsoft.Xna.Framework", "Microsoft.Xna.Framework.Graphics", "Microsoft.Xna.Framework.Input", "System", "System.Collections.Generic"],
            read.ImplicitImports);
        Assert.Equal(5, read.Items.Count(i => i.ItemType == "NamespaceImport"));
        Assert.DoesNotContain(read.Items, i => i.ItemType == "Import" && i.Include == "Microsoft.Xna.Framework");
    }

    [Fact]
    public async Task Handmade_vb_31_project_is_xna_31_from_its_references_alone_and_drops_define_values()
    {
        var report = await AnalyzeAsync("vb-xna31-handmade");

        var project = Assert.Single(report.Projects);
        Assert.True(project.IsXna);
        Assert.Equal(XnaVersion.V31, project.XnaVersion);
        Assert.Equal(["DEBUG", "TRACE"], project.DefineConstants);
        var ids = report.Findings.Select(f => f.RuleId).ToHashSet();
        Assert.Contains("XNA003", ids);
        Assert.DoesNotContain("XNA001", ids);
        Assert.DoesNotContain("XNA002", ids);
    }

    [Fact]
    public async Task Vb_project_that_already_uses_monogame_is_not_detected_as_xna()
    {
        var report = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of("vb-monogame-already"), _catalog.Value, null, CancellationToken.None);

        Assert.DoesNotContain(report.Projects, p => p.IsXna);
        Assert.Empty(report.Findings);
    }

    [Fact]
    public async Task Mixed_solution_lists_the_csharp_game_and_the_vb_library()
    {
        var report = await AnalyzeAsync("mixed-cs-vb-sln");

        Assert.Equal(["Game/Game.csproj", "Lib/Lib.vbproj"], report.Projects.Select(p => p.Path));
        Assert.Equal([SourceLanguage.CSharp, SourceLanguage.VisualBasic], report.Projects.Select(p => p.Language));
        Assert.All(report.Projects, p => Assert.True(p.IsXna));
        Assert.DoesNotContain(report.Diagnostics, d => d.Code == "ANA006");
    }

    [Fact]
    public async Task Vbproj_can_be_opened_directly()
    {
        var path = Path.Combine(FixturePaths.Of("vb-xna31-handmade"), "VbXna31Handmade.vbproj");

        var report = await new ProjectAnalyzer().AnalyzeAsync(path, _catalog.Value, null, CancellationToken.None);

        Assert.Equal(SourceLanguage.VisualBasic, Assert.Single(report.Projects).Language);
    }
}
