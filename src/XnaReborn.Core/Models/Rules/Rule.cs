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
/// <param name="Languages">Source languages the rule runs for, or null for all.</param>
/// <param name="AutoFixLanguages">For an AutoFixable rule, the languages the migrator can really fix, or null for all. Other languages count the rule as Manual.</param>
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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MigratorTask,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SourceLanguage>? Languages = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SourceLanguage>? AutoFixLanguages = null)
{
    /// <summary>Tells whether the rule runs for projects of a language.</summary>
    /// <param name="language">The project language.</param>
    /// <returns>True when <see cref="Languages"/> is null or lists the language.</returns>
    public bool AppliesTo(SourceLanguage language) => Languages is null || Languages.Contains(language);

    /// <summary>Gets the classification that counts for a project of a language: AutoFixable becomes Manual when the migrator cannot fix the rule in that language.</summary>
    /// <param name="language">The project language.</param>
    /// <returns>The effective classification.</returns>
    public Classification EffectiveClassification(SourceLanguage language) =>
        Classification == Classification.AutoFixable && AutoFixLanguages is not null && !AutoFixLanguages.Contains(language)
            ? Classification.Manual
            : Classification;
}
