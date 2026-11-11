using System.Text.Json;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Rules;

/// <summary>
/// Every rule in the real catalog has one positive and one negative case in <c>rule-cases.json</c>
/// (rules.md: "Add a positive fixture test and a negative fixture test"). The positive case must produce
/// a finding for the rule and the negative case must not.
/// </summary>
[Trait("Category", "Rules")]
public sealed class RuleCaseTests : IDisposable
{
    private static readonly Lazy<RuleCatalog> _catalog = new(() =>
        RuleCatalogLoader.LoadAsync(CatalogPath, new ReadOnlyFileSystem(), CancellationToken.None).GetAwaiter().GetResult());

    private static readonly Lazy<IReadOnlyList<RuleCase>> _cases = new(() =>
        JsonSerializer.Deserialize<List<RuleCase>>(File.ReadAllText(CasesPath), new JsonSerializerOptions(JsonSerializerDefaults.Web))!);

    private readonly string _work = Path.Combine(Path.GetTempPath(), "xnareborn-rulecase-" + Guid.NewGuid().ToString("N"));

    private static string RepoRoot => Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", ".."));

    private static string CatalogPath => Path.Combine(RepoRoot, "rules", "xna-to-monogame.json");

    private static string CasesPath => Path.Combine(RepoRoot, "tests", "XnaReborn.Tests", "Rules", "rule-cases.json");

    public static IEnumerable<object[]> RuleIds() =>
        _catalog.Value.Rules.Select(r => new object[] { r.Id });

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    [Fact]
    public void Every_rule_has_exactly_one_case_with_a_positive_and_a_negative_snippet()
    {
        var ruleIds = _catalog.Value.Rules.Select(r => r.Id).ToList();
        var caseIds = _cases.Value.Select(c => c.RuleId).ToList();

        Assert.Equal(ruleIds.Count, ruleIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(ruleIds.Except(caseIds, StringComparer.Ordinal));
        Assert.Empty(caseIds.Except(ruleIds, StringComparer.Ordinal));
        Assert.Equal(caseIds.Count, caseIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(_cases.Value, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Positive), c.RuleId + " has no positive case");
            Assert.False(string.IsNullOrWhiteSpace(c.Negative), c.RuleId + " has no negative case");
            Assert.NotEqual(c.Positive, c.Negative);
        });
    }

    [Fact]
    public void Rule_ids_follow_the_catalog_pattern_and_autofixable_rules_link_a_task()
    {
        Assert.All(_catalog.Value.Rules, r =>
        {
            Assert.Matches("^XNA[0-9]{3}$", r.Id);
            if (r.Classification == Classification.AutoFixable)
            {
                Assert.False(string.IsNullOrEmpty(r.MigratorTask), r.Id + " is AutoFixable without a migratorTask");
            }
        });
    }

    [Theory]
    [MemberData(nameof(RuleIds))]
    public async Task Positive_case_produces_a_finding_for_the_rule(string ruleId)
    {
        var report = await AnalyzeAsync(ruleId, positive: true);

        Assert.Contains(report.Findings, f => f.RuleId == ruleId);
    }

    [Theory]
    [MemberData(nameof(RuleIds))]
    public async Task Negative_case_is_analyzed_and_produces_no_finding_for_the_rule(string ruleId)
    {
        var report = await AnalyzeAsync(ruleId, positive: false);

        Assert.NotEmpty(report.Projects);
        Assert.DoesNotContain(report.Findings, f => f.RuleId == ruleId);
    }

    private async Task<AnalysisReport> AnalyzeAsync(string ruleId, bool positive)
    {
        var testCase = _cases.Value.Single(c => c.RuleId == ruleId);
        var rule = _catalog.Value.Rules.Single(r => r.Id == ruleId);
        var folder = Path.Combine(_work, ruleId + (positive ? "-pos" : "-neg"));
        Directory.CreateDirectory(folder);
        var snippet = positive ? testCase.Positive : testCase.Negative;
        var vb = string.Equals(testCase.Language, "vb", StringComparison.Ordinal);
        var projectFile = vb ? "Game.vbproj" : "Game.csproj";
        var sourceFile = vb ? "Game.vb" : "Game.cs";
        var version = rule.XnaVersions is { Count: > 0 } versions && versions[0] == XnaVersion.V31 ? "v3.1" : "v4.0";

        switch (testCase.Kind)
        {
            case "project":
                File.WriteAllText(Path.Combine(folder, projectFile), snippet);
                break;
            case "source":
                File.WriteAllText(Path.Combine(folder, sourceFile), snippet);
                File.WriteAllText(Path.Combine(folder, projectFile), GameProject(version, compile: sourceFile));
                break;
            case "file":
                File.WriteAllText(Path.Combine(folder, projectFile), GameProject(version));
                Write(Path.Combine(folder, snippet.Replace('/', Path.DirectorySeparatorChar)), "placeholder");
                break;
            case "fileContent":
                {
                    // The first line of the snippet is the relative path, the rest is the file text.
                    File.WriteAllText(Path.Combine(folder, projectFile), GameProject(version));
                    var split = snippet.IndexOf('\n', StringComparison.Ordinal);
                    Write(Path.Combine(folder, snippet[..split].Replace('/', Path.DirectorySeparatorChar)), snippet[(split + 1)..]);
                    break;
                }

            case "content":
                File.WriteAllText(Path.Combine(folder, projectFile), GameProject(version, nested: @"Content\Content.contentproj"));
                Write(Path.Combine(folder, "Content", "Content.contentproj"), snippet);
                break;
            case "font":
                File.WriteAllText(Path.Combine(folder, projectFile), GameProject(version, nested: @"Content\Content.contentproj"));
                Write(Path.Combine(folder, "Content", "Content.contentproj"), "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\" />");
                Write(Path.Combine(folder, "Content", "Fonts", "Test.spritefont"), snippet);
                break;
            default:
                throw new InvalidOperationException($"Unknown case kind '{testCase.Kind}' for {ruleId}.");
        }

        return await new ProjectAnalyzer().AnalyzeAsync(folder, _catalog.Value, null, CancellationToken.None);
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static string GameProject(string xnaVersion, string? compile = null, string? nested = null)
    {
        var compileItem = compile is null ? string.Empty : $"    <Compile Include=\"{compile}\" />\n";
        var nestedItem = nested is null ? string.Empty : $"    <NestedContentProject Include=\"{nested}\"><Project>{{00000000-0000-0000-0000-000000000001}}</Project></NestedContentProject>\n";
        return $"""
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <XnaFrameworkVersion>{xnaVersion}</XnaFrameworkVersion>
                <XnaPlatform>Windows</XnaPlatform>
              </PropertyGroup>
              <ItemGroup>
            {compileItem}{nestedItem}  </ItemGroup>
            </Project>
            """;
    }

    private sealed record RuleCase(string RuleId, string Kind, string Positive, string Negative, string Language = "csharp");
}
