using System.Text.RegularExpressions;
using System.Xml.Linq;
using XnaReborn.Analyzer.IO;

namespace XnaReborn.Analyzer.Reading;

/// <summary>Shared XML and path helpers for the project and content project readers. Never evaluates MSBuild.</summary>
internal static class ProjectXml
{
    /// <summary>Loads a project file through the read-only file system.</summary>
    internal static XDocument Load(IReadOnlyFileSystem fileSystem, string fullPath)
    {
        using var stream = fileSystem.OpenRead(fullPath);
        return XDocument.Load(stream);
    }

    /// <summary>Makes <paramref name="full"/> relative to <paramref name="root"/> with '/' separators.</summary>
    internal static string Rel(string root, string full) =>
        Path.GetRelativePath(root, full).Replace('\\', '/');

    /// <summary>Converts MSBuild path text to the current platform's separators.</summary>
    internal static string ToNative(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    /// <summary>Reads every PropertyGroup child; the first value wins per name.</summary>
    internal static Dictionary<string, string> ReadProperties(XDocument document)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in PropertyElements(document))
        {
            properties.TryAdd(element.Name.LocalName, element.Value.Trim());
        }

        return properties;
    }

    /// <summary>Returns every property element in document order, including repeated names.</summary>
    internal static IEnumerable<XElement> PropertyElements(XDocument document) =>
        TopLevel(document, "PropertyGroup").SelectMany(g => g.Elements());

    /// <summary>Reads all items in document order, plus an Import item for each top-level Import.</summary>
    internal static List<ProjectItem> ReadItems(XDocument document)
    {
        var items = new List<ProjectItem>();
        if (document.Root is null)
        {
            return items;
        }

        foreach (var child in document.Root.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "ItemGroup":
                    foreach (var element in child.Elements())
                    {
                        if (element.Attribute("Include")?.Value is { } include)
                        {
                            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name.LocalName != "Include"))
                            {
                                metadata[attribute.Name.LocalName] = attribute.Value.Trim();
                            }

                            foreach (var meta in element.Elements())
                            {
                                metadata[meta.Name.LocalName] = meta.Value.Trim();
                            }

                            // In Visual Basic projects <Import Include="Namespace" /> in an ItemGroup is a namespace import, not an MSBuild Import.
                            var itemType = element.Name.LocalName == "Import" ? "NamespaceImport" : element.Name.LocalName;
                            items.Add(new ProjectItem(itemType, include, metadata));
                        }
                    }

                    break;
                case "Import":
                    if (child.Attribute("Project")?.Value is { } project)
                    {
                        items.Add(new ProjectItem("Import", project, new Dictionary<string, string>()));
                    }

                    break;
                default:
                    break;
            }
        }

        return items;
    }

    /// <summary>Expands an Include that may contain *, ? or ** against a folder. Results use '/' and sort ordinally.</summary>
    internal static List<string> ExpandCompile(IReadOnlyFileSystem fileSystem, string projectDirectory, string include, CancellationToken cancellationToken)
    {
        var normalized = include.Replace('\\', '/');
        if (normalized.IndexOfAny(['*', '?']) < 0)
        {
            return [normalized];
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var results = new SortedSet<string>(StringComparer.Ordinal);
        Walk(fileSystem, projectDirectory, string.Empty, segments, 0, results, cancellationToken);
        return [.. results];
    }

    private static IEnumerable<XElement> TopLevel(XDocument document, string name) =>
        document.Root is null ? [] : document.Root.Elements().Where(e => e.Name.LocalName == name);

    private static bool HasWildcard(string text) => text.IndexOfAny(['*', '?']) >= 0;

    private static Regex ToRegex(string pattern) =>
        new("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string NameOf(string path) =>
        Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    private static void Walk(
        IReadOnlyFileSystem fs,
        string directory,
        string prefix,
        string[] segments,
        int index,
        SortedSet<string> results,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var segment = segments[index];
        var last = index == segments.Length - 1;

        if (segment == "**")
        {
            if (last)
            {
                AddFiles(fs, directory, prefix, "*", results);
            }
            else
            {
                Walk(fs, directory, prefix, segments, index + 1, results, ct);
            }

            foreach (var sub in SafeDirectories(fs, directory))
            {
                if (last)
                {
                    Walk(fs, sub, prefix + NameOf(sub) + "/", ["**"], 0, results, ct);
                }
                else
                {
                    Walk(fs, sub, prefix + NameOf(sub) + "/", segments, index, results, ct);
                }
            }

            return;
        }

        if (HasWildcard(segment))
        {
            if (last)
            {
                AddFiles(fs, directory, prefix, segment, results);
                return;
            }

            var matcher = ToRegex(segment);
            foreach (var sub in SafeDirectories(fs, directory).Where(d => matcher.IsMatch(NameOf(d))))
            {
                Walk(fs, sub, prefix + NameOf(sub) + "/", segments, index + 1, results, ct);
            }

            return;
        }

        var next = Path.Combine(directory, segment);
        if (last)
        {
            if (fs.FileExists(next))
            {
                results.Add(prefix + segment);
            }
        }
        else
        {
            Walk(fs, next, prefix + segment + "/", segments, index + 1, results, ct);
        }
    }

    private static IReadOnlyList<string> SafeDirectories(IReadOnlyFileSystem fs, string directory)
    {
        try
        {
            return fs.EnumerateDirectories(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static void AddFiles(IReadOnlyFileSystem fs, string directory, string prefix, string pattern, SortedSet<string> results)
    {
        IReadOnlyList<string> files;
        try
        {
            files = fs.EnumerateFiles(directory, "*");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        var matcher = ToRegex(pattern);
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            if (matcher.IsMatch(name))
            {
                results.Add(prefix + name);
            }
        }
    }
}
