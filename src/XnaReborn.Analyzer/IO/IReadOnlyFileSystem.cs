namespace XnaReborn.Analyzer.IO;

/// <summary>
/// The only way the analyzer touches disk. It has no member that creates, changes, moves, or deletes anything.
/// </summary>
public interface IReadOnlyFileSystem
{
    /// <summary>Returns true when a file exists at <paramref name="path"/>.</summary>
    /// <param name="path">Absolute path.</param>
    bool FileExists(string path);

    /// <summary>Returns true when a directory exists at <paramref name="path"/>.</summary>
    /// <param name="path">Absolute path.</param>
    bool DirectoryExists(string path);

    /// <summary>Lists files directly inside a directory (not subdirectories), sorted by ordinal path order.</summary>
    /// <param name="directory">Absolute directory path.</param>
    /// <param name="searchPattern">File name pattern such as "*.csproj".</param>
    /// <returns>Absolute file paths.</returns>
    IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern);

    /// <summary>Lists the directories directly inside a directory, sorted by ordinal path order.</summary>
    /// <param name="directory">Absolute directory path.</param>
    /// <returns>Absolute directory paths.</returns>
    IReadOnlyList<string> EnumerateDirectories(string directory);

    /// <summary>Opens a file for reading only. Other processes may keep reading or writing it.</summary>
    /// <param name="path">Absolute file path.</param>
    Stream OpenRead(string path);

    /// <summary>Reads a whole text file. Detects UTF-8 and UTF-16 byte order marks and defaults to UTF-8.</summary>
    /// <param name="path">Absolute file path.</param>
    string ReadAllText(string path);
}
