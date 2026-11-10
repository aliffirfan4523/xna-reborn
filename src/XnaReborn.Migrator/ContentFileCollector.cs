using XnaReborn.Analyzer.IO;

namespace XnaReborn.Migrator;

/// <summary>Lists the files below a content project folder, for the placeholder copy until content conversion is wired in.</summary>
internal static class ContentFileCollector
{
    private static readonly string[] _skippedDirectories = ["bin", "obj", ".git", ".vs"];

    /// <summary>Returns the full path of every file below <paramref name="directory"/>, sorted ordinally.</summary>
    /// <param name="fileSystem">Read-only file system.</param>
    /// <param name="directory">Full path of the content project folder.</param>
    /// <param name="cancellationToken">Cancellation token, checked per folder.</param>
    internal static IReadOnlyList<string> Collect(IReadOnlyFileSystem fileSystem, string directory, CancellationToken cancellationToken)
    {
        var files = new List<string>();
        Walk(fileSystem, directory, files, cancellationToken);
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static void Walk(IReadOnlyFileSystem fileSystem, string directory, List<string> files, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        files.AddRange(fileSystem.EnumerateFiles(directory, "*"));
        foreach (var sub in fileSystem.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(sub.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (_skippedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            Walk(fileSystem, sub, files, cancellationToken);
        }
    }
}
