using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Rules.Matchers;

/// <summary>Matches <see cref="FilePatternMatch"/> by walking the project folder through <see cref="IReadOnlyFileSystem"/>.</summary>
internal static class FilePatternMatcher
{
    private static readonly string[] _skippedFolders = ["bin", "obj", ".git", ".vs"];

    /// <summary>Returns matching file paths relative to the project directory, using '/' separators.</summary>
    /// <param name="match">The rule match.</param>
    /// <param name="fileSystem">Read-only file system.</param>
    /// <param name="projectDirectory">Absolute project directory.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    public static IReadOnlyList<string> Match(FilePatternMatch match, IReadOnlyFileSystem fileSystem, string projectDirectory, CancellationToken cancellationToken)
    {
        var glob = match.Glob.Replace('\\', '/').TrimStart('/');
        if (glob.Length == 0)
        {
            return [];
        }

        if (!Wildcard.HasWildcard(glob))
        {
            return fileSystem.FileExists(Path.Combine(projectDirectory, glob.Replace('/', Path.DirectorySeparatorChar))) ? [glob] : [];
        }

        var segments = glob.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var results = new SortedSet<string>(StringComparer.Ordinal);
        Walk(fileSystem, projectDirectory, string.Empty, segments, 0, results, cancellationToken);
        return results.ToList();
    }

    private static void Walk(IReadOnlyFileSystem fs, string directory, string relative, string[] segments, int index, SortedSet<string> results, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (index >= segments.Length)
        {
            return;
        }

        var segment = segments[index];
        var last = index == segments.Length - 1;

        if (segment == "**")
        {
            if (last)
            {
                AddFiles(fs, directory, relative, "*", results);
            }
            else
            {
                Walk(fs, directory, relative, segments, index + 1, results, token);
            }

            foreach (var sub in SafeDirectories(fs, directory))
            {
                Walk(fs, sub.Path, relative + sub.Name + "/", segments, index, results, token);
            }

            return;
        }

        if (last)
        {
            AddFiles(fs, directory, relative, segment, results);
            return;
        }

        foreach (var sub in SafeDirectories(fs, directory))
        {
            if (Wildcard.IsMatch(segment, sub.Name))
            {
                Walk(fs, sub.Path, relative + sub.Name + "/", segments, index + 1, results, token);
            }
        }
    }

    private static void AddFiles(IReadOnlyFileSystem fs, string directory, string relative, string pattern, SortedSet<string> results)
    {
        IReadOnlyList<string> files;
        try
        {
            files = fs.EnumerateFiles(directory, "*");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            if (Wildcard.IsMatch(pattern, name))
            {
                results.Add(relative + name);
            }
        }
    }

    private static List<(string Name, string Path)> SafeDirectories(IReadOnlyFileSystem fs, string directory)
    {
        try
        {
            return fs.EnumerateDirectories(directory)
                .Select(p => (Name: System.IO.Path.GetFileName(p), Path: p))
                .Where(d => !_skippedFolders.Contains(d.Name, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
