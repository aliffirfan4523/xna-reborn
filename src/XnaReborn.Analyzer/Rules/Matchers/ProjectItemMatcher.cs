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
                && (match.Include is null || Wildcard.IsMatch(match.Include, i.Include))
                && (match.Metadata is null || MetadataPasses(match.Metadata, i)))
            .Select(i => i.Include)
            .ToList();

    // Every filter that is set must pass. An item that lacks the named metadata passes none of them, so a PackageReference
    // without a Version is never reported as "pinned below" a version.
    private static bool MetadataPasses(MetadataFilter filter, ProjectItem item)
    {
        if (!item.Metadata.TryGetValue(filter.Name, out var value))
        {
            return false;
        }

        return (filter.EqualsValue is null || string.Equals(value, filter.EqualsValue, StringComparison.OrdinalIgnoreCase))
            && (filter.NotEquals is null || !string.Equals(value, filter.NotEquals, StringComparison.OrdinalIgnoreCase))
            && (filter.MatchesPattern is null || TextPattern.IsMatch(filter.MatchesPattern, value));
    }
}
