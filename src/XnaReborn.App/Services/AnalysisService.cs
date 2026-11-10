using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reporting;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.App.Services;

/// <summary>Real <see cref="IAnalysisService"/>. Loads <c>rules/xna-to-monogame.json</c> once and reuses it.</summary>
public sealed class AnalysisService : IAnalysisService
{
    private const string _catalogRelativePath = "rules/xna-to-monogame.json";

    private readonly string? _catalogPath;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private RuleCatalog? _catalog;

    /// <summary>Creates the service.</summary>
    /// <param name="catalogPath">Catalog file to use, or null to look next to the app and then up to the repository root.</param>
    public AnalysisService(string? catalogPath = null)
    {
        _catalogPath = catalogPath;
    }

    /// <inheritdoc />
    public async Task<AnalysisReport> AnalyzeAsync(string path, IProgress<AnalysisProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var catalog = await GetCatalogAsync(cancellationToken).ConfigureAwait(false);
        return await new ProjectAnalyzer().AnalyzeAsync(path, catalog, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public string ToMarkdown(AnalysisReport report) => MarkdownReportWriter.Write(report);

    /// <inheritdoc />
    public string ToJson(AnalysisReport report) => JsonReportWriter.Write(report);

    private async Task<RuleCatalog> GetCatalogAsync(CancellationToken cancellationToken)
    {
        if (_catalog is { } cached)
        {
            return cached;
        }

        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // A failed or cancelled load is not cached, so the next call tries again.
            return _catalog ??= await RuleCatalogLoader
                .LoadAsync(_catalogPath ?? FindCatalog(), new ReadOnlyFileSystem(), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static string FindCatalog()
    {
        // The build copies the catalog next to the app. Walking up finds it when the app runs from another folder.
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, _catalogRelativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("The rule catalog " + _catalogRelativePath + " was not found next to the app or in a parent folder.");
    }
}
