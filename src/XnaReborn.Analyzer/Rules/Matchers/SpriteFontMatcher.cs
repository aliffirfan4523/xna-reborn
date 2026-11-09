using System.Xml;
using System.Xml.Linq;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Rules.Matchers;

/// <summary>
/// Matches <see cref="SpriteFontFamilyMatch"/> by reading the FontName of every .spritefont file in the project folder
/// and in the folder of each NestedContentProject item.
/// </summary>
internal static class SpriteFontMatcher
{
    private static readonly string[] _skippedFolders = ["bin", "obj", ".git", ".vs"];

    private static readonly StringComparer _pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>Returns "path: FontName" for each matching font file, in ordinal path order. Paths are relative to the project directory and use '/'.</summary>
    /// <param name="match">The rule match.</param>
    /// <param name="project">The project.</param>
    /// <param name="fileSystem">Read-only file system.</param>
    /// <param name="projectDirectory">Absolute project directory.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    public static IReadOnlyList<string> Match(SpriteFontFamilyMatch match, ProjectReadResult project, IReadOnlyFileSystem fileSystem, string projectDirectory, CancellationToken cancellationToken)
    {
        var families = new HashSet<string>(match.Families, StringComparer.OrdinalIgnoreCase);
        var baseDirectory = Path.GetFullPath(projectDirectory);
        var roots = new List<string> { baseDirectory };
        foreach (var item in project.Items)
        {
            if (string.Equals(item.ItemType, "NestedContentProject", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(item.Include))
            {
                var include = item.Include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                var directory = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(baseDirectory, include)));
                if (directory is not null)
                {
                    roots.Add(directory);
                }
            }
        }

        var files = new HashSet<string>(_pathComparer);
        foreach (var root in roots.Distinct(_pathComparer))
        {
            Collect(fileSystem, root, files, cancellationToken);
        }

        var results = new List<(string Path, string Text)>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fontName = ReadFontName(fileSystem, file);
            if (fontName is not null && families.Contains(fontName))
            {
                var relative = Path.GetRelativePath(baseDirectory, file).Replace('\\', '/');
                results.Add((relative, relative + ": " + fontName));
            }
        }

        return results.OrderBy(r => r.Path, StringComparer.Ordinal).Select(r => r.Text).ToList();
    }

    private static void Collect(IReadOnlyFileSystem fs, string directory, HashSet<string> files, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            foreach (var file in fs.EnumerateFiles(directory, "*.spritefont"))
            {
                files.Add(file);
            }

            foreach (var sub in fs.EnumerateDirectories(directory))
            {
                if (!_skippedFolders.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase))
                {
                    Collect(fs, sub, files, token);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string? ReadFontName(IReadOnlyFileSystem fs, string path)
    {
        try
        {
            using var stream = fs.OpenRead(path);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var document = XDocument.Load(reader);
            var name = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "FontName")?.Value.Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
