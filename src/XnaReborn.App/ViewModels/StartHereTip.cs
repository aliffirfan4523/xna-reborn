using XnaReborn.Core.Models;

namespace XnaReborn.App.ViewModels;

/// <summary>One numbered line of the "Start here" tip.</summary>
/// <param name="Number">Position in the shown list, starting at 1.</param>
/// <param name="Text">The line in plain words.</param>
/// <param name="FilterTo">When set, the line is a button that filters the findings list to this classification.</param>
/// <param name="ButtonLabel">Name of the button, or null when the line is not a button.</param>
public sealed record StartHereStep(int Number, string Text, Classification? FilterTo = null, string? ButtonLabel = null)
{
    /// <summary>True when the line filters the list.</summary>
    public bool IsButton => FilterTo is not null;

    /// <summary>True when the line is plain text.</summary>
    public bool IsText => FilterTo is null;

    /// <summary>Name for screen readers: the button label, or the text.</summary>
    public string AutomationName => ButtonLabel ?? Text;

    /// <summary>The line with its number, such as "1. 10 issues are fixed for you when you migrate."</summary>
    public string NumberedText => $"{Number}. {Text}";
}

/// <summary>The "Start here" tip for a report: what to do first, in order. Built from the report only, so it is easy to test.</summary>
/// <param name="BlockerLine">First line when a blocker was found, or null.</param>
/// <param name="Steps">The numbered lines. Lines whose count is zero are left out.</param>
/// <param name="CleanLine">Shown instead of the lines when the report has no findings, or null.</param>
public sealed record StartHereTip(string? BlockerLine, IReadOnlyList<StartHereStep> Steps, string? CleanLine)
{
    private const int _topRules = 3;

    /// <summary>True when the blocker line is shown.</summary>
    public bool HasBlocker => BlockerLine is not null;

    /// <summary>True when the clean-report line is shown.</summary>
    public bool IsClean => CleanLine is not null;

    /// <summary>Builds the tip, or returns null when there is no project to give a tip for.</summary>
    /// <param name="report">The analysis report, or null.</param>
    public static StartHereTip? From(AnalysisReport? report)
    {
        if (report is null || report.NoProjectReason is not null)
        {
            return null;
        }

        if (report.Findings.Count == 0)
        {
            return new StartHereTip(null, [], "Nothing to fix, go to Migration setup.");
        }

        var auto = Count(report, Classification.AutoFixable);
        var manual = Count(report, Classification.Manual);
        var unsupported = Count(report, Classification.Unsupported);

        var steps = new List<StartHereStep>();
        if (auto > 0)
        {
            steps.Add(new(0, $"{Issues(auto)} fixed for you when you migrate (score {report.Score} -> {report.ProjectedScore})."));
        }

        if (unsupported > 0)
        {
            var top = TopRules(report, Classification.Unsupported);
            var text = $"{unsupported} use features MonoGame does not have. Remove or replace them. Most: {top}.";
            steps.Add(new(0, text, Classification.Unsupported, "Show unsupported findings"));
        }

        if (manual > 0)
        {
            steps.Add(new(0, $"{manual} need a change by hand. Click one to see how.", Classification.Manual, "Show manual findings"));
        }

        steps.Add(new(0, "Then migrate and build. Build errors come with hints."));
        var numbered = steps.Select((s, i) => s with { Number = i + 1 }).ToList();
        return new StartHereTip(BlockerText(report), numbered, null);
    }

    private static string Issues(int n) => n == 1 ? "1 issue is" : $"{n} issues are";

    private static int Count(AnalysisReport report, Classification c) =>
        report.CountsByClassification.TryGetValue(c, out var n) ? n : 0;

    private static string TopRules(AnalysisReport report, Classification c) =>
        string.Join("; ", report.Findings
            .Where(f => f.Classification == c)
            .GroupBy(f => (f.RuleId, f.Title))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key.RuleId, StringComparer.Ordinal)
            .Take(_topRules)
            .Select(g => $"{g.Key.Title}, {Places(g.Count())}"));

    private static string Places(int n) => n == 1 ? "1 place" : $"{n} places";

    private static string? BlockerText(AnalysisReport report)
    {
        var titles = report.Findings
            .Where(f => f.Severity == Severity.Blocker)
            .Select(f => f.Title)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return titles.Count == 0 ? null : $"Blocker: {string.Join(", ", titles)}. The game cannot run on MonoGame until this is removed or replaced.";
    }
}
