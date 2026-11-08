namespace XnaReborn.Core.Models;

/// <summary>A rule that matched at least once, with its explanation kept once per report.</summary>
/// <param name="Id">Rule id, such as "XNA001".</param>
/// <param name="Title">Short title of the rule.</param>
/// <param name="Explanation">Why the rule matters.</param>
/// <param name="Resolution">How to fix the match.</param>
/// <param name="MigratorTask">Migrator spec task that automates the fix, or null.</param>
public sealed record TriggeredRule(
    string Id,
    string Title,
    string Explanation,
    string Resolution,
    string? MigratorTask);
