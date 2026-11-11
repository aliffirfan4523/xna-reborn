using System.Text;
using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reporting;

/// <summary>Writes an <see cref="AnalysisReport"/> as a Markdown document.</summary>
public static class MarkdownReportWriter
{
    /// <summary>Converts the report to Markdown with LF line endings and one trailing newline.</summary>
    /// <param name="report">The report to write.</param>
    /// <returns>The Markdown text.</returns>
    public static string Write(AnalysisReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var blocks = new List<string>
        {
            $"# XNA Reborn compatibility report\n\nRule catalog: {report.CatalogName} (report format {report.SchemaVersion})",
            SummaryBlock(report),
            $"Severity: {SeverityLine(report)}",
            $"Classification: {ClassificationLine(report)}",
            report.NoProjectReason is null ? ProjectsBlock(report) : $"## No XNA project found\n\n{report.NoProjectReason}",
        };

        blocks.AddRange(FindingBlocks(report));

        if (report.TriggeredRules.Count > 0)
        {
            blocks.Add("## Rules");
            blocks.AddRange(report.TriggeredRules.Select(RuleBlock));
        }

        if (report.Diagnostics.Count > 0)
        {
            var lines = report.Diagnostics.Select(d =>
                $"- {d.Code}: {d.Message}" + (d.File is null ? string.Empty : $" (`{d.File}`)"));
            blocks.Add("## Diagnostics\n\n" + string.Join('\n', lines));
        }

        var text = string.Join("\n\n", blocks);
        return text.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static string SummaryBlock(AnalysisReport report) =>
        string.Join(
            '\n',
            "## Summary",
            string.Empty,
            "| Item | Value |",
            "|---|---|",
            $"| Compatibility score | {report.Score} / 100 |",
            $"| Projected score after automatic fixes | {report.ProjectedScore} / 100 |",
            $"| Projects analyzed | {report.Projects.Count} |",
            $"| Findings | {report.Findings.Count} |");

    private static string SeverityLine(AnalysisReport report) =>
        string.Join(", ", new[] { Severity.Blocker, Severity.Error, Severity.Warning, Severity.Info }
            .Select(s => $"{s} {Count(report.CountsBySeverity, s)}"));

    private static string ClassificationLine(AnalysisReport report) =>
        string.Join(", ", new[] { Classification.AutoFixable, Classification.Manual, Classification.Unsupported }
            .Select(c => $"{c} {Count(report.CountsByClassification, c)}"));

    private static int Count<TKey>(IReadOnlyDictionary<TKey, int> counts, TKey key) =>
        counts.TryGetValue(key, out var value) ? value : 0;

    private static string ProjectsBlock(AnalysisReport report)
    {
        var sb = new StringBuilder();
        sb.Append("## Projects\n\n");
        sb.Append("| Project | XNA version | Platform | Profile | Target framework |\n");
        sb.Append("|---|---|---|---|---|");
        foreach (var p in report.Projects)
        {
            var version = p.XnaVersion switch
            {
                XnaVersion.V30 => "3.0",
                XnaVersion.V31 => "3.1",
                XnaVersion.V40 => "4.0",
                _ => "Unknown",
            };
            sb.Append("\n| ").Append(Cell(p.Path))
                .Append(" | ").Append(version)
                .Append(" | ").Append(p.Platform).Append(p.PlatformInferred ? " (inferred)" : string.Empty)
                .Append(" | ").Append(Cell(p.Profile))
                .Append(" | ").Append(Cell(p.TargetFramework))
                .Append(" |");
        }

        return sb.ToString();
    }

    private static string Cell(string? text) =>
        string.IsNullOrEmpty(text) ? "-" : text.Replace("|", "\\|", StringComparison.Ordinal);

    private static IEnumerable<string> FindingBlocks(AnalysisReport report)
    {
        yield return "## Findings";
        if (report.Findings.Count == 0)
        {
            yield return "No findings.";
            yield break;
        }

        foreach (var classification in new[] { Classification.AutoFixable, Classification.Manual, Classification.Unsupported })
        {
            var group = report.Findings.Where(f => f.Classification == classification).ToList();
            if (group.Count == 0)
            {
                continue;
            }

            yield return $"### {classification}\n\n" + string.Join('\n', group.Select(FindingBullet));
        }
    }

    private static string FindingBullet(Finding f)
    {
        var sb = new StringBuilder();
        sb.Append($"- **[{f.Severity}] {f.RuleId}** {f.Title}");
        sb.Append($"\n  - Project: `{f.Project}`");
        if (f.Location is not null)
        {
            sb.Append($"\n  - Location: `{f.Location.File}:{f.Location.Line}:{f.Location.Column}`");
        }

        if (f.Snippet is not null)
        {
            sb.Append($"\n  - Code: `{f.Snippet.Replace('`', '\'')}`");
        }

        return sb.ToString();
    }

    private static string RuleBlock(TriggeredRule rule)
    {
        var text = $"### {rule.Id} {rule.Title}\n\n{rule.Explanation}\n\n**Resolution:** {rule.Resolution}";
        return rule.MigratorTask is null ? text : text + $"\n\nMigrator task: {rule.MigratorTask}";
    }
}
