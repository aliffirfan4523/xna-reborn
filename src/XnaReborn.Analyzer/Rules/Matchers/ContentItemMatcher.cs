using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Content;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Rules.Matchers;

/// <summary>
/// Matches <see cref="ContentItemMatch"/> against the project's content items. Every field that is set must match.
/// With <see cref="ContentItemMatch.NonStandardPair"/> the item must also use a custom importer or processor, judged by <see cref="ImporterMap.Default"/>.
/// </summary>
internal static class ContentItemMatcher
{
    /// <summary>Returns the SourcePath of matching content items, or an empty list.</summary>
    /// <param name="match">The rule match.</param>
    /// <param name="project">The project.</param>
    public static IReadOnlyList<string> Match(ContentItemMatch match, ProjectReadResult project) =>
        project.Info.ContentItems
            .Where(c => (match.Extension is null || string.Equals(Path.GetExtension(c.SourcePath), match.Extension, StringComparison.OrdinalIgnoreCase))
                && (match.Importer is null || (c.Importer is not null && Wildcard.IsMatch(match.Importer, c.Importer)))
                && (match.Processor is null || (c.Processor is not null && Wildcard.IsMatch(match.Processor, c.Processor)))
                && (!match.NonStandardPair || ImporterMap.Default.IsCustom(c.Importer, c.Processor)))
            .Select(c => c.SourcePath)
            .ToList();
}
