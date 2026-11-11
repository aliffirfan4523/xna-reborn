using System.Xml;
using System.Xml.Linq;
using XnaReborn.Analyzer.Discovery;
using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Mgcb;
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
        var diagnostics = new List<Diagnostic>();
        var projectDirectory = Path.GetDirectoryName(projectFullPath) ?? root;
        var items = ProjectXml.ReadItems(document);
        items.AddRange(ReadSharedItems(items, projectDirectory, root, diagnostics, ct));

        var packagesResult = PackagesConfigReader.Read(_fileSystem, projectDirectory, root);
        diagnostics.AddRange(packagesResult.Diagnostics);
        var family = MonoGameProjectDetector.Classify(document, packagesResult.Packages);
        var isMonoGame = family is ProjectFamily.MonoGameLegacy or ProjectFamily.MonoGameCurrent;
        var monoGame = isMonoGame ? MonoGameVersionDetector.Detect(properties, items, packagesResult.Packages, relative) : null;
        if (monoGame?.Conflict is not null)
        {
            diagnostics.Add(monoGame.Conflict);
        }

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

        // An SDK-style project compiles every *.cs file under its folder without listing them.
        // shortcut: Compile Remove and Compile Update items are not read, so a removed file is still scanned; read them if that causes false findings.
        if (isMonoGame && MonoGameProjectDetector.IsSdkStyle(document) && !string.Equals(Prop(properties, "EnableDefaultCompileItems"), "false", StringComparison.OrdinalIgnoreCase))
        {
            var listed = new HashSet<string>(compileFiles, StringComparer.Ordinal);
            foreach (var file in ProjectXml.ExpandCompile(_fileSystem, projectDirectory, "**/*.cs", ct).Where(f => !IsBuildOutput(f)))
            {
                if (listed.Add(file))
                {
                    compileFiles.Add(file);
                }
            }
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

        var mgcbFiles = isMonoGame ? ReadMgcbFiles(items, projectDirectory, root, diagnostics, ct) : [];

        var info = new ProjectInfo(
            relative,
            Prop(properties, "AssemblyName") is { Length: > 0 } name ? name : Path.GetFileNameWithoutExtension(projectFullPath),
            family == ProjectFamily.Xna,
            version.Version,
            ParsePlatform(Prop(properties, "XnaPlatform")),
            Prop(properties, "XnaProfile"),
            Prop(properties, "TargetFrameworkVersion") ?? Prop(properties, "TargetFramework"),
            references,
            compileFiles,
            defines,
            contentProjects,
            contentItems,
            family,
            monoGame?.Version,
            monoGame?.Platform ?? MonoGamePlatform.Unknown,
            mgcbFiles.Count == 0 ? null : mgcbFiles.Select(m => m.Path).ToList());
        return new ProjectReadResult(info, properties, items, diagnostics, packagesResult.Packages, mgcbFiles);
    }

    // bin and obj hold build output, never source (the SDK excludes them by default).
    private static bool IsBuildOutput(string path) =>
        path.Split('/').SkipLast(1).Any(s => s.Equals("bin", StringComparison.OrdinalIgnoreCase) || s.Equals("obj", StringComparison.OrdinalIgnoreCase));

    private const string _conventionalMgcb = "Content/Content.mgcb";

    // Reads each Content.mgcb named by a MonoGameContentReference item, then the conventional Content/Content.mgcb if it is not already listed.
    private List<ParsedMgcb> ReadMgcbFiles(List<ProjectItem> items, string projectDirectory, string root, List<Diagnostic> diagnostics, CancellationToken ct)
    {
        var result = new List<ParsedMgcb>();
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var listed = items.Where(i => i.ItemType == "MonoGameContentReference").Select(i => (Include: i.Include, Listed: true));
        foreach (var (include, isListed) in listed.Append((_conventionalMgcb, false)))
        {
            ct.ThrowIfCancellationRequested();
            var full = Path.GetFullPath(Path.Combine(projectDirectory, ProjectXml.ToNative(include)));
            if (!seen.Add(full))
            {
                continue;
            }

            var relative = ProjectXml.Rel(root, full);
            if (!_fileSystem.FileExists(full))
            {
                if (isListed)
                {
                    diagnostics.Add(new Diagnostic("ANA013", "Content.mgcb listed by the project was not found.", relative));
                }

                continue;
            }

            try
            {
                var file = MgcbParser.Parse(_fileSystem.ReadAllText(full), relative);
                diagnostics.AddRange(file.Diagnostics);
                result.Add(new ParsedMgcb(relative, file));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new Diagnostic("ANA013", $"Content.mgcb could not be read: {ex.Message}", relative));
            }
        }

        return result;
    }

    private const string _sharedItemsExtension = ".projitems";
    private const string _thisFileDirectory = "$(MSBuildThisFileDirectory)";
    private const int _maxSharedDepth = 5;

    // Follows Import of .projitems files (shared projects) and returns their items with paths made relative to the project folder.
    private List<ProjectItem> ReadSharedItems(List<ProjectItem> items, string projectDirectory, string root, List<Diagnostic> diagnostics, CancellationToken ct)
    {
        var result = new List<ProjectItem>();
        var visited = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var import in items.Where(IsSharedImport))
        {
            var native = ProjectXml.ToNative(import.Include.Replace(_thisFileDirectory, string.Empty, StringComparison.OrdinalIgnoreCase));
            CollectShared(Path.GetFullPath(Path.Combine(projectDirectory, native)), projectDirectory, root, 0, visited, result, diagnostics, ct);
        }

        return result;
    }

    private static bool IsSharedImport(ProjectItem item) =>
        item.ItemType == "Import" && item.Include.Trim().EndsWith(_sharedItemsExtension, StringComparison.OrdinalIgnoreCase);

    private void CollectShared(string full, string projectDirectory, string root, int depth, HashSet<string> visited, List<ProjectItem> result, List<Diagnostic> diagnostics, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (depth > _maxSharedDepth || !visited.Add(full))
        {
            return;
        }

        XDocument shared;
        try
        {
            shared = ProjectXml.Load(_fileSystem, full);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new Diagnostic("ANA012", $"Shared project items could not be read: {ex.Message}", ProjectXml.Rel(root, full)));
            return;
        }

        var sharedDirectory = Path.GetDirectoryName(full) ?? projectDirectory;
        foreach (var item in ProjectXml.ReadItems(shared))
        {
            if (IsSharedImport(item))
            {
                var native = ProjectXml.ToNative(item.Include.Replace(_thisFileDirectory, string.Empty, StringComparison.OrdinalIgnoreCase));
                CollectShared(Path.GetFullPath(Path.Combine(sharedDirectory, native)), projectDirectory, root, depth + 1, visited, result, diagnostics, ct);
            }
            else if (item.ItemType != "Import")
            {
                var include = item.Include.Replace(_thisFileDirectory, string.Empty, StringComparison.OrdinalIgnoreCase);
                var expanded = item.ItemType == "Compile"
                    ? ProjectXml.ExpandCompile(_fileSystem, sharedDirectory, include, ct)
                    : [include.Replace('\\', '/')];
                foreach (var path in expanded)
                {
                    var absolute = Path.GetFullPath(Path.Combine(sharedDirectory, ProjectXml.ToNative(path)));
                    result.Add(new ProjectItem(item.ItemType, ProjectXml.Rel(projectDirectory, absolute), item.Metadata));
                }
            }
        }
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
            [],
            ProjectFamily.Other);
        return new ProjectReadResult(
            info,
            new Dictionary<string, string>(),
            [],
            [new Diagnostic("ANA001", $"Project file could not be read: {reason}", relative)]);
    }
}
