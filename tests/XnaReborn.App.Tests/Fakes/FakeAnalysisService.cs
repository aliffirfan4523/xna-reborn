using XnaReborn.App.Services;
using XnaReborn.Core.Models;

namespace XnaReborn.App.Tests.Fakes;

/// <summary>Returns <see cref="Report"/> without touching the disk.</summary>
public sealed class FakeAnalysisService : IAnalysisService
{
    /// <summary>What every call returns.</summary>
    public AnalysisReport Report { get; set; } = SampleData.Report();

    /// <summary>Thrown by the next calls when set.</summary>
    public Exception? Failure { get; set; }

    /// <summary>When set, calls wait for this task (or cancellation) before returning, so a test can cancel a running analysis.</summary>
    public Task? Gate { get; set; }

    /// <summary>The paths that were analyzed, in order.</summary>
    public List<string> AnalyzedPaths { get; } = [];

    public async Task<AnalysisReport> AnalyzeAsync(string path, IProgress<AnalysisProgress>? progress, CancellationToken cancellationToken)
    {
        AnalyzedPaths.Add(path);
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new AnalysisProgress("Reading projects", 0, 1, path));
        if (Gate is not null)
        {
            await Gate.WaitAsync(cancellationToken);
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        progress?.Report(new AnalysisProgress("Done", 1, 1, null));
        return Report;
    }

    public string ToMarkdown(AnalysisReport report) => "# Fake report, score " + report.Score;

    public string ToJson(AnalysisReport report) => "{\"score\":" + report.Score + "}";
}
