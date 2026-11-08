using XnaReborn.Analyzer.Scoring;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Tests.Scoring;

/// <summary>Checks the score and projected score that <see cref="Scorer"/> computes from findings.</summary>
public sealed class ScorerTests
{
    private static Finding Make(string ruleId, Severity severity, Classification classification) =>
        new(ruleId, "Title " + ruleId, severity, classification, "Game/Game.csproj", null, null);

    private static ScoringOptions Options(
        Severity severity,
        double weight,
        Classification classification,
        double factor) =>
        new(
            new Dictionary<Severity, double> { [severity] = weight },
            new Dictionary<Classification, double> { [classification] = factor });

    private static ScoringOptions FullOptions(double warningWeight, double manualFactor) =>
        new(
            new Dictionary<Severity, double>
            {
                [Severity.Info] = 1,
                [Severity.Warning] = warningWeight,
                [Severity.Error] = 8,
                [Severity.Blocker] = 20,
            },
            new Dictionary<Classification, double>
            {
                [Classification.AutoFixable] = 0.25,
                [Classification.Manual] = manualFactor,
                [Classification.Unsupported] = 1.5,
            });

    /// <summary>No findings give a perfect score.</summary>
    [Fact]
    public void NoFindings_GivesScore100AndProjected100()
    {
        var result = Scorer.Compute([], null);

        Assert.Equal(100, result.Score);
        Assert.Equal(100, result.ProjectedScore);
    }

    /// <summary>One Blocker Manual finding costs 20 points.</summary>
    [Fact]
    public void OneBlockerManual_GivesScore80AndProjected80()
    {
        var result = Scorer.Compute([Make("XNA001", Severity.Blocker, Classification.Manual)], null);

        Assert.Equal(80, result.Score);
        Assert.Equal(80, result.ProjectedScore);
    }

    /// <summary>Ten matches of the same rule count once, so the score equals the single-finding score.</summary>
    [Fact]
    public void TenFindingsOfSameRule_CountOnce()
    {
        var single = Scorer.Compute([Make("XNA001", Severity.Blocker, Classification.Manual)], null);
        var many = Enumerable.Range(0, 10)
            .Select(_ => Make("XNA001", Severity.Blocker, Classification.Manual))
            .ToList();

        var result = Scorer.Compute(many, null);

        Assert.Equal(single, result);
        Assert.Equal(80, result.Score);
    }

    /// <summary>A Warning AutoFixable finding costs 0.75 points, and projection removes it.</summary>
    [Fact]
    public void OneWarningAutoFixable_GivesScore99AndProjected100()
    {
        var result = Scorer.Compute([Make("XNA002", Severity.Warning, Classification.AutoFixable)], null);

        Assert.Equal(99, result.Score);
        Assert.Equal(100, result.ProjectedScore);
    }

    /// <summary>A mix of Blocker Unsupported, Error Manual and Warning AutoFixable gives 61 and 62.</summary>
    [Fact]
    public void MixedFindings_GiveScore61AndProjected62()
    {
        var findings = new[]
        {
            Make("XNA001", Severity.Blocker, Classification.Unsupported),
            Make("XNA002", Severity.Error, Classification.Manual),
            Make("XNA003", Severity.Warning, Classification.AutoFixable),
        };

        var result = Scorer.Compute(findings, null);

        Assert.Equal(61, result.Score);
        Assert.Equal(62, result.ProjectedScore);
    }

    /// <summary>Many distinct Blocker Unsupported rules clamp the score at 0 and never go negative.</summary>
    [Fact]
    public void ManyDistinctBlockerUnsupportedRules_ClampToZero()
    {
        var findings = Enumerable.Range(1, 5)
            .Select(i => Make($"XNA{i:000}", Severity.Blocker, Classification.Unsupported))
            .ToList();

        var result = Scorer.Compute(findings, null);

        Assert.Equal(0, result.Score);
        Assert.Equal(0, result.ProjectedScore);
    }

    /// <summary>Custom options replace the default weight for the given severity.</summary>
    [Fact]
    public void CustomOptions_OverrideDefaults()
    {
        var options = FullOptions(warningWeight: 3, manualFactor: 1);
        var custom = new ScoringOptions(
            new Dictionary<Severity, double>
            {
                [Severity.Info] = 10,
                [Severity.Warning] = 3,
                [Severity.Error] = 8,
                [Severity.Blocker] = 20,
            },
            options.ClassificationFactors);

        var result = Scorer.Compute([Make("XNA001", Severity.Info, Classification.Manual)], custom);

        Assert.Equal(90, result.Score);
    }

    /// <summary>Null options produce the same result as ScoringOptions.Default.</summary>
    [Fact]
    public void NullOptions_BehaveLikeDefault()
    {
        var findings = new[]
        {
            Make("XNA001", Severity.Blocker, Classification.Unsupported),
            Make("XNA002", Severity.Warning, Classification.AutoFixable),
        };

        var withNull = Scorer.Compute(findings, null);
        var withDefault = Scorer.Compute(findings, ScoringOptions.Default);

        Assert.Equal(withDefault, withNull);
    }

    /// <summary>A key missing from the options falls back to the default weight for that key.</summary>
    [Fact]
    public void MissingKeys_FallBackToDefaultForThatKey()
    {
        var empty = new ScoringOptions(
            new Dictionary<Severity, double>(),
            new Dictionary<Classification, double>());

        var result = Scorer.Compute([Make("XNA001", Severity.Blocker, Classification.Manual)], empty);

        Assert.Equal(80, result.Score);
        Assert.Equal(80, result.ProjectedScore);
    }

    /// <summary>A partial options dictionary overrides its own key and uses defaults for the rest.</summary>
    [Fact]
    public void PartialOptions_UseDefaultsForMissingEntries()
    {
        var partial = Options(Severity.Info, 10, Classification.Manual, 1);

        var result = Scorer.Compute([Make("XNA001", Severity.Blocker, Classification.Unsupported)], partial);

        Assert.Equal(70, result.Score);
    }

    /// <summary>A .5 penalty rounds away from zero, so a Warning weight of 1.5 gives 99.</summary>
    [Fact]
    public void HalfPoint_RoundsUp()
    {
        var options = FullOptions(warningWeight: 1.5, manualFactor: 1);

        var result = Scorer.Compute([Make("XNA001", Severity.Warning, Classification.Manual)], options);

        Assert.Equal(99, result.Score);
    }

    /// <summary>The same findings in a different order give the same result.</summary>
    [Fact]
    public void FindingOrder_DoesNotChangeResult()
    {
        var findings = new[]
        {
            Make("XNA001", Severity.Blocker, Classification.Unsupported),
            Make("XNA002", Severity.Error, Classification.Manual),
            Make("XNA003", Severity.Warning, Classification.AutoFixable),
            Make("XNA004", Severity.Info, Classification.Manual),
        };

        var forward = Scorer.Compute(findings, null);
        var reversed = Scorer.Compute(findings.Reverse(), null);

        Assert.Equal(forward, reversed);
    }

    /// <summary>The projected score is never lower than the score across several mixes.</summary>
    [Fact]
    public void ProjectedScore_IsAtLeastScore()
    {
        var mixes = new List<IEnumerable<Finding>>
        {
            Array.Empty<Finding>(),
            new[] { Make("XNA001", Severity.Blocker, Classification.Manual) },
            new[] { Make("XNA001", Severity.Warning, Classification.AutoFixable) },
            new[]
            {
                Make("XNA001", Severity.Blocker, Classification.Unsupported),
                Make("XNA002", Severity.Error, Classification.AutoFixable),
                Make("XNA003", Severity.Info, Classification.Manual),
            },
            Enumerable.Range(1, 6).Select(i => Make($"XNA{i:000}", Severity.Error, Classification.AutoFixable)).ToList(),
        };

        foreach (var mix in mixes)
        {
            var result = Scorer.Compute(mix, null);

            Assert.True(
                result.ProjectedScore >= result.Score,
                $"Projected {result.ProjectedScore} was below score {result.Score}.");
        }
    }

    /// <summary>Null findings throw ArgumentNullException.</summary>
    [Fact]
    public void NullFindings_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Scorer.Compute(null!, null));
    }
}
