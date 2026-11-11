using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Rules.Matchers;

/// <summary>Matches <see cref="ProjectLinksMatch"/>: a project whose Compile items are mostly links to other projects' files.</summary>
internal static class ProjectLinksMatcher
{
    /// <summary>Returns one line such as <c>3 of 3 Compile items are links</c> when the linked share reaches the minimum, or an empty list.</summary>
    /// <param name="match">The rule match.</param>
    /// <param name="project">The project.</param>
    public static IReadOnlyList<string> Match(ProjectLinksMatch match, ProjectReadResult project)
    {
        var (total, linked) = ProjectLinkReader.Count(project.Items);
        if (total == 0 || linked == 0 || (double)linked / total < match.MinLinkedRatio)
        {
            return [];
        }

        return [$"{linked} of {total} Compile items are links"];
    }
}
