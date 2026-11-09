using System.Globalization;

namespace XnaReborn.Migrator.Output;

/// <summary>Decides whether an output folder is safe to write to and opens a staging session for it. The source folder is only ever read.</summary>
public sealed class OutputGuard
{
    internal const string StagingSuffix = ".xnareborn-incomplete";
    internal const string MarkerName = "INCOMPLETE.txt";

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Checks the paths without creating or deleting anything.</summary>
    /// <param name="sourceFolder">The user's original project folder.</param>
    /// <param name="outputFolder">Where the migrated copy should go.</param>
    /// <param name="confirmedNonEmpty">True when the user agreed to replace an existing non-empty output folder.</param>
    /// <returns>The verdict.</returns>
    public GuardResult Check(string sourceFolder, string outputFolder, bool confirmedNonEmpty)
    {
        if (!TryResolve(sourceFolder, out var source))
        {
            return Refuse(GuardProblem.InvalidPath, "The source folder path is empty or not a valid path. Choose the folder that holds the project.");
        }

        if (!TryResolve(outputFolder, out var output))
        {
            return Refuse(GuardProblem.InvalidPath, "The output folder path is empty or not a valid path. Choose a new folder for the migrated copy.");
        }

        if (string.Equals(output, Path.GetPathRoot(output), PathComparison))
        {
            return Refuse(GuardProblem.InvalidPath, "The output folder cannot be a drive root. Choose a new folder for the migrated copy.");
        }

        if (!Directory.Exists(source))
        {
            return Refuse(GuardProblem.SourceMissing, $"The source folder '{source}' does not exist. Check the path and try again.");
        }

        if (string.Equals(source, output, PathComparison))
        {
            return Refuse(GuardProblem.OutputIsSource, "The output folder is the source folder. Choose a different folder so the original project stays untouched.");
        }

        if (IsInside(output, source))
        {
            return Refuse(GuardProblem.OutputInsideSource, $"The output folder '{output}' is inside the source folder. Choose a folder outside the original project.");
        }

        if (IsInside(source, output))
        {
            return Refuse(GuardProblem.SourceInsideOutput, $"The source folder '{source}' is inside the output folder. Choose an output folder that does not contain the original project.");
        }

        if (File.Exists(output))
        {
            return Refuse(GuardProblem.OutputIsAFile, $"The output path '{output}' is a file. Choose a folder path instead.");
        }

        if (Directory.Exists(output) && !confirmedNonEmpty && !IsEmptyDirectory(output))
        {
            return Refuse(GuardProblem.OutputNotEmpty, $"The output folder '{output}' already has content. Choose an empty or new folder, or confirm that it may be replaced (the old content is kept in a backup folder).");
        }

        return new GuardResult(true, GuardProblem.None, "The output location is safe to use.");
    }

    /// <summary>Checks the paths and creates a staging folder next to the output folder. Nothing is created inside the source folder.</summary>
    /// <param name="sourceFolder">The user's original project folder.</param>
    /// <param name="outputFolder">Where the migrated copy should go.</param>
    /// <param name="confirmedNonEmpty">True when the user agreed to replace an existing non-empty output folder.</param>
    /// <returns>A session whose <see cref="OutputSession.StagingFolder"/> receives all output.</returns>
    /// <exception cref="OutputGuardException">The check refused the paths.</exception>
    public OutputSession Begin(string sourceFolder, string outputFolder, bool confirmedNonEmpty)
    {
        var result = Check(sourceFolder, outputFolder, confirmedNonEmpty);
        if (!result.Allowed)
        {
            throw new OutputGuardException(result);
        }

        TryResolve(outputFolder, out var output);
        var parent = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(parent);

        var staging = UniquePath(parent, Path.GetFileName(output) + StagingSuffix);
        Directory.CreateDirectory(staging);
        File.WriteAllText(
            Path.Combine(staging, MarkerName),
            "This migration did not finish. The folder is incomplete and can be deleted." + Environment.NewLine);

        return new OutputSession(staging, output);
    }

    /// <summary>Returns <paramref name="folder"/>/<paramref name="name"/>, or the same name with -2, -3, ... when something already uses it.</summary>
    internal static string UniquePath(string folder, string name)
    {
        var candidate = Path.Combine(folder, name);
        for (var n = 2; Directory.Exists(candidate) || File.Exists(candidate); n++)
        {
            candidate = Path.Combine(folder, name + "-" + n.ToString(CultureInfo.InvariantCulture));
        }

        return candidate;
    }

    internal static bool IsEmptyDirectory(string folder) =>
        !Directory.EnumerateFileSystemEntries(folder).Any();

    private static GuardResult Refuse(GuardProblem problem, string message) => new(false, problem, message);

    private static bool IsInside(string child, string parent)
    {
        var prefix = Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar;
        return child.Length > prefix.Length && child.StartsWith(prefix, PathComparison);
    }

    private static bool TryResolve(string? path, out string full)
    {
        full = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return false;
        }

        try
        {
            full = ResolveLinks(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)));
            return true;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Follows directory links on the existing part of the path so a link cannot hide that two paths are the same place.</summary>
    private static string ResolveLinks(string full)
    {
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root))
        {
            return full;
        }

        var current = root;
        var segments = full[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var next = Path.Combine(current, segment);
            try
            {
                var info = new DirectoryInfo(next);
                current = info.Exists && info.LinkTarget is not null && info.ResolveLinkTarget(true) is { } target
                    ? Path.TrimEndingDirectorySeparator(target.FullName)
                    : next;
            }
            catch (IOException)
            {
                current = next;
            }
        }

        return current;
    }
}
