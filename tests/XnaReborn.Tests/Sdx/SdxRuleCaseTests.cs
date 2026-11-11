using System.Text.Json;
using System.Text.RegularExpressions;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Sdx;

/// <summary>
/// Task 4: every rule in <c>rules/sharpdx-toolkit-to-monogame.json</c> has one positive and one negative case in
/// <c>sdx-rule-cases.json</c>. The positive case must produce a finding for the rule, the negative must not.
/// Both cases are Toolkit projects (a packages.config names SharpDX.Toolkit), so only the catalog under test decides.
/// </summary>
[Trait("Category", "Sdx")]
public sealed partial class SdxRuleCaseTests : IDisposable
{
    private const string _ns = "http://schemas.microsoft.com/developer/msbuild/2003";

    private static readonly Lazy<IReadOnlyList<RuleCatalog>> _catalogs = new(() =>
        RuleCatalogLoader.LoadAllAsync([Path.Combine(RepoRoot, "rules", "xna-to-monogame.json"), SdxCatalogPath], new ReadOnlyFileSystem(), CancellationToken.None)
            .GetAwaiter().GetResult());

    private static readonly Lazy<IReadOnlyList<RuleCase>> _cases = new(() =>
        JsonSerializer.Deserialize<List<RuleCase>>(File.ReadAllText(Path.Combine(RepoRoot, "tests", "XnaReborn.Tests", "Sdx", "sdx-rule-cases.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!);

    private readonly string _work = Path.Combine(Path.GetTempPath(), "xnareborn-sdxrules-" + Guid.NewGuid().ToString("N"));

    private static string RepoRoot => Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", ".."));

    private static string SdxCatalogPath => Path.Combine(RepoRoot, "rules", "sharpdx-toolkit-to-monogame.json");

    private static RuleCatalog SdxCatalog => _catalogs.Value[1];

    public static IEnumerable<object[]> RuleIds() => SdxCatalog.Rules.Select(r => new object[] { r.Id });

    [GeneratedRegex(@"^\s*- \[[ x]\] (?<id>\d+\.\d+) ", RegexOptions.Multiline)]
    private static partial Regex TaskLine();

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    // ---------- 4.7 the catalog itself ----------
    [Fact]
    public void Catalog_has_45_rules_with_unique_ids_that_follow_the_design_table()
    {
        var ids = SdxCatalog.Rules.Select(r => r.Id).ToList();

        Assert.Equal("sharpdx-toolkit-to-monogame", SdxCatalog.Name);
        Assert.Equal("SharpDxToolkit", SdxCatalog.AppliesTo);
        Assert.Equal(45, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Matches("^SDX[0-9]{3}$", id));
        Assert.Equal(ids.Order(StringComparer.Ordinal), ids);
        Assert.Equal(20, SdxCatalog.Rules.Count(r => r.Classification == Classification.AutoFixable));
        Assert.Equal(18, SdxCatalog.Rules.Count(r => r.Classification == Classification.Manual));
        Assert.Equal(7, SdxCatalog.Rules.Count(r => r.Classification == Classification.Unsupported));
    }

    [Fact]
    public void Ids_do_not_collide_with_the_xna_catalog()
    {
        RuleCatalogLoader.EnsureUniqueIds(_catalogs.Value);
    }

    [Fact]
    public void Every_autofixable_rule_links_a_task_that_exists_in_this_spec()
    {
        var tasks = TaskLine().Matches(File.ReadAllText(Path.Combine(RepoRoot, ".kiro", "specs", "sharpdx-toolkit", "tasks.md")))
            .Select(m => m.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var rule in SdxCatalog.Rules)
        {
            if (rule.Classification == Classification.AutoFixable)
            {
                var link = Assert.IsType<string>(rule.MigratorTask);
                Assert.StartsWith("sharpdx-toolkit:", link, StringComparison.Ordinal);
                Assert.Contains(link["sharpdx-toolkit:".Length..], tasks);
            }
            else
            {
                Assert.Null(rule.MigratorTask);
            }
        }
    }

    [Fact]
    public void Every_rule_has_a_resolution_and_an_explanation_that_admits_syntax_only_matching_for_source_rules()
    {
        Assert.All(SdxCatalog.Rules, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Resolution), r.Id + " has no resolution");
            Assert.False(string.IsNullOrWhiteSpace(r.Explanation), r.Id + " has no explanation");
            if (r.Match is NamespaceMatch or TypeMatch or MemberMatch)
            {
                Assert.Contains("by syntax", r.Explanation, StringComparison.Ordinal);
            }
        });
    }

    [Fact]
    public void Rule_ids_are_grouped_as_the_design_says()
    {
        string Range(string from, string to) => string.Join(",", SdxCatalog.Rules.Select(r => r.Id).Where(id => string.CompareOrdinal(id, from) >= 0 && string.CompareOrdinal(id, to) <= 0));

        Assert.Equal("SDX001,SDX002,SDX003,SDX004,SDX005,SDX006,SDX007,SDX008", Range("SDX001", "SDX009"));
        Assert.Equal("SDX010,SDX011,SDX012,SDX013,SDX014,SDX015", Range("SDX010", "SDX019"));
        Assert.Equal("SDX020,SDX021,SDX022,SDX023,SDX024,SDX025,SDX026,SDX027,SDX028", Range("SDX020", "SDX029"));
        Assert.Equal("SDX030,SDX031,SDX032,SDX033,SDX034,SDX035", Range("SDX030", "SDX039"));
        Assert.Equal(12, Range("SDX040", "SDX059").Split(',').Length);
        Assert.Equal("SDX060,SDX061,SDX062,SDX063", Range("SDX060", "SDX069"));
    }

    [Fact]
    public void Every_rule_has_exactly_one_case_with_a_positive_and_a_negative_snippet()
    {
        var ruleIds = SdxCatalog.Rules.Select(r => r.Id).ToList();
        var caseIds = _cases.Value.Select(c => c.RuleId).ToList();

        Assert.Equal(ruleIds, caseIds.Order(StringComparer.Ordinal));
        Assert.All(_cases.Value, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Positive), c.RuleId + " has no positive case");
            Assert.False(string.IsNullOrWhiteSpace(c.Negative), c.RuleId + " has no negative case");
            Assert.NotEqual(c.Positive, c.Negative);
        });
    }

    // ---------- per-rule cases ----------
    [Theory]
    [MemberData(nameof(RuleIds))]
    public async Task Positive_case_produces_a_finding_for_the_rule(string ruleId)
    {
        var report = await AnalyzeAsync(ruleId, positive: true);

        var finding = Assert.Single(report.Findings, f => f.RuleId == ruleId);
        Assert.Equal(SdxCatalog.Rules.Single(r => r.Id == ruleId).Severity, finding.Severity);
    }

    [Theory]
    [MemberData(nameof(RuleIds))]
    public async Task Negative_case_is_a_toolkit_project_and_produces_no_finding_for_the_rule(string ruleId)
    {
        var report = await AnalyzeAsync(ruleId, positive: false);

        var project = Assert.Single(report.Projects);
        Assert.Equal(ProjectFamily.SharpDxToolkit, project.Family);
        Assert.DoesNotContain(report.Findings, f => f.RuleId == ruleId);
    }

    [Fact]
    public async Task Source_findings_point_at_the_file_and_line_of_the_first_match()
    {
        var report = await AnalyzeAsync("SDX030", positive: true);

        var finding = Assert.Single(report.Findings, f => f.RuleId == "SDX030");
        Assert.Equal(new SourceLocation("Game.cs", 4, 25), finding.Location);
    }

    [Fact]
    public async Task A_project_with_only_xna_compile_items_has_no_content_finding()
    {
        var report = await AnalyzeFragmentAsync("<ItemGroup><Compile Include=\"Content\\a.cs\" /><NestedContentProject Include=\"Content\\Content.contentproj\" /></ItemGroup>");

        Assert.DoesNotContain(report.Findings, f => string.CompareOrdinal(f.RuleId, "SDX010") >= 0 && string.CompareOrdinal(f.RuleId, "SDX015") <= 0);
    }

    [Fact]
    public async Task Rule_cases_use_no_xna_rules_on_a_toolkit_project()
    {
        var report = await AnalyzeAsync("SDX001", positive: true);

        Assert.DoesNotContain(report.Findings, f => f.RuleId.StartsWith("XNA", StringComparison.Ordinal));
    }

    // ---------- fixtures ----------
    public static IEnumerable<object[]> FixtureExpectations() =>
    [
        ["sdx-basic", "SDX001,SDX002,SDX003,SDX010,SDX011,SDX020,SDX021,SDX023,SDX024,SDX025,SDX030,SDX031,SDX040,SDX041,SDX060"],
        ["sdx-effects", "SDX001,SDX002,SDX003,SDX010,SDX013,SDX020,SDX021,SDX023,SDX046,SDX049,SDX051"],
        ["sdx-lowlevel", "SDX001,SDX002,SDX003,SDX005,SDX006,SDX020,SDX021,SDX023,SDX032,SDX033,SDX034,SDX035,SDX043,SDX045,SDX048,SDX050"],
        ["sdx-mixed", "SDX001,SDX002,SDX003,SDX011,SDX020,SDX023,SDX030,XNA001,XNA002,XNA003,XNA004,XNA005,XNA046"],
        ["sdx-prebuilt", "SDX001,SDX002,SDX003,SDX015,SDX020,SDX021"],
        ["sdx-store-app", "SDX001,SDX002,SDX003,SDX004,SDX020"],
        ["sdx-negative", ""],
    ];

    [Theory]
    [MemberData(nameof(FixtureExpectations))]
    public async Task Fixture_triggers_exactly_the_documented_rules(string fixture, string expected)
    {
        var report = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of(fixture), _catalogs.Value, null, CancellationToken.None);

        var ids = string.Join(",", report.Findings.Select(f => f.RuleId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(expected, ids);
    }

    [Fact]
    public async Task Negative_fixture_scores_100_and_fires_no_sdx_rule()
    {
        var report = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of("sdx-negative"), _catalogs.Value, null, CancellationToken.None);

        Assert.DoesNotContain(report.Findings, f => f.RuleId.StartsWith("SDX", StringComparison.Ordinal));
        Assert.Equal(100, report.Score);
    }

    // ---------- helpers ----------
    private Task<AnalysisReport> AnalyzeAsync(string ruleId, bool positive)
    {
        var testCase = _cases.Value.Single(c => c.RuleId == ruleId);
        var snippet = positive ? testCase.Positive : testCase.Negative;
        var folder = Path.Combine(_work, ruleId + (positive ? "-pos" : "-neg"));
        Directory.CreateDirectory(folder);
        switch (testCase.Kind)
        {
            case "project":
                WriteProject(folder, snippet, compile: false);
                break;
            case "source":
                WriteProject(folder, string.Empty, compile: true);
                File.WriteAllText(Path.Combine(folder, "Game.cs"), snippet);
                break;
            case "file":
                WriteProject(folder, string.Empty, compile: false);
                var path = Path.Combine(folder, snippet.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "placeholder");
                break;
            default:
                throw new InvalidOperationException($"Unknown case kind '{testCase.Kind}' for {ruleId}.");
        }

        return new ProjectAnalyzer().AnalyzeAsync(folder, _catalogs.Value, null, CancellationToken.None);
    }

    private Task<AnalysisReport> AnalyzeFragmentAsync(string fragment)
    {
        var folder = Path.Combine(_work, "fragment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        WriteProject(folder, fragment, compile: false);
        return new ProjectAnalyzer().AnalyzeAsync(folder, _catalogs.Value, null, CancellationToken.None);
    }

    // The project is a Toolkit project only through packages.config, which no rule looks at.
    private static void WriteProject(string folder, string fragment, bool compile)
    {
        var compileItem = compile ? "<ItemGroup><Compile Include=\"Game.cs\" /></ItemGroup>" : string.Empty;
        File.WriteAllText(Path.Combine(folder, "Game.csproj"), $"<Project xmlns=\"{_ns}\">{fragment}{compileItem}</Project>");
        File.WriteAllText(Path.Combine(folder, "packages.config"), "<packages><package id=\"SharpDX.Toolkit.Game\" version=\"2.6.3\" /></packages>");
    }

    private sealed record RuleCase(string RuleId, string Kind, string Positive, string Negative);
}
