using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Analyzer.Source;
using XnaReborn.Core.Content;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Analysis;

/// <summary>XNA Game Studio Express 1.0 and 2.0 projects: version model, detection, in-project content, method declarations and the XNA100 to XNA105 rules.</summary>
[Trait("Category", "Analyzer")]
public sealed class OlderXnaTests
{
    private static readonly Lazy<RuleCatalog> _catalog = new(() =>
        RuleCatalogLoader.LoadAsync(
            Path.Combine(FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json"),
            new ReadOnlyFileSystem(),
            CancellationToken.None).GetAwaiter().GetResult());

    private static Task<AnalysisReport> AnalyzeAsync(string fixture) =>
        new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of(fixture), _catalog.Value, null, CancellationToken.None);

    private static IEnumerable<string> RuleIds(AnalysisReport report) => report.Findings.Select(f => f.RuleId).Distinct();

    [Theory]
    [InlineData("1.0", XnaVersion.V10)]
    [InlineData("2.0", XnaVersion.V20)]
    [InlineData("3.0", XnaVersion.V30)]
    [InlineData("3.1", XnaVersion.V31)]
    [InlineData("4.0", XnaVersion.V40)]
    [InlineData("Unknown", XnaVersion.Unknown)]
    public void Version_round_trips_through_json(string text, XnaVersion version)
    {
        var json = JsonSerializer.Serialize(version);

        Assert.Equal($"\"{text}\"", json);
        Assert.Equal(version, JsonSerializer.Deserialize<XnaVersion>(json));
    }

    [Fact]
    public async Task Express_fixture_is_version_1_0_with_in_project_content_and_no_conflict()
    {
        var report = await AnalyzeAsync("xna10-express-basic");

        var project = Assert.Single(report.Projects);
        Assert.True(project.IsXna);
        Assert.Equal(XnaVersion.V10, project.XnaVersion);
        Assert.DoesNotContain(report.Diagnostics, d => d.Code == "ANA002");
        Assert.Equal(2, project.ContentItems.Count);
        Assert.All(project.ContentItems, i => Assert.True(i.InProject));
        var ship = Assert.Single(project.ContentItems, i => i.NameOverride == "ship");
        Assert.Equal("TextureImporter", ship.Importer);
        Assert.Equal("SpriteTextureProcessor", ship.Processor);
        Assert.Equal("Content/ship.png", ship.SourcePath);
    }

    [Fact]
    public async Task Express_fixture_triggers_the_older_rules_and_not_the_legacy_processor_rule()
    {
        var ids = RuleIds(await AnalyzeAsync("xna10-express-basic")).ToHashSet();

        Assert.Superset(
            new HashSet<string> { "XNA001", "XNA002", "XNA003", "XNA022", "XNA024", "XNA100", "XNA101", "XNA102", "XNA103", "XNA104", "XNA105" },
            ids);
        Assert.DoesNotContain("XNA050", ids);
    }

    [Fact]
    public async Task Clean_express_fixture_has_only_project_findings()
    {
        var report = await AnalyzeAsync("xna10-clean");

        Assert.Equal(XnaVersion.V10, Assert.Single(report.Projects).XnaVersion);
        Assert.Equal(["XNA001", "XNA002", "XNA003", "XNA100"], RuleIds(report).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Version_2_0_solution_reads_both_projects_with_the_nested_content_project()
    {
        var report = await AnalyzeAsync("xna20-vs2005");

        Assert.Equal(2, report.Projects.Count);
        Assert.All(report.Projects, p => Assert.Equal(XnaVersion.V20, p.XnaVersion));
        Assert.DoesNotContain(report.Diagnostics, d => d.Code == "ANA002");
        var windows = Assert.Single(report.Projects, p => p.Platform == XnaPlatform.Windows);
        Assert.Single(windows.ContentProjects);
        Assert.All(windows.ContentItems, i => Assert.False(i.InProject));
        var ids = RuleIds(report).ToHashSet();
        Assert.Superset(new HashSet<string> { "XNA001", "XNA005", "XNA007", "XNA022", "XNA024", "XNA100" }, ids);
        Assert.DoesNotContain(ids, id => id is "XNA101" or "XNA102" or "XNA103" or "XNA104" or "XNA105");
    }

    [Theory]
    [InlineData("xna31-legacy", XnaVersion.V31)]
    [InlineData("xna40-basic", XnaVersion.V40)]
    public async Task Existing_fixtures_keep_their_version_and_get_no_older_rules(string fixture, XnaVersion version)
    {
        var report = await AnalyzeAsync(fixture);

        Assert.All(report.Projects, p => Assert.Equal(version, p.XnaVersion));
        Assert.DoesNotContain(RuleIds(report), id => id is "XNA100" or "XNA101" or "XNA102" or "XNA103" or "XNA104" or "XNA105");
    }

    [Fact]
    public void Importer_map_treats_the_legacy_texture_processors_as_stock_and_resolves_them()
    {
        var map = ImporterMap.Default;

        Assert.True(map.IsStock("TextureImporter", "SpriteTextureProcessor"));
        Assert.True(map.IsStock("TextureImporter", "ModelTextureProcessor"));
        Assert.Equal("TextureProcessor", map.Resolve("TextureImporter", "SpriteTextureProcessor", out var alias));
        Assert.NotNull(alias);
        Assert.Equal("TextureProcessor", map.Resolve("TextureImporter", "TextureProcessor", out var none));
        Assert.Null(none);
        Assert.True(map.IsCustom("TextureImporter", "OutlineProcessor"));
    }

    // ---------- methodDeclaration ----------
    private const string _fw = "Microsoft.Xna.Framework";

    private static IReadOnlyList<SourceMatch> Match(string code, bool overrides = true, string name = "LoadGraphicsContent")
    {
        var rule = new Rule("T1", "t", Severity.Error, Classification.Manual, null, null, new MethodDeclarationMatch(name, [_fw], overrides), "e", "r", null);
        return SourceRuleMatcher.Match(CSharpSyntaxTree.ParseText(code), [rule]);
    }

    [Fact]
    public void Override_with_the_namespace_imported_is_found()
    {
        var matches = Match("using Microsoft.Xna.Framework;\nclass G : Game\n{\n    protected override void LoadGraphicsContent(bool all) { }\n}\n");

        var match = Assert.Single(matches);
        Assert.Equal(4, match.Line);
    }

    [Fact]
    public void Non_override_is_not_found_when_overrides_is_required_but_found_without_it()
    {
        const string code = "using Microsoft.Xna.Framework;\nclass G\n{\n    void LoadGraphicsContent(bool all) { }\n}\n";

        Assert.Empty(Match(code, overrides: true));
        Assert.Single(Match(code, overrides: false));
    }

    [Fact]
    public void Same_name_without_the_namespace_or_with_another_name_is_not_found()
    {
        Assert.Empty(Match("class G\n{\n    protected override void LoadGraphicsContent(bool all) { }\n}\n"));
        Assert.Empty(Match("using Microsoft.Xna.Framework;\nclass G\n{\n    protected override void LoadContent() { }\n}\n"));
    }

    [Fact]
    public void Method_declaration_match_round_trips_through_the_catalog_json()
    {
        const string json = """{"type":"methodDeclaration","name":"X","namespaces":["A"],"overrides":true}""";

        var match = Assert.IsType<MethodDeclarationMatch>(JsonSerializer.Deserialize<RuleMatch>(json, RuleCatalogJson.Options));

        Assert.Equal("X", match.Name);
        Assert.True(match.Overrides);
        Assert.Contains("\"methodDeclaration\"", JsonSerializer.Serialize<RuleMatch>(match, RuleCatalogJson.Options));
    }
}
