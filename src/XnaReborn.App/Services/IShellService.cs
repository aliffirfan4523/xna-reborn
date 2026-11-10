namespace XnaReborn.App.Services;

/// <summary>Opens a folder or a file with the operating system's default program.</summary>
public interface IShellService
{
    /// <summary>Opens a folder in the file manager.</summary>
    /// <param name="path">Absolute path of an existing local folder.</param>
    Task OpenFolderAsync(string path);

    /// <summary>Opens a file with its default program.</summary>
    /// <param name="path">Absolute path of an existing local file.</param>
    Task OpenFileAsync(string path);
}
