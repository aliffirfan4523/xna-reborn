using XnaReborn.App.Services;

namespace XnaReborn.App.Tests.Fakes;

/// <summary>Remembers what was asked to open. Opens nothing.</summary>
public sealed class FakeShellService : IShellService
{
    /// <summary>Folders opened, in order.</summary>
    public List<string> OpenedFolders { get; } = [];

    /// <summary>Files opened, in order.</summary>
    public List<string> OpenedFiles { get; } = [];

    /// <summary>Thrown by both methods when set.</summary>
    public Exception? Failure { get; set; }

    public Task OpenFolderAsync(string path)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        OpenedFolders.Add(path);
        return Task.CompletedTask;
    }

    public Task OpenFileAsync(string path)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        OpenedFiles.Add(path);
        return Task.CompletedTask;
    }
}
