namespace XnaReborn.Migrator.Source;

/// <summary>A rewriter that threw. Its output was discarded.</summary>
/// <param name="RuleId">The rule whose rewriter failed.</param>
/// <param name="Message">The exception message.</param>
public sealed record RewriteFailure(string RuleId, string Message);

/// <summary>Result of running the enabled rewriters over one file.</summary>
/// <param name="Text">The final text. The exact input string when nothing changed.</param>
/// <param name="Changed">True when <paramref name="Text"/> differs from the input.</param>
/// <param name="AppliedRuleIds">Rule IDs of the rewriters that changed the text, in the order they ran.</param>
/// <param name="Notes">Notes from the rewriters that completed.</param>
/// <param name="Failures">Rewriters that threw.</param>
public sealed record FileRewriteResult(
    string Text,
    bool Changed,
    IReadOnlyList<string> AppliedRuleIds,
    IReadOnlyList<RewriteNote> Notes,
    IReadOnlyList<RewriteFailure> Failures);
