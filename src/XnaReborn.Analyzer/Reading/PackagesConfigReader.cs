using System.Xml;
using System.Xml.Linq;
using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reading;

/// <summary>The packages listed in a <c>packages.config</c> file, and any problem met while reading it.</summary>
/// <param name="Packages">Entries in file order. Empty when there is no file or it cannot be read.</param>
/// <param name="Diagnostics">ANA011 when the file exists but cannot be read.</param>
public sealed record PackagesConfigResult(IReadOnlyList<PackageEntry> Packages, IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>No file, no packages, no problems.</summary>
    public static PackagesConfigResult Empty { get; } = new([], []);
}

/// <summary>Reads the <c>packages.config</c> next to a project as XML. Reads disk only through <see cref="IReadOnlyFileSystem"/>.</summary>
public static class PackagesConfigReader
{
    /// <summary>The file name read next to a project.</summary>
    public const string FileName = "packages.config";

    /// <summary>Diagnostic code for an unreadable packages.config.</summary>
    public const string UnreadableCode = "ANA011";

    /// <summary>Reads <c>packages.config</c> from the project folder. A missing file gives an empty result; a broken file gives a diagnostic.</summary>
    /// <param name="fileSystem">Read-only file system.</param>
    /// <param name="projectDirectory">Absolute folder of the project file.</param>
    /// <param name="root">Absolute discovery root, used to make the diagnostic path relative.</param>
    /// <returns>The entries and diagnostics.</returns>
    public static PackagesConfigResult Read(IReadOnlyFileSystem fileSystem, string projectDirectory, string root)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        var path = Path.Combine(projectDirectory, FileName);
        if (!fileSystem.FileExists(path))
        {
            return PackagesConfigResult.Empty;
        }

        try
        {
            using var stream = fileSystem.OpenRead(path);
            return new PackagesConfigResult(Parse(XDocument.Load(stream)), []);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return new PackagesConfigResult([], [new Diagnostic(UnreadableCode, $"packages.config could not be read: {ex.Message}", ProjectXml.Rel(root, path))]);
        }
    }

    /// <summary>Reads the <c>package</c> elements of a parsed document. Entries without an id are skipped.</summary>
    /// <param name="document">The parsed packages.config.</param>
    /// <returns>The entries in document order.</returns>
    public static IReadOnlyList<PackageEntry> Parse(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Descendants()
            .Where(e => e.Name.LocalName == "package" && !string.IsNullOrWhiteSpace((string?)e.Attribute("id")))
            .Select(e => new PackageEntry(
                ((string)e.Attribute("id")!).Trim(),
                ((string?)e.Attribute("version"))?.Trim() ?? string.Empty,
                ((string?)e.Attribute("targetFramework"))?.Trim(),
                string.Equals((string?)e.Attribute("developmentDependency"), "true", StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
