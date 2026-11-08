using System.Xml;
using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reading;

/// <summary>Reads an XNA content project (.contentproj) as XML. Reads disk only through <see cref="IReadOnlyFileSystem"/>.</summary>
public sealed class ContentProjectReader
{
    private const string _parameterPrefix = "ProcessorParameters_";

    private readonly IReadOnlyFileSystem _fileSystem;

    /// <summary>Creates a reader.</summary>
    /// <param name="fileSystem">Read-only file system used for all disk access.</param>
    public ContentProjectReader(IReadOnlyFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>Reads a content project. A missing or invalid file gives an ANA001 diagnostic instead of an exception.</summary>
    /// <param name="contentProjectFullPath">Absolute path of the .contentproj.</param>
    /// <param name="root">Absolute discovery root, used for the diagnostic file path.</param>
    /// <returns>The content items, properties, references, and diagnostics.</returns>
    public ContentProjectReadResult Read(string contentProjectFullPath, string root)
    {
        var relative = ProjectXml.Rel(root, contentProjectFullPath);
        if (!_fileSystem.FileExists(contentProjectFullPath))
        {
            return Failed("file not found", relative);
        }

        try
        {
            var document = ProjectXml.Load(_fileSystem, contentProjectFullPath);
            var properties = ProjectXml.ReadProperties(document);
            var items = ProjectXml.ReadItems(document);

            var contentItems = items
                .Where(i => i.ItemType == "Compile")
                .Select(ToContentItem)
                .ToList();
            var references = items.Where(i => i.ItemType == "Reference").ToList();
            return new ContentProjectReadResult(contentItems, properties, references, []);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return Failed(ex.Message, relative);
        }
    }

    private static ContentProjectReadResult Failed(string reason, string relative) =>
        new(
            [],
            new Dictionary<string, string>(),
            [],
            [new Diagnostic("ANA001", $"Content project could not be read: {reason}", relative)]);

    private static ContentItem ToContentItem(ProjectItem item)
    {
        var parameters = new Dictionary<string, string>();
        foreach (var (name, value) in item.Metadata)
        {
            if (name.StartsWith(_parameterPrefix, StringComparison.Ordinal) && name.Length > _parameterPrefix.Length)
            {
                parameters[name[_parameterPrefix.Length..]] = value;
            }
        }

        return new ContentItem(
            item.Include.Replace('\\', '/'),
            Meta(item, "Name"),
            Meta(item, "Importer"),
            Meta(item, "Processor"),
            parameters);
    }

    private static string? Meta(ProjectItem item, string name) =>
        item.Metadata.TryGetValue(name, out var value) ? value : null;
}

