using System.Security.Cryptography;

namespace XnaReborn.Tests.Safety;

/// <summary>A snapshot of every file and folder under a root: size, timestamps, attributes, and SHA-256.</summary>
public static class FolderState
{
    /// <summary>Captures the state of <paramref name="root"/>. The lines are sorted so two captures compare with equality.</summary>
    /// <param name="root">Absolute folder path.</param>
    /// <returns>One line per folder and per file.</returns>
    public static IReadOnlyList<string> Capture(string root)
    {
        var lines = new List<string>();
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            lines.Add("D|" + Relative(root, directory));
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            lines.Add($"F|{Relative(root, file)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{(int)info.Attributes}|{hash}");
        }

        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
