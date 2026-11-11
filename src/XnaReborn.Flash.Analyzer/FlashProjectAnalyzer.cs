using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Scoring;
using XnaReborn.Core.Models;
using XnaReborn.Flash.Analyzer.Reading;
using XnaReborn.Flash.Analyzer.Rules;

namespace XnaReborn.Flash.Analyzer;

/// <summary>The result of analyzing a Flash project. It has no absolute paths and no timestamps, so it is stable between runs.</summary>
/// <param name="SchemaVersion">Version of the report format. Currently 1.</param>
/// <param name="CatalogName">Name of the rule catalog used.</param>
/// <param name="Kind">The detected project kind.</param>
/// <param name="ProjectFile">Relative path of the project file, or null.</param>
/// <param name="SourceFileCount">Number of project .as files (bundled Flixel excluded).</param>
/// <param name="EntryClass">Name of the single FlxGame subclass, or null.</param>
/// <param name="FlixelVersion">Flixel version read from bundled source, or null.</param>
/// <param name="Findings">Every rule match, sorted.</param>
/// <param name="TriggeredRules">Each rule that matched once, with its explanation.</param>
/// <param name="CountsBySeverity">Finding counts by severity.</param>
/// <param name="Score">Score from 0 to 100 for the project as it is.</param>
/// <param name="ProjectedScore">Score if every auto-fixable finding were resolved.</param>
/// <param name="Diagnostics">Problems met while reading.</param>
public sealed record FlashAnalysisReport(
    int SchemaVersion,
    string CatalogName,
    ProjectKind Kind,
    string? ProjectFile,
    int SourceFileCount,
    string? EntryClass,
    string? FlixelVersion,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<TriggeredRule> TriggeredRules,
    IReadOnlyDictionary<Severity, int> CountsBySeverity,
    int Score,
    int ProjectedScore,
    IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>Read-only analysis of a Flash (AS3 + Flixel) project.</summary>
public static class FlashProjectAnalyzer
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Analyzes the project in <paramref name="path"/>.</summary>
    /// <param name="path">Absolute project folder. It is only read.</param>
    /// <param name="catalog">The rule catalog, or null for the built-in one.</param>
    /// <param name="progress">Receives progress, or null.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public static Task<FlashAnalysisReport> AnalyzeAsync(
        string path,
        FlashRuleCatalog? catalog = null,
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Task.Run(() => Analyze(path, catalog ?? FlashRuleCatalog.LoadBuiltIn(), progress, cancellationToken), cancellationToken);
    }

    private static FlashAnalysisReport Analyze(string path, FlashRuleCatalog catalog, IProgress<AnalysisProgress>? progress, CancellationToken ct)
    {
        var fs = new ReadOnlyFileSystem();
        progress?.Report(new AnalysisProgress("Reading", 0, 3, null));
        var project = FlashProjectReader.Read(fs, path, ct);
        progress?.Report(new AnalysisProgress("Rules", 1, 3, null));
        var findings = FlashRuleEngine.Run(project, catalog, fs, ct);
        progress?.Report(new AnalysisProgress("Scoring", 2, 3, null));

        var score = Scorer.Compute(findings, null);
        var rules = catalog.Rules.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var triggered = findings
            .Select(f => f.RuleId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(id => new TriggeredRule(id, rules[id].Title, rules[id].Explanation, rules[id].Resolution, rules[id].MigratorTask))
            .ToList();
        var counts = new Dictionary<Severity, int>();
        foreach (var s in Enum.GetValues<Severity>().Reverse())
        {
            counts[s] = findings.Count(f => f.Severity == s);
        }

        progress?.Report(new AnalysisProgress("Done", 3, 3, null));
        return new FlashAnalysisReport(
            1,
            catalog.Name,
            project.Detection.Kind,
            project.ProjectFile,
            project.Files.Count(f => !f.IsFlixelSource),
            project.EntryClasses.Count == 1 ? project.EntryClasses[0].Name : null,
            project.FlixelVersion,
            findings,
            triggered,
            counts,
            score.Score,
            score.ProjectedScore,
            project.Diagnostics);
    }

    /// <summary>Writes the report as JSON with LF line endings.</summary>
    /// <param name="report">The report.</param>
    public static string ToJson(FlashAnalysisReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, _json).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    /// <summary>Writes the report as Markdown with LF line endings.</summary>
    /// <param name="report">The report.</param>
    public static string ToMarkdown(FlashAnalysisReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sb = new StringBuilder();
        sb.Append("# Flash project report\n\n");
        sb.Append($"- Kind: {report.Kind}\n");
        sb.Append($"- Entry class: {report.EntryClass ?? "none"}\n");
        sb.Append($"- Flixel version: {report.FlixelVersion ?? "unknown"}\n");
        sb.Append($"- Source files: {report.SourceFileCount}\n");
        sb.Append($"- Score: {report.Score} (projected {report.ProjectedScore})\n\n");
        sb.Append("## Findings\n\n");
        if (report.Findings.Count == 0)
        {
            sb.Append("None.\n");
        }

        foreach (var f in report.Findings)
        {
            var where = f.Location is null ? string.Empty : $" {f.Location.File}:{f.Location.Line}";
            sb.Append($"- {f.RuleId} {f.Severity} {f.Classification}: {f.Title}{where}\n");
        }

        if (report.Diagnostics.Count > 0)
        {
            sb.Append("\n## Diagnostics\n\n");
            foreach (var d in report.Diagnostics)
            {
                sb.Append($"- {d.Code} {d.File}: {d.Message}\n");
            }
        }

        return sb.ToString();
    }
}
