namespace XnaReborn.Core.Models;

/// <summary>The result of one analysis run across all projects.</summary>
/// <param name="SchemaVersion">Version of the report format. Currently 1.</param>
/// <param name="CatalogName">Name of the rule catalog used.</param>
/// <param name="Projects">Projects that were analyzed, sorted by path.</param>
/// <param name="Findings">Every rule match, sorted.</param>
/// <param name="TriggeredRules">Each rule that matched at least once, once per rule.</param>
/// <param name="CountsBySeverity">Finding counts keyed by severity.</param>
/// <param name="CountsByClassification">Finding counts keyed by classification.</param>
/// <param name="Score">Score from 0 to 100 for the current project state.</param>
/// <param name="ProjectedScore">Score from 0 to 100 if the auto-fixable findings were fixed.</param>
/// <param name="Diagnostics">Problems met during analysis.</param>
/// <param name="NoProjectReason">Why no XNA project was found, or null when at least one was found.</param>
public sealed record AnalysisReport(
    int SchemaVersion,
    string CatalogName,
    IReadOnlyList<ProjectInfo> Projects,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<TriggeredRule> TriggeredRules,
    IReadOnlyDictionary<Severity, int> CountsBySeverity,
    IReadOnlyDictionary<Classification, int> CountsByClassification,
    int Score,
    int ProjectedScore,
    IReadOnlyList<Diagnostic> Diagnostics,
    string? NoProjectReason);
