using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Rules.Matchers;

/// <summary>Matches <see cref="ContentItemMatch"/> against the project's content items. Every field that is set must match.</summary>
internal static class ContentItemMatcher
{
    /// <summary>Returns the SourcePath of matching content items, or an empty list.</summary>
    /// <param name="match">The rule match.</param>
    /// <param name="project">The project.</param>
    public static IReadOnlyList<string> Match(ContentItemMatch match, ProjectReadResult project) =>
        project.Info.ContentItems
            .Where(c => (match.Extension is null || string.Equals(Path.GetExtension(c.SourcePath), match.Extension, StringComparison.OrdinalIgnoreCase))
                && (match.Importer is null || (c.Importer is not null && Wildcard.IsMatch(match.Importer, c.Importer)))
                && (match.Processor is null || (c.Processor is not null && Wildcard.IsMatch(match.Processor, c.Processor))))
            .Select(c => c.SourcePath)
            .ToList();
}
