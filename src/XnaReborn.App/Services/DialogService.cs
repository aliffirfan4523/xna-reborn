using Avalonia.Controls;
using Avalonia.Platform.Storage;
using XnaReborn.App.Views;

namespace XnaReborn.App.Services;

/// <summary>Real <see cref="IDialogService"/>: file pickers from Avalonia's storage provider and a small modal message window.</summary>
public sealed class DialogService : IDialogService
{
    private readonly Func<TopLevel?> _topLevel;

    /// <summary>Creates the service.</summary>
    /// <param name="topLevel">Returns the window that owns the dialogs, or null before it exists.</param>
    public DialogService(Func<TopLevel?> topLevel)
    {
        _topLevel = topLevel;
    }

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await Owner().StorageProvider
            .OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PickProjectFileAsync()
    {
        var files = await Owner().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open an XNA project",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Solution or project") { Patterns = ["*.sln", "*.csproj"] }],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> SaveFileAsync(string title, string suggestedName, string extension)
    {
        var file = await Owner().StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(extension.ToUpperInvariant() + " file") { Patterns = ["*." + extension] }],
        });
        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText) =>
        new MessageWindow(title, message, null, confirmText, cancelText).ShowDialog<bool>(OwnerWindow());

    /// <inheritdoc />
    public Task ShowErrorAsync(string title, string message, string? details) =>
        new MessageWindow(title, message, details, "Close", null).ShowDialog<bool>(OwnerWindow());

    private TopLevel Owner() =>
        _topLevel() ?? throw new InvalidOperationException("There is no window to show the dialog on.");

    private Window OwnerWindow() =>
        Owner() as Window ?? throw new InvalidOperationException("The owner is not a window.");
}
