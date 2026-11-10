using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Planning;

/// <summary>Turns the auto-fixable findings of an analysis report into plan items. Pure: no file system, clock or randomness.</summary>
public sealed class MigrationPlanner
{
    /// <summary>Builds the plan, honoring the exclusions in the options.</summary>
    /// <param name="report">Analysis report to plan from.</param>
    /// <param name="options">Migration options carrying the excluded item ids.</param>
    /// <returns>The plan.</returns>
    public MigrationPlan CreatePlan(AnalysisReport report, MigrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return CreatePlan(report, options.ExcludedPlanItemIds);
    }

    /// <summary>Builds the plan, moving the items with the given ids to the excluded list.</summary>
    /// <param name="report">Analysis report to plan from.</param>
    /// <param name="excludedPlanItemIds">Ids of items to exclude. Unknown ids are ignored.</param>
    /// <returns>The plan.</returns>
    public MigrationPlan CreatePlan(AnalysisReport report, IReadOnlySet<string> excludedPlanItemIds)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(excludedPlanItemIds);

        var rules = report.TriggeredRules.GroupBy(r => r.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var items = report.Findings
            .Where(f => f.Classification == Classification.AutoFixable)
            .GroupBy(f => (f.RuleId, f.Project))
            .OrderBy(g => g.Key.RuleId, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Project, StringComparer.Ordinal)
            .Select(g => ToItem(g.Key.RuleId, g.Key.Project, g.ToList(), rules))
            .ToList();

        return new MigrationPlan(
            items.Where(i => !excludedPlanItemIds.Contains(i.Id)).ToList(),
            items.Where(i => excludedPlanItemIds.Contains(i.Id)).ToList());
    }

    private static PlanItem ToItem(string ruleId, string project, List<Finding> findings, Dictionary<string, TriggeredRule> rules)
    {
        var description = rules.TryGetValue(ruleId, out var rule)
            ? $"{rule.Title}: {rule.Resolution}"
            : findings[0].Title;
        if (findings.Count > 1)
        {
            description += $" ({findings.Count} places)";
        }

        var files = findings
            .Select(f => (f.Location?.File ?? project).Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new PlanItem($"{ruleId}:{project}", ruleId, description, files);
    }
}
