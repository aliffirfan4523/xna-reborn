using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Content;

/// <summary>The asset name of one content item.</summary>
/// <param name="Item">The item.</param>
/// <param name="AssetName">The name the game passes to <c>Content.Load</c>, with '/' separators.</param>
public sealed record AssetNameResult(ContentItem Item, string AssetName);

/// <summary>Several items that share one asset name, compared without regard to case.</summary>
/// <param name="AssetName">The name, spelled as the first item in the group spells it.</param>
/// <param name="Items">All items with that name, in input order.</param>
public sealed record NameConflict(string AssetName, IReadOnlyList<ContentItem> Items);

/// <summary>The names for a list of items and the conflicts among them.</summary>
/// <param name="Names">One entry per input item, in input order. Items with a conflict are kept.</param>
/// <param name="Conflicts">Each conflicting name once, in order of first appearance.</param>
public sealed record NameResolution(IReadOnlyList<AssetNameResult> Names, IReadOnlyList<NameConflict> Conflicts);

/// <summary>Works out the name each content item has at run time, and finds items that share a name.</summary>
public static class AssetNameResolver
{
    /// <summary>
    /// Resolves asset names. A <c>Name</c> override replaces only the file-name part of the path, the same as XNA does,
    /// so <c>Textures/hero.png</c> with the name <c>big</c> becomes <c>Textures/big</c>. Without an override the name is
    /// the path without its extension. Names always use '/' and never start with "./".
    /// </summary>
    /// <param name="items">The content items. SourcePath is relative to the content project folder.</param>
    /// <param name="contentRootDirectory">
    /// The content root directory from the project (<c>ContentRootDirectory</c>). It is only carried for the report
    /// and does not change any name.
    /// </param>
    /// <returns>The names and the conflicts. Nothing is dropped.</returns>
    public static NameResolution Resolve(IReadOnlyList<ContentItem> items, string contentRootDirectory)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(contentRootDirectory);

        var names = new List<AssetNameResult>(items.Count);
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(items));
            names.Add(new AssetNameResult(item, NameOf(item)));
        }

        var conflicts = names
            .GroupBy(n => n.AssetName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => new NameConflict(g.First().AssetName, g.Select(n => n.Item).ToList()))
            .ToList();
        return new NameResolution(names, conflicts);
    }

    private static string NameOf(ContentItem item)
    {
        var path = item.SourcePath.Replace('\\', '/');
        while (path.StartsWith("./", StringComparison.Ordinal))
        {
            path = path[2..];
        }

        var slash = path.LastIndexOf('/');
        var directory = slash < 0 ? string.Empty : path[..slash];
        var fileName = path[(slash + 1)..];

        string name;
        if (!string.IsNullOrWhiteSpace(item.NameOverride))
        {
            name = item.NameOverride.Replace('\\', '/');
        }
        else
        {
            var dot = fileName.LastIndexOf('.');
            name = dot > 0 ? fileName[..dot] : fileName;
        }

        return directory.Length == 0 ? name : directory + "/" + name;
    }
}
