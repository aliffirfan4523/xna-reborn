using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models.Rules;

/// <summary>A versioned collection of rules. The JSON "$schema" property is ignored.</summary>
/// <param name="Version">Catalog schema version.</param>
/// <param name="Name">Catalog name.</param>
/// <param name="Scoring">Scoring weights, or null to use <see cref="ScoringOptions.Default"/>.</param>
/// <param name="Rules">The rules.</param>
/// <param name="AppliesTo">Project family the catalog runs against: "xna" (also when null) or "monogame". Omitted from JSON when null.</param>
public sealed record RuleCatalog(
    int Version,
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ScoringOptions? Scoring,
    IReadOnlyList<Rule> Rules,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AppliesTo = null)
{
    /// <summary>The <see cref="AppliesTo"/> value for XNA catalogs.</summary>
    public const string Xna = "xna";

    /// <summary>The <see cref="AppliesTo"/> value for MonoGame catalogs.</summary>
    public const string MonoGame = "monogame";

    /// <summary>Tells whether this catalog runs against projects of <paramref name="family"/>.</summary>
    /// <param name="family">The project family.</param>
    /// <returns>True for the Xna family when the catalog is an XNA catalog, and for the two MonoGame families when it is a MonoGame catalog.</returns>
    public bool AppliesToFamily(ProjectFamily family) =>
        string.Equals(AppliesTo ?? Xna, MonoGame, StringComparison.OrdinalIgnoreCase)
            ? family is ProjectFamily.MonoGameLegacy or ProjectFamily.MonoGameCurrent
            : family == ProjectFamily.Xna;
}
