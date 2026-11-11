using XnaReborn.Core.Models;
using XnaReborn.Migrator.Source;

namespace XnaReborn.Migrator.Planning;

/// <summary>Turns the auto-fixable findings of an analysis report into plan items. Pure: no file system, clock or randomness.</summary>
public sealed class MigrationPlanner
{
    private const string _deviceProjectRuleId = "XNA007";

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

        // Platform copies: one "skip" item per project that is not migrated (device-to-desktop:5.2). A project whose
        // skip item is excluded is migrated after all, so only an included skip hides the other items of its project.
        var skips = PlatformCopies.Find(report).Skips;
        var skipped = skips.Where(s => !excludedPlanItemIds.Contains(PlatformCopies.SkipItemId(s.Project))).Select(s => s.Project).ToHashSet(StringComparer.Ordinal);
        var platforms = report.Projects.GroupBy(p => p.Path, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Platform, StringComparer.Ordinal);

        var items = report.Findings
            .Where(f => f.Classification == Classification.AutoFixable && f.RuleId != PlatformCopies.CopyRuleId && !skipped.Contains(f.Project))
            .GroupBy(f => (f.RuleId, f.Project))
            .Select(g => ToItem(g.Key.RuleId, g.Key.Project, g.ToList(), rules, platforms))
            .Concat(skips.Select(s => new PlanItem(PlatformCopies.SkipItemId(s.Project), PlatformCopies.CopyRuleId, $"Skip {s.Project}: {s.Reason}. Exclude this item to migrate it too.", [s.Project])))
            .OrderBy(i => i.RuleId, StringComparer.Ordinal)
            .ThenBy(i => i.Id, StringComparer.Ordinal)
            .ToList();

        return new MigrationPlan(
            items.Where(i => !excludedPlanItemIds.Contains(i.Id)).ToList(),
            items.Where(i => excludedPlanItemIds.Contains(i.Id)).ToList());
    }

    private static PlanItem ToItem(string ruleId, string project, List<Finding> findings, Dictionary<string, TriggeredRule> rules, Dictionary<string, XnaPlatform> platforms)
    {
        var description = rules.TryGetValue(ruleId, out var rule)
            ? $"{rule.Title}: {rule.Resolution}"
            : findings[0].Title;
        if (ruleId == _deviceProjectRuleId && platforms.TryGetValue(project, out var platform) && PlatformSymbolSet.For(platform) is { } symbols)
        {
            description += $" Platform {platform}: after conversion the #if symbols {string.Join(", ", symbols.TrueSymbols)} are true and the other platform symbols are false.";
        }

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
