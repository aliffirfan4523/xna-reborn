using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;

namespace XnaReborn.Tests.Fixtures;

/// <summary>Checks that the xna31-rewritable demo fixture only trips the rules the migrator rewrites.</summary>
[Trait("Category", "Analyzer")]
public sealed class RewritableFixtureTests
{
    private const string _fixture = "xna31-rewritable";

    private static string CatalogPath => Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json"));

    private static async Task<AnalysisReport> AnalyzeAsync()
    {
        var catalog = await RuleCatalogLoader.LoadAsync(CatalogPath, new ReadOnlyFileSystem(), CancellationToken.None);
        return await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of(_fixture), catalog, null, CancellationToken.None);
    }

    [Fact]
    public async Task Source_findings_are_only_the_rewritable_rules()
    {
        var report = await AnalyzeAsync();

        var sourceRules = report.Findings
            .Where(f => f.Location is not null)
            .Select(f => f.RuleId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        // XNA053 is the #if WINDOWS || XBOX wrapper in Program.cs; the migrator folds it (device-to-desktop:6.1).
        Assert.Equal(["XNA022", "XNA023", "XNA024", "XNA039", "XNA040", "XNA053"], sourceRules);
    }

    [Fact]
    public async Task Project_level_findings_are_the_expected_set()
    {
        var report = await AnalyzeAsync();

        var projectRules = report.Findings
            .Where(f => f.Location is null)
            .Select(f => f.RuleId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal(["XNA001", "XNA002", "XNA003", "XNA004", "XNA006", "XNA046"], projectRules);
    }

    [Fact]
    public async Task Project_is_reported_as_xna_3_1_windows()
    {
        var report = await AnalyzeAsync();

        var project = Assert.Single(report.Projects);
        Assert.Equal(XnaVersion.V31, project.XnaVersion);
        Assert.DoesNotContain(report.Findings, f => f.RuleId is "XNA005" or "XNA008" or "XNA016" or "XNA017" or "XNA050");
    }
}
