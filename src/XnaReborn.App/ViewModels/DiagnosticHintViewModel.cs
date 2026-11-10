using XnaReborn.Runner.Hints;

namespace XnaReborn.App.ViewModels;

/// <summary>A fix hint as shown on the Build and run page.</summary>
public sealed class DiagnosticHintViewModel
{
    /// <summary>Creates the view model.</summary>
    /// <param name="hint">The hint from the runner.</param>
    public DiagnosticHintViewModel(FixHint hint)
    {
        ArgumentNullException.ThrowIfNull(hint);
        Id = hint.Id;
        Title = hint.Title;
        Advice = hint.Advice;
        RuleId = hint.RuleId;
    }

    /// <summary>Hint id from the catalog, such as H001 or R001.</summary>
    public string Id { get; }

    /// <summary>Short title.</summary>
    public string Title { get; }

    /// <summary>What to do next.</summary>
    public string Advice { get; }

    /// <summary>The related analyzer rule id, such as XNA009, or null.</summary>
    public string? RuleId { get; }

    /// <summary>True when the hint names a related rule.</summary>
    public bool HasRuleId => RuleId is not null;

    /// <summary>The rule line shown under the advice.</summary>
    public string RuleText => RuleId is null ? string.Empty : "Related rule: " + RuleId;
}
