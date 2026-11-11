using XnaReborn.App.Services;

namespace XnaReborn.App.Tests.Fakes;

/// <summary>Answers dialogs from a script and records what was asked. Nothing is shown.</summary>
public sealed class FakeDialogService : IDialogService
{
    /// <summary>The answer of <see cref="PickFolderAsync"/>. Null means the user cancelled.</summary>
    public string? FolderToPick { get; set; }

    /// <summary>The answer of <see cref="PickProjectFileAsync"/>. Null means the user cancelled.</summary>
    public string? ProjectFileToPick { get; set; }

    /// <summary>The answer of <see cref="SaveFileAsync"/>. Null means the user cancelled.</summary>
    public string? SavePathToReturn { get; set; }

    /// <summary>Answers of <see cref="ConfirmAsync"/>, used first. Then <see cref="DefaultConfirmAnswer"/> applies.</summary>
    public Queue<bool> ConfirmAnswers { get; } = new();

    /// <summary>The answer of <see cref="ConfirmAsync"/> when <see cref="ConfirmAnswers"/> is empty.</summary>
    public bool DefaultConfirmAnswer { get; set; }

    /// <summary>The titles of the folder pickers that were shown.</summary>
    public List<string> FolderPickerTitles { get; } = [];

    /// <summary>Runs when a question is asked, before it is answered. Lets a test look at the page while the dialog is open.</summary>
    public Action? OnConfirm { get; set; }

    /// <summary>The questions that were asked.</summary>
    public List<(string Title, string Message, string ConfirmText, string CancelText)> Confirmations { get; } = [];

    /// <summary>The errors that were shown.</summary>
    public List<(string Title, string Message, string? Details)> Errors { get; } = [];

    public Task<string?> PickFolderAsync(string title)
    {
        FolderPickerTitles.Add(title);
        return Task.FromResult(FolderToPick);
    }

    public Task<string?> PickProjectFileAsync() => Task.FromResult(ProjectFileToPick);

    public Task<string?> SaveFileAsync(string title, string suggestedName, string extension) => Task.FromResult(SavePathToReturn);

    public Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText)
    {
        OnConfirm?.Invoke();
        Confirmations.Add((title, message, confirmText, cancelText));
        return Task.FromResult(ConfirmAnswers.Count > 0 ? ConfirmAnswers.Dequeue() : DefaultConfirmAnswer);
    }

    public Task ShowErrorAsync(string title, string message, string? details)
    {
        Errors.Add((title, message, details));
        return Task.CompletedTask;
    }
}
