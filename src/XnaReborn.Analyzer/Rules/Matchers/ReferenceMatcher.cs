using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Rules.Matchers;

/// <summary>Matches <see cref="ReferenceMatch"/> against the project's assembly references.</summary>
internal static class ReferenceMatcher
{
    /// <summary>Returns the matching reference names, or an empty list.</summary>
    /// <param name="match">The rule match.</param>
    /// <param name="project">The project.</param>
    public static IReadOnlyList<string> Match(ReferenceMatch match, ProjectReadResult project) =>
        project.Info.References.Where(r => Wildcard.IsMatch(match.Name, r)).ToList();
}
