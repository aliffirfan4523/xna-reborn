using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reading;

/// <summary>
/// Finds content that XNA Game Studio Express 1.0 lists inside the game project: <c>Content</c> items with
/// <c>XNAUseContentPipeline</c> set to true. Later versions use a separate content project instead.
/// </summary>
internal static class InProjectContentReader
{
    /// <summary>Turns the in-project content items of a project into content items marked <see cref="ContentItem.InProject"/>.</summary>
    /// <param name="items">All items of the game project.</param>
    /// <returns>One content item per matching item, in document order.</returns>
    internal static IReadOnlyList<ContentItem> Read(IReadOnlyList<ProjectItem> items) =>
        items
            .Where(i => i.ItemType == "Content"
                && i.Metadata.TryGetValue("XNAUseContentPipeline", out var use)
                && string.Equals(use, "true", StringComparison.OrdinalIgnoreCase))
            .Select(i => ContentProjectReader.ToContentItem(i, inProject: true))
            .ToList();
}
