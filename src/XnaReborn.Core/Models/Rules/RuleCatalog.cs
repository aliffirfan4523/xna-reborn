using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models.Rules;

/// <summary>A versioned collection of rules. The JSON "$schema" property is ignored.</summary>
/// <param name="Version">Catalog schema version.</param>
/// <param name="Name">Catalog name.</param>
/// <param name="Scoring">Scoring weights, or null to use <see cref="ScoringOptions.Default"/>.</param>
/// <param name="Rules">The rules.</param>
public sealed record RuleCatalog(
    int Version,
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ScoringOptions? Scoring,
    IReadOnlyList<Rule> Rules);
