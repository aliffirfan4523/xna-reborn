using System.Xml;
using System.Xml.Linq;
using XnaReborn.Analyzer.Discovery;
using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reading;

/// <summary>Reads one .csproj as XML and builds a <see cref="ProjectInfo"/>. Never evaluates MSBuild. Reads disk only through <see cref="IReadOnlyFileSystem"/>.</summary>
public sealed class ProjectReader
{
    private readonly IReadOnlyFileSystem _fileSystem;

    /// <summary>Creates a reader.</summary>
    /// <param name="fileSystem">Read-only file system used for all disk access.</param>
    public ProjectReader(IReadOnlyFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>Reads a project. A file that cannot be read gives minimal info and an ANA001 diagnostic instead of an exception.</summary>
    /// <param name="projectFullPath">Absolute path of the .csproj.</param>
    /// <param name="root">Absolute discovery root. <c>Info.Path</c> is relative to it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The project info, properties, items, and diagnostics.</returns>
    public Task<ProjectReadResult> ReadAsync(string projectFullPath, string root, CancellationToken cancellationToken)
    {
        return Task.Run(() => Read(projectFullPath, root, cancellationToken), cancellationToken);
    }

    private static XnaPlatform ParsePlatform(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "windows" => XnaPlatform.Windows,
        "xbox 360" => XnaPlatform.Xbox360,
        "zune" => XnaPlatform.Zune,
        "windows phone" => XnaPlatform.WindowsPhone,
        _ => XnaPlatform.Unknown,
    };

    private static string? Prop(IReadOnlyDictionary<string, string> properties, string name) =>
        properties.TryGetValue(name, out var value) ? value : null;

    /// <summary>A NestedContentProject item, or a ProjectReference to a .contentproj (the XnaReferenceType Content form).</summary>
    private static bool IsContentProjectItem(ProjectItem item) =>
        item.ItemType == "NestedContentProject"
        || (item.ItemType == "ProjectReference" && item.Include.Trim().EndsWith(".contentproj", StringComparison.OrdinalIgnoreCase));

    private static string AssemblySimpleName(string include)
    {
        var comma = include.IndexOf(',', StringComparison.Ordinal);
        return (comma < 0 ? include : include[..comma]).Trim();
    }

    private ProjectReadResult Read(string projectFullPath, string root, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var relative = ProjectXml.Rel(root, projectFullPath);
        XDocument document;
        try
        {
            document = ProjectXml.Load(_fileSystem, projectFullPath);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return Minimal(projectFullPath, relative, ex.Message);
        }

        var properties = ProjectXml.ReadProperties(document);
        var items = ProjectXml.ReadItems(document);
        var diagnostics = new List<Diagnostic>();
        var projectDirectory = Path.GetDirectoryName(projectFullPath) ?? root;

        var version = XnaVersionDetector.Detect(properties, items, relative);
        if (version.Conflict is not null)
        {
            diagnostics.Add(version.Conflict);
        }

        var references = items
            .Where(i => i.ItemType == "Reference")
            .Select(i => AssemblySimpleName(i.Include))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var compileFiles = new List<string>();
        foreach (var item in items.Where(i => i.ItemType == "Compile"))
        {
            compileFiles.AddRange(ProjectXml.ExpandCompile(_fileSystem, projectDirectory, item.Include, ct));
        }

        var defines = ProjectXml.PropertyElements(document)
            .Where(e => e.Name.LocalName == "DefineConstants")
            .SelectMany(e => e.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var contentProjects = new List<string>();
        var contentItems = new List<ContentItem>();
        var contentReader = new ContentProjectReader(_fileSystem);
        var seenContentProjects = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var nested in items.Where(IsContentProjectItem))
        {
            ct.ThrowIfCancellationRequested();
            var full = Path.GetFullPath(Path.Combine(projectDirectory, ProjectXml.ToNative(nested.Include)));
            if (!seenContentProjects.Add(full))
            {
                continue;
            }

            contentProjects.Add(ProjectXml.Rel(root, full));
            var content = contentReader.Read(full, root);
            contentItems.AddRange(content.Items);
            diagnostics.AddRange(content.Diagnostics);
        }

        var info = new ProjectInfo(
            relative,
            Prop(properties, "AssemblyName") is { Length: > 0 } name ? name : Path.GetFileNameWithoutExtension(projectFullPath),
            XnaProjectDetector.IsXna(document),
            version.Version,
            ParsePlatform(Prop(properties, "XnaPlatform")),
            Prop(properties, "XnaProfile"),
            Prop(properties, "TargetFrameworkVersion") ?? Prop(properties, "TargetFramework"),
            references,
            compileFiles,
            defines,
            contentProjects,
            contentItems);
        return new ProjectReadResult(info, properties, items, diagnostics);
    }

    private static ProjectReadResult Minimal(string projectFullPath, string relative, string reason)
    {
        var info = new ProjectInfo(
            relative,
            Path.GetFileNameWithoutExtension(projectFullPath),
            false,
            XnaVersion.Unknown,
            XnaPlatform.Unknown,
            null,
            null,
            [],
            [],
            [],
            [],
            []);
        return new ProjectReadResult(
            info,
            new Dictionary<string, string>(),
            [],
            [new Diagnostic("ANA001", $"Project file could not be read: {reason}", relative)]);
    }
}
