using VerifyXunit;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reporting;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Reporting;

/// <summary>Snapshots of the Markdown report for each fixture, analyzed with the real catalog <c>rules/xna-to-monogame.json</c>.</summary>
public class RealCatalogSnapshotTests
{
    private static string CatalogPath => Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json"));

    public static IEnumerable<object[]> NameRows() => FixturePaths.NameRows();

    [Theory]
    [MemberData(nameof(NameRows))]
    public async Task Markdown_report_with_the_real_catalog_matches_snapshot(string name)
    {
        var catalog = await RuleCatalogLoader.LoadAsync(CatalogPath, new ReadOnlyFileSystem(), CancellationToken.None);
        var report = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of(name), catalog, null, CancellationToken.None);

        var text = MarkdownReportWriter.Write(report);

        Assert.DoesNotContain(FixturePaths.Root, text, StringComparison.OrdinalIgnoreCase);
        await Verifier.Verify(text, "md").UseParameters(name);
    }
}
