using System.Globalization;

namespace XnaReborn.Migrator.Output;

/// <summary>One migration's output. All files are written to <see cref="StagingFolder"/>, and <see cref="Commit"/> moves them into place.</summary>
public sealed class OutputSession : IDisposable
{
    private bool _disposed;

    internal OutputSession(string stagingFolder, string outputFolder)
    {
        StagingFolder = stagingFolder;
        OutputFolder = outputFolder;
    }

    /// <summary>The folder where all output is written.</summary>
    public string StagingFolder { get; }

    /// <summary>The final output path.</summary>
    public string OutputFolder { get; }

    /// <summary>The folder an existing output was moved to by <see cref="Commit"/>, or null when nothing was moved aside.</summary>
    public string? BackupFolder { get; private set; }

    /// <summary>True once <see cref="Commit"/> has succeeded.</summary>
    public bool Committed { get; private set; }

    /// <summary>Moves the staging folder to the output path. An existing non-empty output is moved aside to a backup folder, never deleted.</summary>
    /// <exception cref="InvalidOperationException">The session was already committed or disposed.</exception>
    /// <exception cref="IOException">A move failed. The earlier output is restored first.</exception>
    public void Commit()
    {
        if (_disposed || Committed)
        {
            throw new InvalidOperationException(_disposed
                ? "This output session has been disposed and cannot be committed."
                : "This output session has already been committed.");
        }

        if (File.Exists(OutputFolder))
        {
            throw new IOException($"The output path '{OutputFolder}' is now a file. The migrated copy stays in '{StagingFolder}'.");
        }

        string? backup = null;
        try
        {
            if (Directory.Exists(OutputFolder))
            {
                if (OutputGuard.IsEmptyDirectory(OutputFolder))
                {
                    Directory.Delete(OutputFolder, recursive: false);
                }
                else
                {
                    var stamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                    backup = OutputGuard.UniquePath(
                        Path.GetDirectoryName(OutputFolder)!,
                        Path.GetFileName(OutputFolder) + ".backup-" + stamp);
                    Directory.Move(OutputFolder, backup);
                }
            }

            Directory.Move(StagingFolder, OutputFolder);
            File.Delete(Path.Combine(OutputFolder, OutputGuard.MarkerName));
        }
        catch
        {
            Rollback(backup);
            throw;
        }

        BackupFolder = backup;
        Committed = true;
    }

    /// <summary>Ends the session. Without <see cref="Commit"/>, the staging folder stays in place with its INCOMPLETE.txt marker. Nothing is deleted.</summary>
    public void Dispose()
    {
        _disposed = true;
    }

    private void Rollback(string? backup)
    {
        try
        {
            if (!Directory.Exists(StagingFolder) && Directory.Exists(OutputFolder))
            {
                Directory.Move(OutputFolder, StagingFolder);
            }

            if (backup is not null && Directory.Exists(backup) && !Directory.Exists(OutputFolder))
            {
                Directory.Move(backup, OutputFolder);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort. The original exception is rethrown by the caller, and nothing was deleted.
        }
    }
}
