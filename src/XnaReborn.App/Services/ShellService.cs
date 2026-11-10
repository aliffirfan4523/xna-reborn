using System.Diagnostics;

namespace XnaReborn.App.Services;

/// <summary>Real <see cref="IShellService"/>: asks the operating system to open the path.</summary>
public sealed class ShellService : IShellService
{
    /// <inheritdoc />
    public Task OpenFolderAsync(string path)
    {
        Open(path, Directory.Exists, "folder");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OpenFileAsync(string path)
    {
        Open(path, File.Exists, "file");
        return Task.CompletedTask;
    }

    private static void Open(string path, Func<string, bool> exists, string what)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new ArgumentException($"The {what} path must be an absolute local path: {path}", nameof(path));
        }

        if (!exists(path))
        {
            throw new FileNotFoundException($"The {what} does not exist: {path}", path);
        }

        using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
