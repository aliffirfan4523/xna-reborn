using System.Text.RegularExpressions;
using System.Xml.Linq;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Discovery;

/// <summary>Finds the .csproj files under a folder, solution, or project path and flags the XNA ones. Reads disk only through <see cref="IReadOnlyFileSystem"/>.</summary>
public sealed partial class ProjectDiscovery
{
    private static readonly string[] _skippedDirectories = ["bin", "obj", ".git", ".vs"];

    private readonly IReadOnlyFileSystem _fileSystem;

    /// <summary>Creates a discovery service.</summary>
    /// <param name="fileSystem">Read-only file system used for all disk access.</param>
    public ProjectDiscovery(IReadOnlyFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>Discovers projects at <paramref name="path"/>. Bad paths produce a result with a reason instead of an exception.</summary>
    /// <param name="path">A folder, a .sln file, or a .csproj file.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The discovery result.</returns>
    public Task<DiscoveryResult> DiscoverAsync(string path, CancellationToken cancellationToken)
    {
        return Task.Run(() => Discover(path, cancellationToken), cancellationToken);
    }

    [GeneratedRegex("""^\s*Project\("\{[^}]*\}"\)\s*=\s*"[^"]*"\s*,\s*"(?<path>[^"]*)"\s*,""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SolutionProjectLine();

    private static string Rel(string root, string full) =>
        Path.GetRelativePath(root, full).Replace('\\', '/');

    private static DiscoveryResult Empty(string root, string reason) =>
        new(root, [], reason, []);

    private DiscoveryResult Discover(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Empty(path, $"Path not found: {path}");
        }

        var diagnostics = new List<Diagnostic>();
        var candidates = new List<string>();
        var protobuild = new List<string>();
        string root;

        if (_fileSystem.DirectoryExists(full))
        {
            root = full;
            var csprojs = new List<string>();
            var slns = new List<string>();
            Walk(full, root, csprojs, slns, protobuild, diagnostics, ct);
            foreach (var sln in slns)
            {
                candidates.AddRange(ReadSolution(sln, root, diagnostics));
            }

            candidates.AddRange(csprojs);
        }
        else if (_fileSystem.FileExists(full))
        {
            var ext = Path.GetExtension(full);
            root = Path.GetDirectoryName(full) ?? full;
            if (ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(full);
            }
            else if (ext.Equals(".sln", StringComparison.OrdinalIgnoreCase))
            {
                candidates.AddRange(ReadSolution(full, root, diagnostics));
            }
            else
            {
                return Empty(root, $"Unsupported file type '{ext}'. Open a folder, a .sln, or a .csproj.");
            }
        }
        else
        {
            return Empty(full, $"Path not found: {path}");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var projects = new List<DiscoveredProject>();
        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            if (!seen.Add(candidate))
            {
                continue;
            }

            var relative = Rel(root, candidate);
            projects.Add(new DiscoveredProject(relative, candidate, Classify(candidate, relative, root, diagnostics)));
        }

        projects.Sort((a, b) =>
        {
            var c = string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.CompareOrdinal(a.RelativePath, b.RelativePath);
        });

        var unique = diagnostics.Distinct().ToList();
        return new DiscoveryResult(root, projects, ReasonFor(full, projects, diagnostics, protobuild), unique);
    }

    private static string? ReasonFor(string full, List<DiscoveredProject> projects, List<Diagnostic> diagnostics, List<string> protobuild)
    {
        if (projects.Any(p => p.Family is ProjectFamily.Xna or ProjectFamily.MonoGameLegacy))
        {
            return null;
        }

        if (projects.Any(p => p.Family == ProjectFamily.MonoGameCurrent))
        {
            return "All MonoGame projects are already current. Nothing to migrate.";
        }

        if (projects.Count > 0)
        {
            return $"Found {projects.Count} project(s), but none use XNA or MonoGame.";
        }

        if (protobuild.Count > 0)
        {
            var shown = string.Join(", ", protobuild.Take(3));
            return $"Found Protobuild files ({shown}) but no .csproj files. Generate the project files with the Protobuild version this project used, outside XNA Reborn, then open the generated folder.";
        }

        var isSolution = Path.GetExtension(full).Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || diagnostics.Any(d => d.Code == "ANA006");
        return isSolution
            ? "The solution lists no projects that exist on disk."
            : "No .csproj files found.";
    }

    private void Walk(string directory, string root, List<string> csprojs, List<string> slns, List<string> protobuild, List<Diagnostic> diagnostics, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<string> solutionFiles;
        IReadOnlyList<string> projectFiles;
        IReadOnlyList<string> subdirectories;
        try
        {
            solutionFiles = _fileSystem.EnumerateFiles(directory, "*.sln");
            projectFiles = _fileSystem.EnumerateFiles(directory, "*.csproj");
            subdirectories = _fileSystem.EnumerateDirectories(directory);
            AddProtobuildFiles(directory, root, protobuild);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable folder must not stop the search. Report it and move on.
            var relative = Rel(root, directory);
            diagnostics.Add(new Diagnostic("ANA007", $"Folder could not be read: {ex.Message}", relative == "." ? string.Empty : relative));
            return;
        }

        slns.AddRange(solutionFiles.Where(f => Path.GetExtension(f).Equals(".sln", StringComparison.OrdinalIgnoreCase)));
        csprojs.AddRange(projectFiles.Where(f => Path.GetExtension(f).Equals(".csproj", StringComparison.OrdinalIgnoreCase)));
        foreach (var sub in subdirectories)
        {
            var name = Path.GetFileName(sub.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (_skippedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            Walk(sub, root, csprojs, slns, protobuild, diagnostics, ct);
        }
    }

    private List<string> ReadSolution(string slnPath, string root, List<Diagnostic> diagnostics)
    {
        var found = new List<string>();
        var slnDir = Path.GetDirectoryName(slnPath) ?? root;
        string text;
        try
        {
            text = _fileSystem.ReadAllText(slnPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new Diagnostic("ANA001", $"Project file could not be read: {ex.Message}", Rel(root, slnPath)));
            return found;
        }

        foreach (var line in text.Split('\n'))
        {
            var match = SolutionProjectLine().Match(line.TrimEnd('\r'));
            if (!match.Success)
            {
                continue;
            }

            var entry = match.Groups["path"].Value;
            if (!entry.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var native = entry.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            var projectPath = Path.GetFullPath(Path.Combine(slnDir, native));
            if (_fileSystem.FileExists(projectPath))
            {
                found.Add(projectPath);
            }
            else
            {
                diagnostics.Add(new Diagnostic("ANA006", $"Solution lists a project that does not exist: {entry.Replace('\\', '/')}", entry.Replace('\\', '/')));
            }
        }

        return found;
    }

    // Protobuild keeps its project definitions in Build/Projects/*.definition and its launcher in Protobuild.exe. Only names are read.
    private void AddProtobuildFiles(string directory, string root, List<string> protobuild)
    {
        protobuild.AddRange(_fileSystem.EnumerateFiles(directory, "Protobuild.exe").Select(f => Rel(root, f)));
        var name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var parent = Path.GetFileName(Path.GetDirectoryName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? string.Empty);
        if (name.Equals("Projects", StringComparison.OrdinalIgnoreCase) && parent.Equals("Build", StringComparison.OrdinalIgnoreCase))
        {
            protobuild.AddRange(_fileSystem.EnumerateFiles(directory, "*.definition").Select(f => Rel(root, f)));
        }
    }

    private ProjectFamily Classify(string projectPath, string relative, string root, List<Diagnostic> diagnostics)
    {
        try
        {
            using var stream = _fileSystem.OpenRead(projectPath);
            var document = XDocument.Load(stream);

            // An unreadable packages.config is reported by ProjectReader; here it only means no extra signal.
            var packages = PackagesConfigReader.Read(_fileSystem, Path.GetDirectoryName(projectPath) ?? root, root).Packages;
            var family = MonoGameProjectDetector.Classify(document, packages);
            if (family == ProjectFamily.Xna && MonoGameProjectDetector.IsMonoGame(document, packages))
            {
                diagnostics.Add(new Diagnostic("ANA008", $"The project references both XNA and MonoGame ({MonoGameProjectDetector.DescribeMix(document, packages)}). It is treated as an XNA project.", relative));
            }

            return family;
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new Diagnostic("ANA001", $"Project file could not be read: {ex.Message}", relative));
            return ProjectFamily.Other;
        }
    }
}
