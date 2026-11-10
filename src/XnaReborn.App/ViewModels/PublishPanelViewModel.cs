using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XnaReborn.App.Services;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Publish;

namespace XnaReborn.App.ViewModels;

/// <summary>
/// The "Publish" section of the Build and run page: pick a platform, publish the migrated copy as one self-contained program,
/// watch the progress, cancel, and open the result folder. It holds no publish logic; <see cref="IRunService"/> does the work.
/// The user is asked before a result folder that already has content is used.
/// </summary>
public sealed partial class PublishPanelViewModel : ViewModelBase
{
    private static readonly TimeSpan _shutdownWait = TimeSpan.FromSeconds(5);

    private readonly WorkflowSession _session;
    private readonly IDialogService _dialogs;
    private readonly IRunService _runner;
    private readonly IShellService _shell;
    private readonly Func<string?> _migratedFolder;
    private readonly Func<bool> _otherWorkRunning;
    private readonly Action _stateChanged;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource? _idle;

    /// <summary>The platform chosen in the picker.</summary>
    [ObservableProperty]
    private PublishTarget _selectedTarget = PublishTarget.WindowsX64;

    /// <summary>True while a publish runs.</summary>
    [ObservableProperty]
    private bool _isPublishing;

    /// <summary>The line under the buttons: the latest output line while publishing, then the outcome.</summary>
    [ObservableProperty]
    private string _statusText = "Not published yet.";

    /// <summary>True when <see cref="StatusText"/> describes a failure.</summary>
    [ObservableProperty]
    private bool _isStatusError;

    /// <summary>The folder of the last successful publish, or null.</summary>
    [ObservableProperty]
    private string? _resultFolder;

    /// <summary>Creates the panel.</summary>
    /// <param name="session">The shared workflow data, used to show unexpected errors.</param>
    /// <param name="dialogs">Asks before an existing result folder is used.</param>
    /// <param name="runner">Publishes the migrated copy.</param>
    /// <param name="shell">Opens the result folder.</param>
    /// <param name="migratedFolder">Returns the completed migrated copy, or null when there is none.</param>
    /// <param name="otherWorkRunning">Returns true while a build or a game start runs, so the two never overlap.</param>
    /// <param name="stateChanged">Called when this panel starts or ends work, so the page can refresh its own buttons.</param>
    public PublishPanelViewModel(
        WorkflowSession session,
        IDialogService dialogs,
        IRunService runner,
        IShellService shell,
        Func<string?> migratedFolder,
        Func<bool> otherWorkRunning,
        Action stateChanged)
    {
        _session = session;
        _dialogs = dialogs;
        _runner = runner;
        _shell = shell;
        _migratedFolder = migratedFolder;
        _otherWorkRunning = otherWorkRunning;
        _stateChanged = stateChanged;
    }

    /// <summary>The platforms in the picker.</summary>
    public IReadOnlyList<PublishTarget> Targets => PublishTarget.All;

    /// <summary>What cannot be checked from this computer, shown above the buttons.</summary>
    public string Notice =>
        "Makes one self-contained program, so the other computer needs no .NET. Linux and macOS builds are made here but cannot be started, "
        + "packed as a macOS app or signed from Windows. Native libraries stay as files next to the program. The first publish for a platform downloads its runtime.";

    /// <summary>True when Publish can start: a migrated copy exists and nothing else is running.</summary>
    public bool CanPublish => !IsPublishing && !_otherWorkRunning() && _migratedFolder() is not null;

    /// <summary>True while a publish can be cancelled.</summary>
    public bool CanCancel => IsPublishing;

    /// <summary>True when the result folder can be opened.</summary>
    public bool CanOpenFolder => ResultFolder is not null && !IsPublishing;

    /// <summary>Re-reads whether the buttons are available. The page calls it when its own state changes.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(CanPublish));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanOpenFolder));
        PublishCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Forgets the last result, for a new migration.</summary>
    public void Reset()
    {
        ResultFolder = null;
        IsStatusError = false;
        StatusText = "Not published yet.";
        Refresh();
    }

    /// <summary>Cancels a running publish and waits briefly for its process to stop.</summary>
    public async Task ShutdownAsync()
    {
        _cts?.Cancel();
        if (_idle is { Task.IsCompleted: false } idle)
        {
            await Task.WhenAny(idle.Task, Task.Delay(_shutdownWait)).ConfigureAwait(true);
        }
    }

    partial void OnIsPublishingChanged(bool value)
    {
        if (value)
        {
            _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        else
        {
            _idle?.TrySetResult();
        }

        Refresh();
        _stateChanged();
    }

    partial void OnResultFolderChanged(string? value) => Refresh();

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private async Task PublishAsync()
    {
        if (_migratedFolder() is not { } folder)
        {
            return;
        }

        var rid = SelectedTarget.Rid;
        using var cts = new CancellationTokenSource();
        _cts = cts;
        IsPublishing = true;
        IsStatusError = false;
        ResultFolder = null;
        StatusText = $"Publishing for {SelectedTarget.DisplayName}...";
        var context = SynchronizationContext.Current;
        var log = new BuildRunSink<OutputLine>(lines => StatusText = lines[^1].Text, context);
        try
        {
            var result = await _runner.PublishAsync(folder, rid, overwriteConfirmed: false, log, cts.Token).ConfigureAwait(true);
            if (result.Status == PublishStatus.NeedsConfirmation)
            {
                var yes = await _dialogs.ConfirmAsync(
                    "Publish into a folder that already has content?",
                    result.Message,
                    "Publish again",
                    "Keep the folder").ConfigureAwait(true);
                if (!yes)
                {
                    IsStatusError = false;
                    StatusText = "Nothing was published. The existing folder was not changed.";
                    return;
                }

                result = await _runner.PublishAsync(folder, rid, overwriteConfirmed: true, log, cts.Token).ConfigureAwait(true);
            }

            log.Flush();
            IsStatusError = result.Status is PublishStatus.Failed or PublishStatus.TimedOut or PublishStatus.Refused;
            StatusText = result.Message;
            ResultFolder = result.Succeeded ? result.OutputFolder : null;
        }
        catch (OperationCanceledException)
        {
            log.Flush();
            StatusText = "The publish was cancelled.";
        }
        catch (Exception ex)
        {
            log.Flush();
            IsStatusError = true;
            StatusText = "The publish stopped because of an error.";
            _session.ShowError("The publish could not be run.", ex.ToString());
        }
        finally
        {
            _cts = null;
            IsPublishing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private async Task OpenFolderAsync()
    {
        if (ResultFolder is not { } folder)
        {
            return;
        }

        try
        {
            await _shell.OpenFolderAsync(folder).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _session.ShowError("The folder could not be opened.", ex.ToString());
        }
    }
}
