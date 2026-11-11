using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Scoring;

/// <summary>The compatibility score and the score if all auto-fixable findings were fixed.</summary>
/// <param name="Score">Score from 0 to 100 for the project as it is.</param>
/// <param name="ProjectedScore">Score from 0 to 100 with every auto-fixable rule treated as resolved.</param>
public sealed record ScoreResult(int Score, int ProjectedScore);

/// <summary>
/// Turns findings into a compatibility score. Each distinct rule that fired costs
/// <c>severity weight × classification factor</c> points once, however many times it matched.
/// </summary>
public static class Scorer
{
    private const double _perfectScore = 100;

    /// <summary>Computes the score and the projected score.</summary>
    /// <param name="findings">All findings of the analysis.</param>
    /// <param name="options">Weights from the rule catalog, or null to use <see cref="ScoringOptions.Default"/>.</param>
    /// <returns>Integer scores clamped to 0 to 100.</returns>
    public static ScoreResult Compute(IEnumerable<Finding> findings, ScoringOptions? options)
    {
        ArgumentNullException.ThrowIfNull(findings);
        return ComputeByRule(findings, _ => options);
    }

    /// <summary>Computes the score when findings come from several catalogs, each with its own weights.</summary>
    /// <param name="findings">All findings of the analysis.</param>
    /// <param name="optionsFor">Returns the weights of the catalog that owns the finding's rule, or null for the defaults.</param>
    /// <returns>Integer scores clamped to 0 to 100.</returns>
    public static ScoreResult ComputeByRule(IEnumerable<Finding> findings, Func<Finding, ScoringOptions?> optionsFor)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(optionsFor);

        var perRule = findings
            .GroupBy(f => f.RuleId, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        var total = perRule.Sum(f => Penalty(f, optionsFor(f) ?? ScoringOptions.Default));
        var fixable = perRule.Where(f => f.Classification == Classification.AutoFixable).Sum(f => Penalty(f, optionsFor(f) ?? ScoringOptions.Default));

        return new ScoreResult(ToScore(total), ToScore(total - fixable));
    }

    private static double Penalty(Finding finding, ScoringOptions options) =>
        Lookup(options.SeverityWeights, ScoringOptions.Default.SeverityWeights, finding.Severity)
        * Lookup(options.ClassificationFactors, ScoringOptions.Default.ClassificationFactors, finding.Classification);

    private static double Lookup<TKey>(IReadOnlyDictionary<TKey, double> custom, IReadOnlyDictionary<TKey, double> fallback, TKey key)
        where TKey : notnull =>
        custom.TryGetValue(key, out var value) ? value : fallback[key];

    private static int ToScore(double penalty) =>
        (int)Math.Clamp(Math.Round(_perfectScore - penalty, MidpointRounding.AwayFromZero), 0, _perfectScore);
}
