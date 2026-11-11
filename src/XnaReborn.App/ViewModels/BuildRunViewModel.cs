using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XnaReborn.App.Services;
using XnaReborn.Core.Models;
using XnaReborn.Runner;
using XnaReborn.Runner.Diagnostics;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Publish;
using XnaReborn.Runner.Run;

namespace XnaReborn.App.ViewModels;

/// <summary>
/// The "Build and run" page: shows the environment, builds the migrated copy with a live log, groups the diagnostics by
/// fix hint, and starts or stops the game. A game starts only after the user answers yes to the launch question.
/// </summary>
public sealed partial class BuildRunViewModel : PageViewModel
{
    private static readonly TimeSpan _shutdownWait = TimeSpan.FromSeconds(5);

    private static readonly Regex _link = new(@"https://\S+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IRunService _runner;
    private readonly ISettingsService _settings;
    private AnalysisReport? _defaultsFor;
    private CancellationTokenSource? _cts;
    private RunnerResult? _lastResult;
    private RunHandle? _game;
    private int _lineNumber;
    private bool _declined;
    private bool _closing;
    private TaskCompletionSource? _idle;
    private Task _watching = Task.CompletedTask;
    private bool _inRun;
    private bool _awaitingConfirm;
    private bool _launched;
    private Stopwatch? _clock;

    /// <summary>True while a build, a start or an environment check is running.</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>True while the game started from this page is running.</summary>
    [ObservableProperty]
    private bool _isGameRunning;

    /// <summary>True when there is a completed migrated copy and options to build.</summary>
    [ObservableProperty]
    private bool _hasMigration;

    /// <summary>What the machine offers, or null before the first check and while a check runs.</summary>
    [ObservableProperty]
    private EnvironmentInfo? _environmentStatus;

    /// <summary>The SDK line of the environment panel.</summary>
    [ObservableProperty]
    private string _sdkText = "Not checked yet.";

    /// <summary>The target line of the environment panel.</summary>
    [ObservableProperty]
    private string _targetText = string.Empty;

    /// <summary>What is missing on this machine, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEnvironmentProblem))]
    private string? _environmentProblem;

    /// <summary>The download link inside <see cref="EnvironmentProblem"/>, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEnvironmentLink))]
    private string? _environmentLink;

    /// <summary>The line under the buttons: the current stage, or the outcome of the last run.</summary>
    [ObservableProperty]
    private string _statusText = "Ready.";

    /// <summary>True when <see cref="StatusText"/> describes a failure.</summary>
    [ObservableProperty]
    private bool _isStatusError;

    /// <summary>True when a run has finished and its result is in the session.</summary>
    [ObservableProperty]
    private bool _hasResult;

    /// <summary>The diagnostic groups of the last build, errors first.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGroups))]
    private IReadOnlyList<DiagnosticGroupViewModel> _groups = [];

    /// <summary>The counts above the group list.</summary>
    [ObservableProperty]
    private string _diagnosticsSummary = string.Empty;

    /// <summary>True when the game stopped right after it started.</summary>
    [ObservableProperty]
    private bool _hasStartupFailure;

    /// <summary>Hints for the runtime failure recognized in the game's output.</summary>
    [ObservableProperty]
    private IReadOnlyList<DiagnosticHintViewModel> _runtimeHints = [];

    /// <summary>The standard output and error captured from the game, after it ended.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGameOutput))]
    private string _gameOutput = string.Empty;

    /// <summary>True while the log follows the newest line.</summary>
    [ObservableProperty]
    private bool _autoScroll = true;

    /// <summary>Only lines that contain this text (any letter case) are shown in <see cref="VisibleLogLines"/>.</summary>
    [ObservableProperty]
    private string _logFilter = string.Empty;

    /// <summary>When true, <see cref="VisibleLogLines"/> holds only error lines.</summary>
    [ObservableProperty]
    private bool _errorsOnly;

    /// <summary>The running game in words, such as the process id, or empty.</summary>
    [ObservableProperty]
    private string _gameStatusText = string.Empty;

    /// <summary>Creates the page.</summary>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="navigator">Moves to other pages.</param>
    /// <param name="dialogs">File pickers and message dialogs.</param>
    /// <param name="runner">Builds and launches the migrated copy.</param>
    /// <param name="shell">Opens the folder of a published game.</param>
    /// <param name="settings">The saved defaults: follow the log, show hints, the publish platform and the SDK check. A choice made here for one run overrides them.</param>
    public BuildRunViewModel(WorkflowSession session, INavigator navigator, IDialogService dialogs, IRunService runner, IShellService shell, ISettingsService settings)
        : base("Build and run", session, navigator, dialogs)
    {
        _runner = runner;
        _settings = settings;
        Steps = [SdkStep, BuildStep, LaunchStep];
        Publish = new PublishPanelViewModel(session, dialogs, runner, shell, () => Target()?.Folder, () => IsBusy || IsGameRunning, RefreshState);
    }

    /// <summary>Pipeline card for the SDK check.</summary>
    public PipelineStepViewModel SdkStep { get; } = new(".NET SDK check");

    /// <summary>Pipeline card for the build.</summary>
    public PipelineStepViewModel BuildStep { get; } = new("Build");

    /// <summary>Pipeline card for starting the game.</summary>
    public PipelineStepViewModel LaunchStep { get; } = new("Launch");

    /// <summary>The three pipeline cards in order.</summary>
    public IReadOnlyList<PipelineStepViewModel> Steps { get; }

    /// <summary>The log lines that pass <see cref="LogFilter"/> and <see cref="ErrorsOnly"/>. The log view shows this list.</summary>
    public ObservableCollection<BuildRunLogLine> VisibleLogLines { get; } = [];

    /// <summary>How many lines are shown, out of how many, and how many are errors.</summary>
    public string LogSummary
    {
        get
        {
            var errors = LogLines.Count(l => l.IsError);
            return $"{Plural(VisibleLogLines.Count, "line")} shown of {LogLines.Count}, {Plural(errors, "error line")}.";
        }
    }

    /// <summary>The Publish section: platform picker, Publish, progress, Cancel and Open folder.</summary>
    public PublishPanelViewModel Publish { get; }

    /// <summary>The build output so far, in arrival order.</summary>
    // shortcut: the log is never trimmed, fine for a build of a small game, add a cap if logs reach 100k lines.
    public ObservableCollection<BuildRunLogLine> LogLines { get; } = [];

    /// <summary>True when <see cref="EnvironmentProblem"/> has a value.</summary>
    public bool HasEnvironmentProblem => EnvironmentProblem is not null;

    /// <summary>True when <see cref="EnvironmentLink"/> has a value.</summary>
    public bool HasEnvironmentLink => EnvironmentLink is not null;

    /// <summary>True when there is a group to show.</summary>
    public bool HasGroups => Groups.Count > 0;

    /// <summary>True when the game left output to show.</summary>
    public bool HasGameOutput => GameOutput.Length > 0;

    /// <summary>True when a startup failure has no known hint.</summary>
    public bool HasNoRuntimeHints => HasStartupFailure && RuntimeHints.Count == 0;

    /// <summary>The text of the startup failure, or null.</summary>
    public string? StartupFailureMessage { get; private set; }

    /// <summary>Why nothing can be built yet, or null.</summary>
    public string? NoMigrationMessage => HasMigration ? null : "Migrate a project first. Build and run needs a completed migrated copy.";

    /// <summary>True when Build and Build and Run can start: a copy exists, this machine can build it, and nothing else is running.</summary>
    public bool CanBuild =>
        HasMigration
        && !IsBusy
        && !IsGameRunning
        && !Publish.IsPublishing
        && (EnvironmentStatus is { SdkCompatible: true, TargetSupported: true } || (EnvironmentStatus is null && !_settings.Current.CheckSdkOnStart));

    /// <summary>True while a build can be cancelled.</summary>
    public bool CanCancel => IsBusy;

    /// <summary>True while the game can be stopped.</summary>
    public bool CanStop => IsGameRunning;

    /// <summary>True when the Summary page can be opened after a run.</summary>
    public bool CanGoToSummary => HasResult && !IsBusy;

    /// <inheritdoc />
    public override async Task OnNavigatedToAsync()
    {
        UpdateMigration();
        if (IsBusy || IsGameRunning)
        {
            return;
        }

        if (!ReferenceEquals(Session.RunnerResult, _lastResult))
        {
            // The session was reset (a new project or migration), so the old log and diagnostics no longer apply.
            ResetResults();
            Publish.Reset();
        }

        // The saved defaults are read once per opened project. Choices made on this page afterwards are kept.
        if (Session.Report is { } report && !ReferenceEquals(report, _defaultsFor))
        {
            _defaultsFor = report;
            AutoScroll = _settings.Current.AutoFollowLog;
            Publish.SelectedTarget = PublishTarget.Find(_settings.Current.PublishRid) ?? PublishTarget.WindowsX64;
        }

        if (_settings.Current.CheckSdkOnStart)
        {
            await RefreshEnvironmentAsync().ConfigureAwait(true);
        }
        else if (EnvironmentStatus is null)
        {
            SdkText = "Not checked. Press Check again to check the .NET SDK, or build and read the result.";
            RefreshState();
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        if (value)
        {
            _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        else
        {
            _idle?.TrySetResult();
        }

        RefreshState();
    }

    partial void OnIsGameRunningChanged(bool value) => RefreshState();

    partial void OnLogFilterChanged(string value) => RebuildVisibleLog();

    partial void OnErrorsOnlyChanged(bool value) => RebuildVisibleLog();

    partial void OnHasMigrationChanged(bool value)
    {
        OnPropertyChanged(nameof(NoMigrationMessage));
        RefreshState();
    }

    partial void OnEnvironmentStatusChanged(EnvironmentInfo? value) => RefreshState();

    partial void OnHasResultChanged(bool value) => RefreshState();

    partial void OnHasStartupFailureChanged(bool value) => OnPropertyChanged(nameof(HasNoRuntimeHints));

    partial void OnRuntimeHintsChanged(IReadOnlyList<DiagnosticHintViewModel> value) => OnPropertyChanged(nameof(HasNoRuntimeHints));

    private void RefreshState()
    {
        OnPropertyChanged(nameof(CanBuild));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanGoToSummary));
        BuildCommand.NotifyCanExecuteChanged();
        BuildAndRunCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        GoToSummaryCommand.NotifyCanExecuteChanged();
        RefreshEnvironmentCommand.NotifyCanExecuteChanged();
        Publish?.Refresh();
        RefreshSteps();
    }

    private bool PassesFilter(BuildRunLogLine line) =>
        (!ErrorsOnly || line.IsError)
        && (LogFilter.Length == 0 || line.Text.Contains(LogFilter, StringComparison.OrdinalIgnoreCase));

    private void RebuildVisibleLog()
    {
        VisibleLogLines.Clear();
        foreach (var line in LogLines.Where(PassesFilter))
        {
            VisibleLogLines.Add(line);
        }

        OnPropertyChanged(nameof(LogSummary));
    }

    private void RefreshSteps()
    {
        var result = Session.RunnerResult;
        var build = _lastResult?.Build;
        var ok = EnvironmentStatus is { SdkCompatible: true, TargetSupported: true };

        if (!HasMigration)
        {
            SdkStep.Set(PipelineState.Waiting, "Migrate a project first.");
        }
        else if (EnvironmentStatus is null)
        {
            SdkStep.Set(IsBusy && !_inRun ? PipelineState.Running : PipelineState.Waiting, SdkText);
        }
        else
        {
            SdkStep.Set(ok ? PipelineState.Done : PipelineState.Failed, ok ? SdkText : EnvironmentProblem ?? SdkText);
        }

        if (_inRun && !_awaitingConfirm)
        {
            BuildStep.Set(PipelineState.Running, "Building the migrated copy.", Elapsed());
        }
        else if (build is not null)
        {
            BuildStep.Set(build.Succeeded ? PipelineState.Done : PipelineState.Failed, build.Succeeded ? "Build succeeded." : "Build failed.", Seconds(build.Duration));
        }
        else if (_awaitingConfirm)
        {
            BuildStep.Set(PipelineState.Done, "Build succeeded.", BuildStep.ElapsedText);
        }
        else if (HasResult)
        {
            BuildStep.Set(PipelineState.Failed, _lastResult?.Problem ?? "The build did not run.");
        }
        else
        {
            BuildStep.Set(PipelineState.Waiting, "Not built yet.");
        }

        if (IsGameRunning)
        {
            LaunchStep.Set(PipelineState.Running, GameStatusText);
        }
        else if (_awaitingConfirm)
        {
            LaunchStep.Set(PipelineState.Running, "Waiting for your OK. Nothing starts until you say yes.");
        }
        else if (HasStartupFailure)
        {
            LaunchStep.Set(PipelineState.Failed, "The game stopped right after it started.");
        }
        else if (_declined)
        {
            LaunchStep.Set(PipelineState.Skipped, "You chose not to run the game.");
        }
        else if (_launched)
        {
            LaunchStep.Set(PipelineState.Done, "The game ran and closed.");
        }
        else if (HasResult && result is not null)
        {
            LaunchStep.Set(PipelineState.Skipped, "Not started in this run.");
        }
        else
        {
            LaunchStep.Set(PipelineState.Waiting, "Starts after a good build, and only after you confirm.");
        }
    }

    private string Elapsed() => _clock is null ? string.Empty : Seconds(_clock.Elapsed);

    private async Task TickAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (_inRun && await timer.WaitForNextTickAsync().ConfigureAwait(true))
        {
            RefreshSteps();
        }
    }

    /// <summary>
    /// Cancels a running check or build, waits (up to <see cref="_shutdownWait"/>) until it has stopped its processes, and stops
    /// a running game. A game that was about to be returned by a cancelled run is stopped too.
    /// </summary>
    public override async Task ShutdownAsync()
    {
        _closing = true;
        _cts?.Cancel();
        await Publish.ShutdownAsync().ConfigureAwait(true);
        if (_idle is { Task.IsCompleted: false } idle)
        {
            await Task.WhenAny(idle.Task, Task.Delay(_shutdownWait)).ConfigureAwait(true);
        }

        if (_game is { } game)
        {
            try
            {
                await game.StopAsync().ConfigureAwait(true);
                EndGame(game, await game.Completion.ConfigureAwait(true));
            }
            catch (Exception)
            {
                // The window is closing, so there is nowhere left to show a failure to stop.
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanBuild))]
    private Task BuildAsync() => RunAsync(launch: false);

    [RelayCommand(CanExecute = nameof(CanBuild))]
    private Task BuildAndRunAsync() => RunAsync(launch: true);

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanGoToSummary))]
    private void GoToSummary() => Navigator.GoTo(WorkflowStep.Summary);

    [RelayCommand(CanExecute = nameof(CanRefreshEnvironment))]
    private async Task RefreshEnvironmentAsync()
    {
        if (!UpdateMigration() || Target() is not { } target)
        {
            return;
        }

        using var cts = new CancellationTokenSource();
        _cts = cts;
        IsBusy = true;
        EnvironmentStatus = null;
        SdkText = "Checking the .NET SDK...";
        TargetText = string.Empty;
        EnvironmentProblem = null;
        EnvironmentLink = null;
        try
        {
            var info = await _runner.CheckEnvironmentAsync(target.Kind, cts.Token).ConfigureAwait(true);
            ShowEnvironment(info, target.Kind);
        }
        catch (OperationCanceledException)
        {
            SdkText = "Environment check cancelled.";
        }
        catch (Exception ex)
        {
            SdkText = "The environment could not be checked.";
            Session.ShowError("The environment could not be checked.", ex.ToString());
        }
        finally
        {
            _cts = null;
            IsBusy = false;
        }
    }

    private bool CanRefreshEnvironment() => HasMigration && !IsBusy && !IsGameRunning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        if (_game is not { } game)
        {
            await _watching.ConfigureAwait(true);
            return;
        }

        try
        {
            await game.StopAsync().ConfigureAwait(true);
            EndGame(game, await game.Completion.ConfigureAwait(true));

            // The watcher may have won the race to EndGame on another thread; wait until it has finished writing state.
            await _watching.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Session.ShowError("The game could not be stopped.", ex.ToString());
        }
    }

    private async Task RunAsync(bool launch)
    {
        if (!UpdateMigration() || Target() is not { } target)
        {
            return;
        }

        ResetResults();
        Session.RunnerResult = null;
        _declined = false;
        using var cts = new CancellationTokenSource();
        _cts = cts;
        _launched = false;
        _inRun = true;
        _clock = Stopwatch.StartNew();
        IsBusy = true;
        _ = TickAsync();
        StatusText = "Starting...";
        var context = SynchronizationContext.Current;
        var log = new BuildRunSink<OutputLine>(AppendLog, context);
        var stages = new BuildRunSink<RunnerProgress>(ShowStage, context);
        try
        {
            RunnerResult? result = null;
            try
            {
                result = await _runner.BuildAndRunAsync(
                    target.Folder, target.Kind, launch, CreateConfirm(context), stages, log, cts.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                StatusText = "Cancelled. Nothing was started.";
            }
            catch (Exception ex)
            {
                StatusText = "The run stopped because of an error.";
                IsStatusError = true;
                Session.ShowError("The build could not be run.", ex.ToString());
            }
            finally
            {
                // Drain the log before the result is shown, so no line arrives after the outcome.
                log.Flush();
                stages.Flush();
            }

            if (result is not null)
            {
                await ShowResultAsync(result, launch).ConfigureAwait(true);
            }
        }
        finally
        {
            _cts = null;
            _inRun = false;
            _awaitingConfirm = false;
            IsBusy = false;
        }
    }

    private async Task ShowResultAsync(RunnerResult result, bool launch)
    {
        // Safety net: a game that was not asked for, was declined, or finished starting while the window closes must not stay running.
        var game = result.RunningGame;
        if (game is not null && (!launch || _declined || _closing))
        {
            await game.StopAsync().ConfigureAwait(true);
            game = null;
            result = result with { RunningGame = null };
        }

        Session.RunnerResult = result;
        _lastResult = result;
        HasResult = true;

        Groups = result.Build?.Groups.Select(g => new DiagnosticGroupViewModel(g, _settings.Current.ShowHints)).ToList() ?? [];
        if (Groups.FirstOrDefault(g => g.IsError) is { } first)
        {
            first.IsExpanded = true;
        }

        DiagnosticsSummary = Summarize(Groups);

        if (result.StartupFailure is { } failure)
        {
            HasStartupFailure = true;
            StartupFailureMessage = result.Problem ?? "The game stopped right after it started.";
            OnPropertyChanged(nameof(StartupFailureMessage));
            RuntimeHints = failure.RuntimeHints.Select(h => new DiagnosticHintViewModel(h)).ToList();
            GameOutput = FormatOutput(failure);
        }

        SetOutcome(result, game);
        if (game is not null)
        {
            _launched = true;
            GameStatusText = $"Process {game.ProcessId} is running.";
            IsGameRunning = true;
            _game = game;
            _watching = WatchAsync(game);
        }

        RefreshSteps();
    }

    private void SetOutcome(RunnerResult result, RunHandle? game)
    {
        IsStatusError = true;
        if (game is not null)
        {
            IsStatusError = false;
            StatusText = $"The game is running (process {game.ProcessId}). Its output appears here when it closes.";
        }
        else if (result.StartupFailure is not null)
        {
            StatusText = StartupFailureMessage!;
        }
        else if (_declined)
        {
            IsStatusError = false;
            StatusText = "Launch cancelled. The game was not started.";
        }
        else if (result.Build is { Succeeded: false } failed)
        {
            StatusText = failed.TimedOut && result.Problem is not null
                ? result.Problem
                : $"The build failed: {Plural(failed.Groups.Sum(g => g.Items.Count(i => i.Severity == BuildDiagnosticSeverity.Error)), "error")} in {Seconds(failed.Duration)}.";
        }
        else if (result.Problem is not null)
        {
            StatusText = result.Problem;
        }
        else if (result.Build is { } ok)
        {
            IsStatusError = false;
            StatusText = $"The build succeeded in {Seconds(ok.Duration)}.";
        }
        else
        {
            IsStatusError = false;
            StatusText = "Done.";
        }
    }

    private async Task WatchAsync(RunHandle game)
    {
        try
        {
            EndGame(game, await game.Completion.ConfigureAwait(true));
        }
        catch (Exception ex)
        {
            Session.ShowError("The game's output could not be read.", ex.ToString());
        }
    }

    private void EndGame(RunHandle game, RunSession session)
    {
        if (!ReferenceEquals(_game, game))
        {
            return;
        }

        _game = null;
        IsGameRunning = false;
        GameOutput = FormatOutput(session);
        IsStatusError = !session.StoppedByUser && session.ExitCode is not (null or 0);
        StatusText = session.StoppedByUser
            ? "The game was stopped."
            : $"The game closed (exit code {session.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}).";
    }

    private Func<LaunchConfirmation, Task<bool>> CreateConfirm(SynchronizationContext? context) => request =>
    {
        // The runner may ask from a worker thread; the dialog must open on the UI thread.
        if (context is null || SynchronizationContext.Current == context)
        {
            return AskLaunchAsync(request);
        }

        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(
            async _ =>
            {
                try
                {
                    answer.SetResult(await AskLaunchAsync(request).ConfigureAwait(true));
                }
                catch (Exception ex)
                {
                    answer.SetException(ex);
                }
            },
            null);
        return answer.Task;
    };

    private async Task<bool> AskLaunchAsync(LaunchConfirmation request)
    {
        _awaitingConfirm = true;
        RefreshSteps();
        try
        {
            var allowed = await Dialogs.ConfirmAsync("Run this game?", request.Message, "Run game", "Don't run").ConfigureAwait(true);
            _declined = !allowed;
            return allowed;
        }
        finally
        {
            _awaitingConfirm = false;
        }
    }

    private void AppendLog(IReadOnlyList<OutputLine> lines)
    {
        foreach (var line in lines)
        {
            var entry = new BuildRunLogLine(++_lineNumber, line.Text, line.IsError);
            LogLines.Add(entry);
            if (PassesFilter(entry))
            {
                VisibleLogLines.Add(entry);
            }
        }

        OnPropertyChanged(nameof(LogSummary));
    }

    private void ShowStage(IReadOnlyList<RunnerProgress> stages) => StatusText = stages[^1].Stage + "...";

    private void ShowEnvironment(EnvironmentInfo info, MonoGameTargetKind kind)
    {
        EnvironmentStatus = info;
        SdkText = (info.DotnetVersion, info.SdkCompatible) switch
        {
            (null, _) => ".NET SDK: not found.",
            (var v, true) => $".NET SDK {v}: found.",
            (var v, false) => $".NET SDK {v}: found, but version 8.0 or later is needed.",
        };
        TargetText = info.TargetSupported
            ? $"Target {kind}: supported on {info.Os}."
            : $"Target {kind}: not supported on {info.Os}.";
        EnvironmentProblem = info.Problem;
        EnvironmentLink = info.Problem is null ? null : _link.Match(info.Problem) is { Success: true } m ? m.Value.TrimEnd('.', ',', ')') : null;
    }

    private bool UpdateMigration()
    {
        HasMigration = Target() is not null;
        return HasMigration;
    }

    private (string Folder, MonoGameTargetKind Kind)? Target() =>
        Session.MigrationResult is { Completed: true } && Session.MigrationOptions is { } options && !string.IsNullOrWhiteSpace(options.OutputFolder)
            ? (options.OutputFolder, options.Target.ToRunnerKind())
            : null;

    private void ResetResults()
    {
        LogLines.Clear();
        VisibleLogLines.Clear();
        OnPropertyChanged(nameof(LogSummary));
        _launched = false;
        _declined = false;
        GameStatusText = string.Empty;
        _lineNumber = 0;
        Groups = [];
        DiagnosticsSummary = string.Empty;
        HasStartupFailure = false;
        StartupFailureMessage = null;
        OnPropertyChanged(nameof(StartupFailureMessage));
        RuntimeHints = [];
        GameOutput = string.Empty;
        HasResult = false;
        _lastResult = null;
        IsStatusError = false;
        StatusText = "Ready.";
    }

    private static string Summarize(IReadOnlyList<DiagnosticGroupViewModel> groups)
    {
        if (groups.Count == 0)
        {
            return string.Empty;
        }

        var errors = groups.Sum(g => g.ErrorCount);
        var warnings = groups.Sum(g => g.WarningCount);
        return $"{Plural(groups.Count, "group")}: {Plural(errors, "error")}, {Plural(warnings, "warning")}.";
    }

    private static string FormatOutput(RunSession session)
    {
        var text = new StringBuilder();
        if (session.Stdout.Length > 0)
        {
            text.Append("Standard output\n").Append(session.Stdout).Append('\n');
        }

        if (session.Stderr.Length > 0)
        {
            text.Append(text.Length > 0 ? "\n" : string.Empty).Append("Standard error\n").Append(session.Stderr).Append('\n');
        }

        return text.ToString();
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static string Seconds(TimeSpan time) => time.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
}
