using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Planning;

/// <summary>A project that is left out of the migration because another project of the same game is migrated instead.</summary>
/// <param name="Project">Relative path of the project that is skipped.</param>
/// <param name="Duplicates">Relative path of the project whose code this one shares (its link target when it is a copy).</param>
/// <param name="Kept">Relative path of the project that is migrated for this group.</param>
/// <param name="Reason">Plain-language reason, written to the manifest.</param>
public sealed record PlatformSkip(string Project, string Duplicates, string Kept, string Reason);

/// <summary>The skip decisions for one analysis report.</summary>
/// <param name="Skips">One entry per project that is skipped, sorted by project path.</param>
/// <param name="Notes">One line per group of device-only projects, saying which one was chosen and why.</param>
public sealed record PlatformCopyPlan(IReadOnlyList<PlatformSkip> Skips, IReadOnlyList<string> Notes);

/// <summary>
/// Groups the projects of one report that share code through links (Game Studio platform copies) and picks one
/// project to migrate per group. Pure: it reads only the report.
/// </summary>
public static class PlatformCopies
{
    /// <summary>The rule that marks a project as a copy of another one.</summary>
    public const string CopyRuleId = "XNA052";

    /// <summary>Gets the id of the plan item that skips a project.</summary>
    /// <param name="project">Relative path of the project.</param>
    /// <returns>The plan item id.</returns>
    public static string SkipItemId(string project) => $"{CopyRuleId}:{project}";

    /// <summary>Finds the groups and chooses the project to migrate in each.</summary>
    /// <param name="report">The analysis report.</param>
    /// <returns>The skips and notes. Empty when no project is a copy of another project in the report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is null.</exception>
    public static PlatformCopyPlan Find(AnalysisReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var projects = report.Projects
            .GroupBy(p => p.Path, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var copyPaths = report.Findings
            .Where(f => f.RuleId == CopyRuleId)
            .Select(f => f.Project)
            .ToHashSet(StringComparer.Ordinal);

        // An edge runs from a copy to the project it links its code from.
        var owner = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in copyPaths.Order(StringComparer.Ordinal))
        {
            if (projects.TryGetValue(path, out var info)
                && info.LinkedSourceProjects?.Order(StringComparer.Ordinal).FirstOrDefault(p => projects.ContainsKey(p) && p != path) is { } target)
            {
                owner[path] = target;
            }
        }

        DropCycles(owner);

        var groups = projects.Keys
            .GroupBy(p => Root(p, owner), StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        var skips = new List<PlatformSkip>();
        var notes = new List<string>();
        foreach (var group in groups)
        {
            var members = group.OrderBy(p => p, StringComparer.Ordinal).ToList();
            var kept = members
                .OrderBy(p => Rank(projects[p].Platform))
                .ThenBy(p => owner.ContainsKey(p) ? 1 : 0)
                .ThenBy(p => p, StringComparer.Ordinal)
                .First();

            if (Rank(projects[kept].Platform) > 0)
            {
                notes.Add($"{kept} ({projects[kept].Platform}) was chosen from {string.Join(", ", members)} because the group has no Windows project. The order is Windows Phone, then Xbox 360, then Zune.");
            }

            foreach (var member in members.Where(m => m != kept))
            {
                var duplicates = owner.TryGetValue(member, out var target) ? target : kept;
                var reason = owner.ContainsKey(member)
                    ? $"platform copy of {duplicates}; {kept} is migrated instead"
                    : $"shares its code with the platform copy {kept}, which is migrated instead";
                skips.Add(new PlatformSkip(member, duplicates, kept, reason));
            }
        }

        return new PlatformCopyPlan(skips.OrderBy(s => s.Project, StringComparer.Ordinal).ToList(), notes);
    }

    // Windows (or unknown) first, then Windows Phone, Xbox 360, Zune.
    private static int Rank(XnaPlatform platform) => platform switch
    {
        XnaPlatform.WindowsPhone => 1,
        XnaPlatform.Xbox360 => 2,
        XnaPlatform.Zune => 3,
        _ => 0,
    };

    // A link cycle (A links B and B links A) has no original: none of its projects count as copies.
    private static void DropCycles(Dictionary<string, string> owner)
    {
        var inCycle = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in owner.Keys)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { start };
            var current = start;
            while (owner.TryGetValue(current, out var next))
            {
                if (!seen.Add(next))
                {
                    inCycle.UnionWith(seen);
                    break;
                }

                current = next;
            }
        }

        foreach (var path in inCycle)
        {
            owner.Remove(path);
        }
    }

    private static string Root(string path, Dictionary<string, string> owner)
    {
        var current = path;
        while (owner.TryGetValue(current, out var next))
        {
            current = next;
        }

        return current;
    }
}
