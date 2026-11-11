using XnaReborn.Analyzer.Discovery;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Analyzer.Scoring;
using XnaReborn.Analyzer.Source;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer;

/// <summary>Analyzes an XNA project, solution or folder and produces a deterministic compatibility report. Never writes to disk.</summary>
public sealed class ProjectAnalyzer
{
    private const int _schemaVersion = 1;

    private readonly IReadOnlyFileSystem _fileSystem;

    /// <summary>Creates an analyzer that reads the real file system.</summary>
    public ProjectAnalyzer()
        : this(new ReadOnlyFileSystem())
    {
    }

    /// <summary>Creates an analyzer that reads through the given file system.</summary>
    /// <param name="fileSystem">Read-only file system used for all disk access.</param>
    public ProjectAnalyzer(IReadOnlyFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <summary>Analyzes the folder, .sln or .csproj at <paramref name="path"/> with one catalog.</summary>
    /// <param name="path">Folder, solution or project path.</param>
    /// <param name="catalog">Rule catalog to evaluate. It runs against the projects of the family it applies to (XNA unless <c>appliesTo</c> says otherwise).</param>
    /// <param name="progress">Receives stage updates, or null.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <returns>The analysis report.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public Task<AnalysisReport> AnalyzeAsync(
        string path,
        RuleCatalog catalog,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return AnalyzeAsync(path, [catalog], progress, cancellationToken);
    }

    /// <summary>Analyzes the folder, .sln or .csproj at <paramref name="path"/> with several catalogs. Each catalog runs only against projects of its family.</summary>
    /// <param name="path">Folder, solution or project path.</param>
    /// <param name="catalogs">Rule catalogs to evaluate. No rule id may appear in two of them.</param>
    /// <param name="progress">Receives stage updates, or null.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <returns>The analysis report. Its catalog name joins the catalog names with " + ".</returns>
    /// <exception cref="RuleCatalogException">A rule id is defined in more than one catalog.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async Task<AnalysisReport> AnalyzeAsync(
        string path,
        IReadOnlyList<RuleCatalog> catalogs,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(catalogs);
        RuleCatalogLoader.EnsureUniqueIds(catalogs);
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new AnalysisProgress("Discovering projects", 0, 1, null));
        var discovery = await new ProjectDiscovery(_fileSystem).DiscoverAsync(path, cancellationToken).ConfigureAwait(false);
        progress?.Report(new AnalysisProgress("Discovering projects", 1, 1, null));

        var analyzed = discovery.Projects.Where(p => catalogs.Any(c => c.AppliesToFamily(p.Family))).ToList();
        var engine = new RuleEngine(catalogs, _fileSystem);
        var reader = new ProjectReader(_fileSystem);
        var scanner = new SourceScanner(_fileSystem);

        var projects = new List<ProjectInfo>();
        var findings = new List<Finding>();
        var diagnostics = new List<Diagnostic>(discovery.Diagnostics);

        for (var i = 0; i < analyzed.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var project = analyzed[i];
            progress?.Report(new AnalysisProgress("Reading projects", i, analyzed.Count, project.RelativePath));

            var read = await reader.ReadAsync(project.FullPath, discovery.Root, cancellationToken).ConfigureAwait(false);
            projects.Add(read.Info);
            diagnostics.AddRange(read.Diagnostics);

            var directory = Path.GetDirectoryName(project.FullPath) ?? discovery.Root;
            findings.AddRange(engine.EvaluateProject(read, directory, project.RelativePath, cancellationToken));

            var scan = await scanner.ScanAsync(read, directory, discovery.Root, project.RelativePath, engine.SourceRulesFor(read.Info.Family), progress, cancellationToken).ConfigureAwait(false);
            findings.AddRange(scan.Findings);
            diagnostics.AddRange(scan.Diagnostics);
            progress?.Report(new AnalysisProgress("Reading projects", i + 1, analyzed.Count, project.RelativePath));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var sorted = findings.Order(FindingComparer.Instance).ToList();
        var scoringByRule = catalogs.SelectMany(c => c.Rules.Select(r => (r.Id, c.Scoring))).DistinctBy(p => p.Id, StringComparer.Ordinal).ToDictionary(p => p.Id, p => p.Scoring, StringComparer.Ordinal);
        var score = Scorer.ComputeByRule(sorted, f => scoringByRule.GetValueOrDefault(f.RuleId));

        var report = new AnalysisReport(
            _schemaVersion,
            string.Join(" + ", catalogs.Select(c => c.Name)),
            projects,
            sorted,
            BuildTriggeredRules(sorted, catalogs),
            Enum.GetValues<Severity>().ToDictionary(s => s, s => sorted.Count(f => f.Severity == s)),
            Enum.GetValues<Classification>().ToDictionary(c => c, c => sorted.Count(f => f.Classification == c)),
            score.Score,
            score.ProjectedScore,
            diagnostics.Distinct().Order(DiagnosticComparer.Instance).ToList(),
            discovery.NoProjectReason ?? (analyzed.Count == 0 ? NoCatalogReason(discovery) : null));

        progress?.Report(new AnalysisProgress("Done", 1, 1, null));
        return report;
    }

    private static string NoCatalogReason(DiscoveryResult discovery) =>
        $"Found {discovery.Projects.Count(p => p.IsMonoGame)} MonoGame project(s), but no rule catalog for them was loaded.";

    private static List<TriggeredRule> BuildTriggeredRules(List<Finding> findings, IReadOnlyList<RuleCatalog> catalogs)
    {
        var ids = findings.Select(f => f.RuleId).ToHashSet(StringComparer.Ordinal);
        return catalogs.SelectMany(c => c.Rules)
            .Where(r => ids.Contains(r.Id))
            .GroupBy(r => r.Id, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .Select(r => new TriggeredRule(r.Id, r.Title, r.Explanation, r.Resolution, r.MigratorTask))
            .ToList();
    }
}
