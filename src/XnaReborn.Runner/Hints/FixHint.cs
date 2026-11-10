using XnaReborn.Runner.Diagnostics;

namespace XnaReborn.Runner.Hints;

/// <summary>A suggestion for a known build or runtime failure.</summary>
/// <param name="Id">Hint id from the catalog, such as H001 or R001.</param>
/// <param name="Title">Short title.</param>
/// <param name="Advice">What to do next.</param>
/// <param name="RuleId">Related analyzer rule id, such as XNA009, or null.</param>
public sealed record FixHint(string Id, string Title, string Advice, string? RuleId);

/// <summary>Diagnostics that share one hint, so a repeated error shows as one issue with a count.</summary>
/// <param name="Hint">The matching hint, or null for "no known fix".</param>
/// <param name="Items">The diagnostics in this group.</param>
public sealed record DiagnosticGroup(FixHint? Hint, IReadOnlyList<BuildDiagnostic> Items)
{
    /// <summary>Number of diagnostics in the group.</summary>
    public int Count => Items.Count;
}
