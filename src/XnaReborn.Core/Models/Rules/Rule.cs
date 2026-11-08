using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models.Rules;

/// <summary>One migration rule: what to find, how serious it is, and how to fix it.</summary>
/// <param name="Id">Rule id, for example XNA001.</param>
/// <param name="Title">Short title.</param>
/// <param name="Severity">How serious a finding is.</param>
/// <param name="Classification">Whether the fix is automatic, manual, or impossible.</param>
/// <param name="Category">Area of the project the rule concerns, or null.</param>
/// <param name="XnaVersions">XNA versions the rule applies to, or null for all.</param>
/// <param name="Match">What the rule looks for.</param>
/// <param name="Explanation">Why this matters.</param>
/// <param name="Resolution">What to do about it.</param>
/// <param name="MigratorTask">Spec task that implements the fix, or null.</param>
public sealed record Rule(
    string Id,
    string Title,
    Severity Severity,
    Classification Classification,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleCategory? Category,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<XnaVersion>? XnaVersions,
    RuleMatch Match,
    string Explanation,
    string Resolution,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MigratorTask);
