namespace XnaReborn.Core.Models.Rules;

/// <summary>Weights used to turn findings into a score.</summary>
/// <param name="SeverityWeights">Points per finding by severity.</param>
/// <param name="ClassificationFactors">Multiplier by classification.</param>
public sealed record ScoringOptions(
    IReadOnlyDictionary<Severity, double> SeverityWeights,
    IReadOnlyDictionary<Classification, double> ClassificationFactors)
{
    /// <summary>The weights shipped in rules/xna-to-monogame.json.</summary>
    public static ScoringOptions Default { get; } = new(
        new Dictionary<Severity, double>
        {
            [Severity.Info] = 1,
            [Severity.Warning] = 3,
            [Severity.Error] = 8,
            [Severity.Blocker] = 20,
        },
        new Dictionary<Classification, double>
        {
            [Classification.AutoFixable] = 0.25,
            [Classification.Manual] = 1.0,
            [Classification.Unsupported] = 1.5,
        });
}
