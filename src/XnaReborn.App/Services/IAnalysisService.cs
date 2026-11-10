using XnaReborn.Core.Models;

namespace XnaReborn.App.Services;

/// <summary>Analyzes an XNA project and renders the report. Wraps <c>ProjectAnalyzer</c>.</summary>
public interface IAnalysisService
{
    /// <summary>Analyzes the folder, .sln or .csproj at <paramref name="path"/>. Never writes to it.</summary>
    /// <param name="path">Folder, solution or project path.</param>
    /// <param name="progress">Receives stage updates, or null.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <returns>The report.</returns>
    Task<AnalysisReport> AnalyzeAsync(string path, IProgress<AnalysisProgress>? progress, CancellationToken cancellationToken);

    /// <summary>Renders the report as Markdown.</summary>
    /// <param name="report">The report.</param>
    string ToMarkdown(AnalysisReport report);

    /// <summary>Renders the report as JSON.</summary>
    /// <param name="report">The report.</param>
    string ToJson(AnalysisReport report);
}
