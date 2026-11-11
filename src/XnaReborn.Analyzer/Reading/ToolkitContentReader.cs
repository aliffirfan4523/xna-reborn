using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reading;

/// <summary>One content file of a SharpDX Toolkit project, listed through one of the five Toolkit build actions.</summary>
/// <param name="BuildAction">The build action: ToolkitFxc, ToolkitFont, ToolkitModel, ToolkitTexture or ToolkitAudio.</param>
/// <param name="Item">The file. <see cref="ContentItem.SourcePath"/> is the project-relative path with '/' separators, <see cref="ContentItem.NameOverride"/> is the asset name the game loads, and importer and processor are null because the Toolkit has none.</param>
/// <param name="Link">The <c>Link</c> metadata as written (with '/' separators), or null when the item has none.</param>
public sealed record ToolkitContentItem(string BuildAction, ContentItem Item, string? Link)
{
    /// <summary>The name the game passes to <c>Content.Load</c>, with '/' separators and no extension.</summary>
    public string AssetName => Item.NameOverride ?? string.Empty;
}

/// <summary>
/// Lists the content of a SharpDX Toolkit project. The Toolkit has no content project: the game project holds the assets as items with a build action.
/// The asset name is the <c>Link</c> metadata when there is one, otherwise the project-relative path, without the extension.
/// Pure: it works on the items already read and touches no file.
/// </summary>
public static class ToolkitContentReader
{
    /// <summary>The Toolkit build actions that name content.</summary>
    public static IReadOnlyList<string> BuildActions { get; } = ["ToolkitFont", "ToolkitFxc", "ToolkitModel", "ToolkitTexture", "ToolkitAudio"];

    /// <summary>Reads the Toolkit content items in document order, without repeats.</summary>
    /// <param name="project">The project as read by <see cref="ProjectReader"/>.</param>
    /// <returns>One entry per build-action item that names a file.</returns>
    public static IReadOnlyList<ToolkitContentItem> Read(ProjectReadResult project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var result = new List<ToolkitContentItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in project.Items)
        {
            var action = BuildActions.FirstOrDefault(a => a.Equals(item.ItemType, StringComparison.OrdinalIgnoreCase));
            if (action is null || string.IsNullOrWhiteSpace(item.Include))
            {
                continue;
            }

            var path = Normalize(item.Include);
            if (!seen.Add(action + "|" + path))
            {
                continue;
            }

            var link = item.Metadata.TryGetValue("Link", out var linkValue) && !string.IsNullOrWhiteSpace(linkValue) ? Normalize(linkValue) : null;
            result.Add(new ToolkitContentItem(action, new ContentItem(path, WithoutExtension(link ?? path), null, null, new Dictionary<string, string>()), link));
        }

        return result;
    }

    private static string Normalize(string path)
    {
        var text = path.Trim().Replace('\\', '/');
        while (text.StartsWith("./", StringComparison.Ordinal))
        {
            text = text[2..];
        }

        return text;
    }

    private static string WithoutExtension(string path)
    {
        var slash = path.LastIndexOf('/');
        var dot = path.LastIndexOf('.');
        return dot > slash + 1 ? path[..dot] : path;
    }
}
