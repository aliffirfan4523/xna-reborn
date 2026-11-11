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
            ProjectsOrReasonBlock(report),
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

    // No projects analyzed: explain why. Projects analyzed and still a reason (all MonoGame projects current): list them, then say nothing is left to do.
    private static string ProjectsOrReasonBlock(AnalysisReport report)
    {
        if (report.NoProjectReason is null)
        {
            return ProjectsBlock(report);
        }

        return report.Projects.Count == 0
            ? $"## No XNA project found\n\n{report.NoProjectReason}"
            : ProjectsBlock(report) + $"\n\n## Nothing to migrate\n\n{report.NoProjectReason}";
    }

    private static string ProjectsBlock(AnalysisReport report)
    {
        // The two MonoGame columns appear only when the report has a project that is not XNA, so XNA reports keep their exact shape.
        var withFamily = report.Projects.Any(p => p.Family != ProjectFamily.Xna);
        // The Language column appears only when a project is not written in C#.
        var withLanguage = report.Projects.Any(p => p.Language != SourceLanguage.CSharp);
        var sb = new StringBuilder();
        sb.Append("## Projects\n\n");
        var columns = new List<string> { "Project" };
        if (withLanguage)
        {
            columns.Add("Language");
        }

        if (withFamily)
        {
            columns.AddRange(["Family", "XNA version", "MonoGame version"]);
        }
        else
        {
            columns.Add("XNA version");
        }

        columns.AddRange(["Platform", "Profile", "Target framework"]);
        sb.Append("| ").Append(string.Join(" | ", columns)).Append(" |\n|").Append(string.Concat(columns.Select(_ => "---|"))); foreach (var p in report.Projects)
        {
            var version = p.XnaVersion switch
            {
                XnaVersion.V10 => "1.0",
                XnaVersion.V20 => "2.0",
                XnaVersion.V30 => "3.0",
                XnaVersion.V31 => "3.1",
                XnaVersion.V40 => "4.0",
                _ => "Unknown",
            };
            var isMonoGame = p.Family is ProjectFamily.MonoGameLegacy or ProjectFamily.MonoGameCurrent;
            sb.Append("\n| ").Append(Cell(p.Path));
            if (withLanguage)
            {
                sb.Append(" | ").Append(p.Language == SourceLanguage.VisualBasic ? "Visual Basic" : "C#");
            }

            if (withFamily)
            {
                sb.Append(" | ").Append(p.Family);
            }

            sb.Append(" | ").Append(isMonoGame ? "-" : version);
            if (withFamily)
            {
                sb.Append(" | ").Append(isMonoGame ? MonoGameVersionText(p.MonoGameVersion) : "-");
            }

            sb.Append(" | ").Append(isMonoGame ? p.MonoGamePlatform : p.Platform).Append(!isMonoGame && p.PlatformInferred ? " (inferred)" : string.Empty)
                .Append(" | ").Append(Cell(p.Profile))
                .Append(" | ").Append(Cell(p.TargetFramework))
                .Append(" |");
        }

        return sb.ToString();
    }

    private static string MonoGameVersionText(MonoGameVersionInfo? info)
    {
        if (info is null || (info.Exact is null && info.Band == MonoGameBand.Unknown))
        {
            return "Unknown";
        }

        var text = info.Exact ?? info.Band switch
        {
            MonoGameBand.V3Early => "3.0 to 3.4",
            MonoGameBand.V35 => "3.5",
            MonoGameBand.V36 => "3.6",
            MonoGameBand.V37 => "3.7",
            MonoGameBand.V38 => "3.8",
            MonoGameBand.V32To36 => "3.2 to 3.6",
            MonoGameBand.V33To381 => "3.3 to 3.8.1",
            MonoGameBand.V35Plus => "3.5 or later",
            MonoGameBand.V380Era => "3.8.0 era",
            _ => "Unknown",
        };
        return $"{text} ({info.Confidence})";
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
