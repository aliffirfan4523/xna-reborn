using VerifyXunit;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reporting;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Reporting;

/// <summary>Task 4: report snapshots of every SharpDX Toolkit fixture, analyzed with the real XNA and Toolkit catalogs together.</summary>
public sealed class SdxCatalogSnapshotTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", ".."));

    public static IEnumerable<object[]> NameRows() => FixturePaths.SharpDxNameRows();

    private static async Task<AnalysisReport> AnalyzeAsync(string fixture)
    {
        var catalogs = await RuleCatalogLoader.LoadAllAsync(
            [Path.Combine(RepoRoot, "rules", "xna-to-monogame.json"), Path.Combine(RepoRoot, "rules", "sharpdx-toolkit-to-monogame.json")],
            new ReadOnlyFileSystem(),
            CancellationToken.None);
        return await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of(fixture), catalogs, null, CancellationToken.None);
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
    public async Task Json_report_matches_snapshot(string name)
    {
        var text = JsonReportWriter.Write(await AnalyzeAsync(name));

        Assert.DoesNotContain(FixturePaths.Root, text, StringComparison.OrdinalIgnoreCase);
        await Verifier.Verify(text, "json").UseParameters(name);
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
