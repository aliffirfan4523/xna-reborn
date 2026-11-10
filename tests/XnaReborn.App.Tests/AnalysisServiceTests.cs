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
