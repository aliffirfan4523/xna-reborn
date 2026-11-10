using System.Xml.Linq;

namespace XnaReborn.Runner.Run;

/// <summary>The game that a build of a migrated copy produced.</summary>
/// <param name="ProjectPath">Full path of the game's project file.</param>
/// <param name="AssemblyName">Assembly name from the project, or the project file name.</param>
/// <param name="TargetFramework">Target framework folder name, such as net8.0.</param>
/// <param name="ExecutablePath">Full path of the built .exe (on Windows) or .dll.</param>
public sealed record GameTarget(string ProjectPath, string AssemblyName, string TargetFramework, string ExecutablePath)
{
    /// <summary>Command-line arguments for the game. Only tests set it.</summary>
    internal IReadOnlyList<string>? LaunchArguments { get; init; }
}

/// <summary>Either the game that was found or a plain message that says why none was.</summary>
/// <param name="Target">The game, or null.</param>
/// <param name="Problem">What is wrong, or null when a game was found.</param>
public sealed record GameLocatorResult(GameTarget? Target, string? Problem);

/// <summary>The game project of a migrated copy, found without building.</summary>
/// <param name="ProjectPath">Full path of the game's project file.</param>
/// <param name="AssemblyName">Assembly name from the project, or the project file name.</param>
/// <param name="TargetFramework">Target framework folder name, such as net8.0.</param>
/// <param name="Problem">What is wrong, or null when a game project was found.</param>
public sealed record GameProjectResult(string? ProjectPath, string? AssemblyName, string? TargetFramework, string? Problem);

/// <summary>Finds the executable project of a migrated copy and the file it builds. Read-only.</summary>
public static class GameLocator
{
    /// <summary>
    /// Looks at the .csproj files under the folder (not in bin or obj) and picks the game. Of the projects with
    /// OutputType WinExe or Exe, exactly one is the game. With several, the one with a MonoGameContentReference
    /// wins, then the one that no other project references. The built file is
    /// <c>bin/&lt;configuration&gt;/&lt;TargetFramework&gt;/&lt;AssemblyName&gt;.exe</c> on Windows when it exists, else the .dll.
    /// </summary>
    /// <param name="migratedFolder">The migrated copy.</param>
    /// <param name="configuration">The build configuration. Defaults to Debug, like <c>dotnet build</c>.</param>
    /// <returns>The game, or a problem text. Never throws for a bad folder.</returns>
    public static GameLocatorResult Find(string migratedFolder, string configuration = "Debug")
    {
        var found = FindProject(migratedFolder);
        if (found.ProjectPath is null)
        {
            return Fail(found.Problem!);
        }

        var game = new ProjectInfo(found.ProjectPath, found.AssemblyName!, found.TargetFramework!, true, false, []);
        var outputDir = Path.Combine(Path.GetDirectoryName(game.Path)!, "bin", configuration, game.TargetFramework);
        var exe = Path.Combine(outputDir, game.AssemblyName + ".exe");
        var dll = Path.Combine(outputDir, game.AssemblyName + ".dll");
        var built = OperatingSystem.IsWindows() && File.Exists(exe) ? exe : File.Exists(dll) ? dll : null;
        if (built is null)
        {
            return Fail($"The game has not been built: {dll} does not exist. Build the project first.");
        }

        return new GameLocatorResult(new GameTarget(game.Path, game.AssemblyName, game.TargetFramework, built), null);
    }

    /// <summary>Picks the game project by the same rules as <see cref="Find"/>, without looking for a built file.</summary>
    /// <param name="migratedFolder">The migrated copy.</param>
    /// <returns>The project, or a problem text. Never throws for a bad folder.</returns>
    public static GameProjectResult FindProject(string migratedFolder)
    {
        if (string.IsNullOrWhiteSpace(migratedFolder) || !Directory.Exists(migratedFolder))
        {
            return new GameProjectResult(null, null, null, "The migrated folder does not exist.");
        }

        var files = new List<string>();
        Collect(Path.GetFullPath(migratedFolder), files);
        files.Sort(StringComparer.Ordinal);

        var projects = new List<ProjectInfo>();
        var unreadable = new List<string>();
        foreach (var file in files)
        {
            var info = Read(file);
            if (info is null)
            {
                unreadable.Add(file);
            }
            else
            {
                projects.Add(info);
            }
        }

        var exes = projects.Where(p => p.IsExecutable).ToList();
        if (exes.Count == 0)
        {
            var extra = unreadable.Count == 0 ? string.Empty : " These project files could not be read: " + string.Join(", ", unreadable) + ".";
            return Problem("No game project was found: none of the projects has OutputType WinExe or Exe." + extra);
        }

        var game = exes.Count == 1 ? exes[0] : Choose(exes, projects);
        if (game is null)
        {
            return Problem("Several projects produce a program and none is clearly the game: " + string.Join(", ", exes.Select(e => e.Path)) + ". Build and run the one you want by hand.");
        }

        return new GameProjectResult(game.Path, game.AssemblyName, game.TargetFramework, null);
    }

    private static ProjectInfo? Choose(List<ProjectInfo> exes, List<ProjectInfo> all)
    {
        var withContent = exes.Where(e => e.HasContentReference).ToList();
        if (withContent.Count == 1)
        {
            return withContent[0];
        }

        var pool = withContent.Count > 1 ? withContent : exes;
        var unreferenced = pool
            .Where(p => !all.Any(other => !ReferenceEquals(other, p) && other.References.Contains(p.Path, PathComparer)))
            .ToList();
        return unreferenced.Count == 1 ? unreferenced[0] : null;
    }

    private static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static void Collect(string dir, List<string> found)
    {
        found.AddRange(Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly));
        foreach (var sub in Directory.GetDirectories(dir))
        {
            var name = Path.GetFileName(sub);
            if (name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("obj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Collect(sub, found);
        }
    }

    private static ProjectInfo? Read(string path)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(path);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var dir = Path.GetDirectoryName(path)!;
        var fileName = Path.GetFileNameWithoutExtension(path);

        string? Value(string name) => doc.Descendants()
            .Where(e => e.Name.LocalName == name && !e.HasElements)
            .Select(e => e.Value.Trim())
            .LastOrDefault(v => v.Length > 0);

        var outputType = Value("OutputType");
        var assembly = Value("AssemblyName")?.Replace("$(MSBuildProjectName)", fileName, StringComparison.Ordinal);
        if (assembly is null || assembly.Contains("$(", StringComparison.Ordinal))
        {
            assembly = fileName;
        }

        var framework = Value("TargetFramework") ?? Value("TargetFrameworks")?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "net8.0";
        var references = doc.Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => Path.GetFullPath(Path.Combine(dir, i!.Replace('\\', Path.DirectorySeparatorChar))))
            .ToList();
        var hasContent = doc.Descendants().Any(e => e.Name.LocalName == "MonoGameContentReference");
        var isExe = string.Equals(outputType, "WinExe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase);
        return new ProjectInfo(path, assembly, framework, isExe, hasContent, references);
    }

    private static GameLocatorResult Fail(string problem) => new(null, problem);

    private static GameProjectResult Problem(string problem) => new(null, null, null, problem);

    private sealed record ProjectInfo(
        string Path,
        string AssemblyName,
        string TargetFramework,
        bool IsExecutable,
        bool HasContentReference,
        IReadOnlyList<string> References);
}
