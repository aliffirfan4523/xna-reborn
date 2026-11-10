using System.Text;
using XnaReborn.Core.Models;

namespace XnaReborn.App.ViewModels;

/// <summary>One finding of the report joined with the explanation of its rule, ready to show in the list and the detail pane.</summary>
/// <param name="Finding">The finding.</param>
/// <param name="Rule">The rule that matched, or null when the report has no entry for it.</param>
public sealed record ReportFindingItem(Finding Finding, TriggeredRule? Rule)
{
    private const string _noExplanation = "No explanation is available for this rule.";
    private const string _noResolution = "No resolution is available for this rule.";

    /// <summary>Rule id, such as "XNA001".</summary>
    public string RuleId => Finding.RuleId;

    /// <summary>Short title of the rule.</summary>
    public string Title => Finding.Title;

    /// <summary>How serious the finding is.</summary>
    public Severity Severity => Finding.Severity;

    /// <summary>Who can fix the finding.</summary>
    public Classification Classification => Finding.Classification;

    /// <summary>Name of <see cref="Classification"/> as shown to the user.</summary>
    public string ClassificationLabel => ReportGroup.LabelOf(Classification);

    /// <summary>Why the rule matters.</summary>
    public string Explanation => Rule?.Explanation ?? _noExplanation;

    /// <summary>How to fix the finding.</summary>
    public string Resolution => Rule?.Resolution ?? _noResolution;

    /// <summary>Source file and line such as "Game1.cs:12", or "Project level" when the finding has no position.</summary>
    public string LocationText => Finding.Location is { } l ? $"{l.File}:{l.Line}" : "Project level";

    /// <summary>The project the finding belongs to, as a relative .csproj path.</summary>
    public string Project => Finding.Project;

    /// <summary>The trimmed source line, or an empty string.</summary>
    public string Snippet => Finding.Snippet ?? string.Empty;

    /// <summary>True when there is a snippet to show.</summary>
    public bool HasSnippet => !string.IsNullOrWhiteSpace(Finding.Snippet);

    /// <summary>Text a screen reader announces for the list entry.</summary>
    public string AutomationName => $"{Severity}, {RuleId}: {Title}, {LocationText}";

    /// <summary>True when <paramref name="text"/> occurs (ignoring case) in the rule id, title, file, project or snippet.</summary>
    /// <param name="text">The search text. Empty matches everything.</param>
    public bool Matches(string text) =>
        text.Length == 0
        || Contains(RuleId, text)
        || Contains(Title, text)
        || Contains(Finding.Location?.File, text)
        || Contains(Project, text)
        || Contains(Finding.Snippet, text);

    /// <summary>Plain text of the finding for the clipboard.</summary>
    public string ToClipboardText()
    {
        var sb = new StringBuilder();
        sb.Append(RuleId).Append(": ").AppendLine(Title);
        sb.Append("Severity: ").AppendLine(Severity.ToString());
        sb.Append("Classification: ").AppendLine(Classification.ToString());
        sb.Append("Location: ").Append(LocationText).Append(" (").Append(Project).AppendLine(")");
        sb.AppendLine().AppendLine(Explanation);
        sb.AppendLine().Append("Resolution: ").AppendLine(Resolution);
        if (HasSnippet)
        {
            sb.AppendLine().AppendLine(Snippet);
        }

        return sb.ToString();
    }

    private static bool Contains(string? value, string text) => value?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;
}
