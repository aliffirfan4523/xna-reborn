using XnaReborn.Analyzer.IO;

namespace XnaReborn.Analyzer.Reading;

/// <summary>
/// Finds Compile items that link to files owned by another project, the way Game Studio platform copies do.
/// Never evaluates MSBuild. Reads disk only through <see cref="IReadOnlyFileSystem"/>.
/// </summary>
internal static class ProjectLinkReader
{
    /// <summary>Tells whether a Compile item is a link: it has Link metadata or its Include leaves the project folder.</summary>
    /// <param name="item">A project item.</param>
    internal static bool IsLink(ProjectItem item) =>
        (item.Metadata.TryGetValue("Link", out var link) && !string.IsNullOrWhiteSpace(link))
        || item.Include.StartsWith(@"..\", StringComparison.Ordinal)
        || item.Include.StartsWith("../", StringComparison.Ordinal);

    /// <summary>Counts the Compile items and how many of them are links.</summary>
    /// <param name="items">All items of a project.</param>
    internal static (int Total, int Linked) Count(IReadOnlyList<ProjectItem> items)
    {
        var compile = items.Where(i => i.ItemType == "Compile").ToList();
        return (compile.Count, compile.Count(IsLink));
    }

    /// <summary>Finds the projects that own the linked files, by walking up from each file to the nearest folder with a .csproj.</summary>
    /// <param name="fileSystem">Read-only file system.</param>
    /// <param name="items">All items of the project.</param>
    /// <param name="projectFullPath">Absolute path of the project being read. It is never reported as its own source.</param>
    /// <param name="root">Absolute discovery root. Results are relative to it with '/'.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>Sorted relative project paths, or null when no linked file has an owner.</returns>
    internal static IReadOnlyList<string>? ResolveSources(
        IReadOnlyFileSystem fileSystem,
        IReadOnlyList<ProjectItem> items,
        string projectFullPath,
        string root,
        CancellationToken cancellationToken)
    {
        var projectDirectory = Path.GetDirectoryName(projectFullPath) ?? root;
        var owners = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var item in items.Where(i => i.ItemType == "Compile" && IsLink(i) && i.Include.IndexOfAny(['*', '?']) < 0))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = Path.GetFullPath(Path.Combine(projectDirectory, ProjectXml.ToNative(item.Include)));
            if (FindOwner(fileSystem, Path.GetDirectoryName(file), projectFullPath) is { } owner)
            {
                owners.Add(ProjectXml.Rel(root, owner));
            }
        }

        return owners.Count == 0 ? null : [.. owners];
    }

    private static string? FindOwner(IReadOnlyFileSystem fileSystem, string? directory, string projectFullPath)
    {
        while (!string.IsNullOrEmpty(directory))
        {
            IReadOnlyList<string> projects;
            try
            {
                projects = fileSystem.DirectoryExists(directory) ? fileSystem.EnumerateFiles(directory, "*.csproj").Concat(fileSystem.EnumerateFiles(directory, "*.vbproj")).ToList() : [];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }

            var other = projects.FirstOrDefault(p => !string.Equals(p, projectFullPath, StringComparison.OrdinalIgnoreCase));
            if (other is not null)
            {
                return other;
            }

            if (projects.Count > 0)
            {
                return null;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }
}
