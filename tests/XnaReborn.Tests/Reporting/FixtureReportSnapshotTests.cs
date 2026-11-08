using VerifyXunit;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.Reporting;
using XnaReborn.Core.Models;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Reporting;

/// <summary>Runs the whole analyzer on each fixture with the sample catalog and snapshots both report formats.</summary>
public class FixtureReportSnapshotTests
{
    public static IEnumerable<object[]> NameRows() => FixturePaths.NameRows();

    private static Task<AnalysisReport> AnalyzeAsync(string fixture) =>
        new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of(fixture), SampleCatalog.Create(), null, CancellationToken.None);

    [Theory]
    [MemberData(nameof(NameRows))]
    public async Task Json_report_matches_snapshot(string name)
    {
        var text = JsonReportWriter.Write(await AnalyzeAsync(name));

        Assert.DoesNotContain(FixturePaths.Root, text, StringComparison.OrdinalIgnoreCase);
        await Verifier.Verify(text, "json").UseParameters(name);
    }

    [Theory]
    [MemberData(nameof(NameRows))]
    public async Task Markdown_report_matches_snapshot(string name)
    {
        var text = MarkdownReportWriter.Write(await AnalyzeAsync(name));

        Assert.DoesNotContain(FixturePaths.Root, text, StringComparison.OrdinalIgnoreCase);
        await Verifier.Verify(text, "md").UseParameters(name);
    }

    [Theory]
    [MemberData(nameof(NameRows))]
    public async Task Reports_are_byte_identical_across_runs(string name)
    {
        var first = JsonReportWriter.Write(await AnalyzeAsync(name)) + MarkdownReportWriter.Write(await AnalyzeAsync(name));
        var second = JsonReportWriter.Write(await AnalyzeAsync(name)) + MarkdownReportWriter.Write(await AnalyzeAsync(name));

        Assert.Equal(first, second);
    }
}
