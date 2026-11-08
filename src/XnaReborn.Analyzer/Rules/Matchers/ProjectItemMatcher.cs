using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Rules.Matchers;

/// <summary>Matches <see cref="ProjectItemMatch"/> against the project's MSBuild items.</summary>
internal static class ProjectItemMatcher
{
    /// <summary>Returns the Include values of matching items, or an empty list.</summary>
    /// <param name="match">The rule match.</param>
    /// <param name="project">The project.</param>
    public static IReadOnlyList<string> Match(ProjectItemMatch match, ProjectReadResult project) =>
        project.Items
            .Where(i => string.Equals(i.ItemType, match.ItemType, StringComparison.OrdinalIgnoreCase)
                && (match.Include is null || Wildcard.IsMatch(match.Include, i.Include)))
            .Select(i => i.Include)
            .ToList();
}
