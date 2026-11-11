using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models;

namespace XnaReborn.Flash.Analyzer.Detection;

/// <summary>What <see cref="FlashProjectDetector"/> found in a folder.</summary>
/// <param name="Kind">Flash, Xna (a .NET project or solution is present), or Unknown.</param>
/// <param name="IsConflict">True when the folder holds both Flash signals and a .NET project or solution. <see cref="Kind"/> is Unknown then.</param>
/// <param name="ProjectFile">Relative path of the .as3proj or .actionScriptProperties file, or null.</param>
/// <param name="SourceFiles">Relative paths of every .as file, sorted.</param>
/// <param name="HasSwf">True when a .swf file exists. The file is never opened.</param>
/// <param name="HasFla">True when a .fla or .xfl file exists.</param>
/// <param name="SwcFiles">Relative paths of .swc libraries, sorted.</param>
public sealed record FlashDetection(
    ProjectKind Kind,
    bool IsConflict,
    string? ProjectFile,
    IReadOnlyList<string> SourceFiles,
    bool HasSwf,
    bool HasFla,
    IReadOnlyList<string> SwcFiles);

/// <summary>Decides from file names alone whether a folder is a Flash project. It reads no file contents.</summary>
public static class FlashProjectDetector
{
    private static readonly HashSet<string> _skipFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "bin-debug", "bin-release", "obj", ".git", ".svn", ".vs", "node_modules",
    };

    /// <summary>Scans <paramref name="root"/> and its folders.</summary>
    /// <param name="fileSystem">Read-only file access.</param>
    /// <param name="root">Absolute project folder.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    public static FlashDetection Detect(IReadOnlyFileSystem fileSystem, string root, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrEmpty(root);

        var sources = new List<string>();
        var swc = new List<string>();
        string? projectFile = null;
        var hasSwf = false;
        var hasFla = false;
        var hasDotNet = false;

        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dir = pending.Pop();
            foreach (var file in fileSystem.EnumerateFiles(dir, "*"))
            {
                var name = Path.GetFileName(file);
                var rel = Relative(root, file);
                switch (Path.GetExtension(name).ToLowerInvariant())
                {
                    case ".as":
                        sources.Add(rel);
                        break;
                    case ".as3proj":
                        projectFile = Pick(projectFile, rel);
                        break;
                    case ".swf":
                        hasSwf = true;
                        break;
                    case ".fla":
                    case ".xfl":
                        hasFla = true;
                        break;
                    case ".swc":
                        swc.Add(rel);
                        break;
                    case ".csproj":
                    case ".sln":
                        hasDotNet = true;
                        break;
                    default:
                        if (string.Equals(name, ".actionScriptProperties", StringComparison.OrdinalIgnoreCase))
                        {
                            projectFile ??= rel;
                        }

                        break;
                }
            }

            foreach (var sub in fileSystem.EnumerateDirectories(dir))
            {
                if (!_skipFolders.Contains(Path.GetFileName(sub)))
                {
                    pending.Push(sub);
                }
            }
        }

        sources.Sort(StringComparer.Ordinal);
        swc.Sort(StringComparer.Ordinal);
        var flash = sources.Count > 0 || projectFile is not null || hasSwf || hasFla;
        var kind = (flash, hasDotNet) switch
        {
            (true, true) => ProjectKind.Unknown,
            (true, false) => ProjectKind.Flash,
            (false, true) => ProjectKind.Xna,
            _ => ProjectKind.Unknown,
        };
        return new FlashDetection(kind, flash && hasDotNet, projectFile, sources, hasSwf, hasFla, swc);
    }

    private static string Pick(string? current, string candidate) =>
        current is null || string.CompareOrdinal(candidate, current) < 0 ? candidate : current;

    internal static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
