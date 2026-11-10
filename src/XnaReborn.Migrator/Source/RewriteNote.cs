namespace XnaReborn.Migrator.Source;

/// <summary>A message from a rewriter to the person reading the migration report.</summary>
/// <param name="RuleId">The rule that produced the note.</param>
/// <param name="Message">What happened, in one sentence.</param>
/// <param name="NeedsManualWork">True when the person has to finish or check the change by hand.</param>
public sealed record RewriteNote(string RuleId, string Message, bool NeedsManualWork);
