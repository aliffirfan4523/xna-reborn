using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reporting;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.App.Services;

/// <summary>
/// Real <see cref="IAnalysisService"/>. Loads <c>rules/xna-to-monogame.json</c> and, when they are usable,
/// <c>rules/sharpdx-toolkit-to-monogame.json</c> and <c>rules/monogame-upgrade.json</c> once and reuses them.
/// A Toolkit or MonoGame catalog that is missing or invalid never stops XNA analysis: the report gets an ANA021 or ANA014 diagnostic instead.
/// </summary>
public sealed class AnalysisService : IAnalysisService
{
    private const string _catalogRelativePath = "rules/xna-to-monogame.json";
    private const string _toolkitCatalogRelativePath = "rules/sharpdx-toolkit-to-monogame.json";
    private const string _monoGameCatalogRelativePath = "rules/monogame-upgrade.json";

    private readonly string? _catalogPath;
    private readonly string? _toolkitCatalogPath;
    private readonly string? _monoGameCatalogPath;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private LoadedCatalogs? _loaded;

    /// <summary>Creates the service.</summary>
    /// <param name="catalogPath">XNA catalog file to use, or null to look next to the app and then up to the repository root.</param>
    /// <param name="toolkitCatalogPath">SharpDX Toolkit catalog file to use, or null to look in the same places.</param>
    /// <param name="monoGameCatalogPath">Old MonoGame upgrade catalog file to use, or null to look in the same places.</param>
    public AnalysisService(string? catalogPath = null, string? toolkitCatalogPath = null, string? monoGameCatalogPath = null)
    {
        _catalogPath = catalogPath;
        _toolkitCatalogPath = toolkitCatalogPath;
        _monoGameCatalogPath = monoGameCatalogPath;
    }

    /// <inheritdoc />
    public async Task<AnalysisReport> AnalyzeAsync(string path, IProgress<AnalysisProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var loaded = await GetCatalogsAsync(cancellationToken).ConfigureAwait(false);
        return await new ProjectAnalyzer().AnalyzeAsync(path, loaded.Catalogs, loaded.Diagnostics, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public string ToMarkdown(AnalysisReport report) => MarkdownReportWriter.Write(report);

    /// <inheritdoc />
    public string ToJson(AnalysisReport report) => JsonReportWriter.Write(report);

    private static string FindCatalog(string relativePath)
    {
        // The build copies the catalog next to the app. Walking up finds it when the app runs from another folder.
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("The rule catalog " + relativePath + " was not found next to the app or in a parent folder.");
    }

    private async Task<LoadedCatalogs> GetCatalogsAsync(CancellationToken cancellationToken)
    {
        if (_loaded is { } cached)
        {
            return cached;
        }

        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // A failed or cancelled XNA load is not cached, so the next call tries again.
            if (_loaded is { } again)
            {
                return again;
            }

            var fileSystem = new ReadOnlyFileSystem();
            var xna = await RuleCatalogLoader.LoadAsync(_catalogPath ?? FindCatalog(_catalogRelativePath), fileSystem, cancellationToken).ConfigureAwait(false);
            var catalogs = new List<RuleCatalog> { xna };
            var diagnostics = new List<Diagnostic>();
            try
            {
                catalogs.Add(await RuleCatalogLoader.LoadAsync(_toolkitCatalogPath ?? FindCatalog(_toolkitCatalogRelativePath), fileSystem, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is RuleCatalogException or FileNotFoundException)
            {
                var detail = ex is RuleCatalogException invalid ? string.Join(" ", invalid.Errors.Take(3)) : ex.Message;
                diagnostics.Add(new Diagnostic("ANA021", "The SharpDX Toolkit rule catalog was not loaded, so Toolkit projects are not analyzed: " + detail, _toolkitCatalogRelativePath));
            }

            try
            {
                catalogs.Add(await RuleCatalogLoader.LoadAsync(_monoGameCatalogPath ?? FindCatalog(_monoGameCatalogRelativePath), fileSystem, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is RuleCatalogException or FileNotFoundException)
            {
                var detail = ex is RuleCatalogException invalid ? string.Join(" ", invalid.Errors.Take(3)) : ex.Message;
                diagnostics.Add(new Diagnostic("ANA014", "The old MonoGame rule catalog was not loaded, so old MonoGame projects are not analyzed: " + detail, _monoGameCatalogRelativePath));
            }

            return _loaded = new LoadedCatalogs(catalogs, diagnostics);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private sealed record LoadedCatalogs(IReadOnlyList<RuleCatalog> Catalogs, IReadOnlyList<Diagnostic> Diagnostics);
}
