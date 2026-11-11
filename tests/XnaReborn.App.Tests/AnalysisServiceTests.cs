using XnaReborn.App.Services;
using XnaReborn.Core.Models;

namespace XnaReborn.App.Tests;

public sealed class AnalysisServiceTests
{
    [Fact]
    public async Task The_real_service_analyzes_a_fixture_project()
    {
        var service = new AnalysisService();
        var stages = new List<string>();
        var progress = new SyncProgress<AnalysisProgress>(p => stages.Add(p.Stage));

        var report = await service.AnalyzeAsync(RepoPaths.Fixture("xna40-basic"), progress, TestContext.Current.CancellationToken);

        Assert.NotEmpty(report.Projects);
        Assert.NotEmpty(report.Findings);
        Assert.InRange(report.Score, 0, 100);
        Assert.Null(report.NoProjectReason);
        Assert.Contains("Done", stages);
        Assert.False(string.IsNullOrWhiteSpace(service.ToMarkdown(report)));
        Assert.Contains("\"score\"", service.ToJson(report), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_call_reuses_the_catalog_and_gives_the_same_report()
    {
        var service = new AnalysisService();
        var path = RepoPaths.Fixture("xna40-basic");

        var first = await service.AnalyzeAsync(path, null, TestContext.Current.CancellationToken);
        var second = await service.AnalyzeAsync(path, null, TestContext.Current.CancellationToken);

        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Findings.Count, second.Findings.Count);
    }

    [Fact]
    public async Task A_folder_without_an_XNA_project_explains_why()
    {
        var report = await new AnalysisService().AnalyzeAsync(RepoPaths.Fixture("not-xna"), null, TestContext.Current.CancellationToken);

        Assert.Empty(report.Projects);
        Assert.False(string.IsNullOrWhiteSpace(report.NoProjectReason));
    }

    [Fact]
    public async Task A_missing_catalog_file_is_an_error_and_the_next_call_tries_again()
    {
        var service = new AnalysisService(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".json"));

        await Assert.ThrowsAnyAsync<Exception>(() => service.AnalyzeAsync(RepoPaths.Fixture("xna40-basic"), null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<Exception>(() => service.AnalyzeAsync(RepoPaths.Fixture("xna40-basic"), null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_toolkit_fixture_is_analyzed_with_the_toolkit_catalog_and_the_report_names_both_catalogs()
    {
        var report = await new AnalysisService().AnalyzeAsync(RepoPaths.Fixture("sdx-basic"), null, TestContext.Current.CancellationToken);

        Assert.Null(report.NoProjectReason);
        Assert.Equal("xna-to-monogame + sharpdx-toolkit-to-monogame + monogame-upgrade", report.CatalogName);
        Assert.Contains(report.Findings, f => f.RuleId == "SDX020");
        Assert.DoesNotContain(report.Findings, f => f.RuleId.StartsWith("XNA", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Diagnostics, d => d.Code == "ANA021");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_missing_or_broken_toolkit_catalog_does_not_stop_xna_analysis_and_is_reported(bool broken)
    {
        var toolkitPath = Path.Combine(Path.GetTempPath(), "sdx-catalog-" + Guid.NewGuid().ToString("N") + ".json");
        if (broken)
        {
            await File.WriteAllTextAsync(toolkitPath, "{ \"version\": 1, \"name\": \"x\", \"rules\": [ { \"id\": \"BAD\" } ] }", TestContext.Current.CancellationToken);
        }

        try
        {
            var service = new AnalysisService(toolkitCatalogPath: toolkitPath);

            var xna = await service.AnalyzeAsync(RepoPaths.Fixture("xna40-basic"), null, TestContext.Current.CancellationToken);
            var toolkit = await service.AnalyzeAsync(RepoPaths.Fixture("sdx-basic"), null, TestContext.Current.CancellationToken);

            Assert.NotEmpty(xna.Findings);
            Assert.Equal("xna-to-monogame + monogame-upgrade", xna.CatalogName);
            var note = Assert.Single(xna.Diagnostics, d => d.Code == "ANA021");
            Assert.Contains("Toolkit", note.Message, StringComparison.Ordinal);
            Assert.Empty(toolkit.Projects);
            Assert.Equal("Found 1 SharpDX Toolkit project(s), but no rule catalog for them was loaded.", toolkit.NoProjectReason);
        }
        finally
        {
            File.Delete(toolkitPath);
        }
    }

    [Fact]
    public async Task A_cancelled_token_stops_the_analysis()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new AnalysisService().AnalyzeAsync(RepoPaths.Fixture("xna40-basic"), null, cts.Token));
    }

    /// <summary>Reports on the calling thread, so a test can read the values right after the call.</summary>
    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SyncProgress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value) => _handler(value);
    }
}
