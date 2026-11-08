using System.Text;

namespace XnaReborn.Analyzer.IO;

/// <summary>Real disk access with read-only file handles.</summary>
public sealed class ReadOnlyFileSystem : IReadOnlyFileSystem
{
    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc />
    public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern) =>
        Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <inheritdoc />
    public IReadOnlyList<string> EnumerateDirectories(string directory) =>
        Directory.EnumerateDirectories(directory)
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <inheritdoc />
    public Stream OpenRead(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);

    /// <inheritdoc />
    public string ReadAllText(string path)
    {
        using var stream = OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
