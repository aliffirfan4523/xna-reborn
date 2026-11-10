using CommunityToolkit.Mvvm.ComponentModel;
using XnaReborn.Runner.Diagnostics;
using XnaReborn.Runner.Hints;

namespace XnaReborn.App.ViewModels;

/// <summary>Diagnostics that share one fix hint, shown as one expandable entry.</summary>
public sealed partial class DiagnosticGroupViewModel : ObservableObject
{
    /// <summary>True while the group shows its diagnostics.</summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Creates the view model.</summary>
    /// <param name="group">The group from the runner.</param>
    public DiagnosticGroupViewModel(DiagnosticGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        Hint = group.Hint is null ? null : new DiagnosticHintViewModel(group.Hint);
        Items = group.Items.Select(d => new DiagnosticItemViewModel(d)).ToList();
        ErrorCount = Items.Count(i => i.IsError);
        WarningCount = Items.Count(i => i.IsWarning);
        var code = Items.Count > 0 ? Items[0].Code : "unknown";
        Title = Hint?.Title ?? "No known fix for " + code;
    }

    /// <summary>The matching hint, or null for "no known fix".</summary>
    public DiagnosticHintViewModel? Hint { get; }

    /// <summary>The hint title, or "No known fix for" and the code.</summary>
    public string Title { get; }

    /// <summary>What to do next, or null without a hint.</summary>
    public string? Advice => Hint?.Advice;

    /// <summary>True when there is advice.</summary>
    public bool HasAdvice => Advice is not null;

    /// <summary>The related analyzer rule id, or null.</summary>
    public string? RuleId => Hint?.RuleId;

    /// <summary>True when there is a rule id.</summary>
    public bool HasRuleId => RuleId is not null;

    /// <summary>The rule line, such as "Related rule: XNA009", or empty.</summary>
    public string RuleText => Hint?.RuleText ?? string.Empty;

    /// <summary>The diagnostics, errors first.</summary>
    public IReadOnlyList<DiagnosticItemViewModel> Items { get; }

    /// <summary>Number of errors.</summary>
    public int ErrorCount { get; }

    /// <summary>Number of warnings.</summary>
    public int WarningCount { get; }

    /// <summary>True when the group has an error.</summary>
    public bool IsError => ErrorCount > 0;

    /// <summary>True when the group has warnings but no error.</summary>
    public bool IsWarning => ErrorCount == 0 && WarningCount > 0;

    /// <summary>True when the group has neither errors nor warnings.</summary>
    public bool IsInfo => ErrorCount == 0 && WarningCount == 0;

    /// <summary>The counts in words, such as "3 errors, 1 warning".</summary>
    public string CountText
    {
        get
        {
            var parts = new List<string>();
            if (ErrorCount > 0)
            {
                parts.Add(Plural(ErrorCount, "error"));
            }

            if (WarningCount > 0)
            {
                parts.Add(Plural(WarningCount, "warning"));
            }

            var other = Items.Count - ErrorCount - WarningCount;
            if (other > 0)
            {
                parts.Add(Plural(other, "message"));
            }

            return string.Join(", ", parts);
        }
    }

    /// <summary>The name a screen reader reads for the group.</summary>
    public string AutomationName => Title + ", " + CountText;

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
