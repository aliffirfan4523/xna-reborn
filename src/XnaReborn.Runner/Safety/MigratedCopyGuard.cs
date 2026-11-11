using System.Text.Json;

namespace XnaReborn.Runner.Safety;

/// <summary>The answer to "may the Runner build and run this folder?".</summary>
/// <param name="Allowed">True only for a completed migrated copy.</param>
/// <param name="Message">A plain message telling the user what was found or what to do.</param>
/// <param name="ProjectOrSolutionPath">The solution or project to build, or null when not allowed.</param>
public sealed record GuardVerdict(bool Allowed, string Message, string? ProjectOrSolutionPath);

/// <summary>Refuses to build or run anything that is not a completed migrated copy. Read-only.</summary>
public static class MigratedCopyGuard
{
    private const string _manifestName = "xnareborn-manifest.json";
    private const string _incompleteName = "INCOMPLETE.txt";

    /// <summary>Checks a folder.</summary>
    /// <param name="folder">The folder the user wants to build.</param>
    /// <returns>The verdict. Never throws for a bad folder.</returns>
    public static GuardVerdict Check(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return Refuse("The folder does not exist. Choose the migrated copy that XNA Reborn created.");
        }

        var root = Path.GetFullPath(folder);
        var manifestPath = Path.Combine(root, _manifestName);
        if (!File.Exists(manifestPath))
        {
            return Refuse($"This folder has no {_manifestName}, so it is not a migrated copy. XNA Reborn only builds copies it created. Migrate the project first and choose the output folder.");
        }

        if (File.Exists(Path.Combine(root, _incompleteName)))
        {
            return Refuse($"This migrated copy is marked {_incompleteName}: the migration did not finish. Run the migration again into a new folder.");
        }

        var problem = ReadManifest(manifestPath);
        if (problem is not null)
        {
            return Refuse(problem);
        }

        return FindProject(root);
    }

    private static string? ReadManifest(string manifestPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var obj = doc.RootElement;
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return $"{_manifestName} is not valid. Run the migration again into a new folder.";
            }

            if (!obj.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var v) || v != 1)
            {
                return $"{_manifestName} has an unsupported version. Run the migration again with this version of XNA Reborn.";
            }

            if (!obj.TryGetProperty("completed", out var completed) || completed.ValueKind != JsonValueKind.True)
            {
                return "The migration of this folder did not finish. Run the migration again into a new folder.";
            }

            return null;
        }
        catch (JsonException)
        {
            return $"{_manifestName} cannot be read (invalid JSON). Run the migration again into a new folder.";
        }
        catch (IOException)
        {
            return $"{_manifestName} cannot be read. Check that the folder is accessible.";
        }
        catch (UnauthorizedAccessException)
        {
            return $"{_manifestName} cannot be read. Check that the folder is accessible.";
        }
    }

    private static GuardVerdict FindProject(string root)
    {
        var solutions = Directory.GetFiles(root, "*.sln", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal).ToList();
        if (solutions.Count == 1)
        {
            return new GuardVerdict(true, "Migrated copy found.", solutions[0]);
        }

        if (solutions.Count > 1)
        {
            return Refuse("The folder has more than one solution file. Keep one .sln at the root, or build the project you want by hand.");
        }

        var projects = new List<string>();
        CollectProjects(root, projects);
        projects.Sort(StringComparer.Ordinal);

        // The shim library sits beside the game and is built through the game's ProjectReference, so it is never the project to pick.
        if (projects.Count > 1)
        {
            var games = projects.Where(p => !Path.GetFileName(p).Equals("XnaReborn.Compat.csproj", StringComparison.OrdinalIgnoreCase)).ToList();
            if (games.Count > 0)
            {
                projects = games;
            }
        }

        return projects.Count switch
        {
            1 => new GuardVerdict(true, "Migrated copy found.", projects[0]),
            0 => Refuse("The migrated copy has no .sln or .csproj file to build."),
            _ => Refuse("The migrated copy has several .csproj files and no .sln. Choose the project to build by hand."),
        };
    }

    private static void CollectProjects(string dir, List<string> found)
    {
        found.AddRange(Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly));
        found.AddRange(Directory.GetFiles(dir, "*.vbproj", SearchOption.TopDirectoryOnly));
        foreach (var sub in Directory.GetDirectories(dir))
        {
            var name = Path.GetFileName(sub);
            if (name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("obj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CollectProjects(sub, found);
        }
    }

    private static GuardVerdict Refuse(string message) => new(false, message, null);
}
