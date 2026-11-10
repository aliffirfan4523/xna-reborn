namespace XnaReborn.App.Services;

/// <summary>File pickers and message dialogs, so view models never touch Avalonia windows.</summary>
public interface IDialogService
{
    /// <summary>Asks for a folder. Returns its path, or null when cancelled.</summary>
    /// <param name="title">Dialog title.</param>
    Task<string?> PickFolderAsync(string title);

    /// <summary>Asks for a .sln or .csproj file. Returns its path, or null when cancelled.</summary>
    Task<string?> PickProjectFileAsync();

    /// <summary>Asks where to save a file. Returns the path, or null when cancelled.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="suggestedName">File name offered first, without the extension.</param>
    /// <param name="extension">Extension without the dot, such as "md".</param>
    Task<string?> SaveFileAsync(string title, string suggestedName, string extension);

    /// <summary>Shows a modal question. Returns true for the confirm button.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">The question.</param>
    /// <param name="confirmText">Text of the confirm button.</param>
    /// <param name="cancelText">Text of the cancel button.</param>
    Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText);

    /// <summary>Shows a modal error with optional expandable details.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">What went wrong, in plain words.</param>
    /// <param name="details">Technical details, or null.</param>
    Task ShowErrorAsync(string title, string message, string? details);
}
