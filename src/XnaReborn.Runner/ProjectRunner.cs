using System.Text.Json;
using XnaReborn.Runner.Build;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Hints;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Run;
using XnaReborn.Runner.Safety;

namespace XnaReborn.Runner;

/// <summary>What to do with a migrated copy.</summary>
/// <param name="Target">The MonoGame target, used to check that this machine can run it.</param>
/// <param name="Launch">True to start the game after a successful build.</param>
/// <param name="BuildTimeout">Stops the build after this long. Defaults to 5 minutes.</param>
/// <param name="EarlyCrashWindow">A game that exits inside this time counts as a startup failure. Defaults to the launcher's window (10 seconds).</param>
public sealed record RunnerOptions(
    MonoGameTargetKind Target,
    bool Launch,
    TimeSpan? BuildTimeout = null,
    TimeSpan? EarlyCrashWindow = null);

/// <summary>A step of the pipeline, reported to the caller as it starts.</summary>
/// <param name="Stage">One of the stage names on this type.</param>
/// <param name="Detail">A short extra text, such as the path being built, or null.</param>
public sealed record RunnerProgress(string Stage, string? Detail)
{
    /// <summary>The guard checks that the folder is a completed migrated copy.</summary>
    public const string CheckingFolder = "Checking folder";

    /// <summary>The dotnet SDK and the target are checked.</summary>
    public const string CheckingEnvironment = "Checking environment";

    /// <summary>The project is being built.</summary>
    public const string Building = "Building";

    /// <summary>The game is being found and started.</summary>
    public const string Launching = "Launching";

    /// <summary>The game survived its startup window and is running.</summary>
    public const string Running = "Running";
}

/// <summary>Everything that happened in one run of the pipeline. Parts after the step that stopped it are null.</summary>
/// <param name="Guard">The answer of the migrated-copy guard.</param>
/// <param name="Environment">What the machine offers, or null when the guard refused.</param>
/// <param name="Build">The build result, or null when no build ran.</param>
/// <param name="Game">The game that was found, or null when none was looked for or found.</param>
/// <param name="RunningGame">The started game, still running after the startup window. The caller stops it.</param>
/// <param name="StartupFailure">The session of a game that exited inside the startup window, with runtime hints.</param>
/// <param name="Problem">A plain message for the user when the pipeline stopped before the end, otherwise null.</param>
/// <param name="Warnings">Plain messages about things that do not stop the run but are likely to break the build, such as missing audio tools. Null when there are none.</param>
public sealed record RunnerResult(
    GuardVerdict Guard,
    EnvironmentInfo? Environment,
    BuildResult? Build,
    GameTarget? Game,
    RunHandle? RunningGame,
    RunSession? StartupFailure,
    string? Problem,
    IReadOnlyList<string>? Warnings = null)
{
    /// <summary>
    /// True when the guard allowed the folder, the environment is fine, the build succeeded, and, when a launch was
    /// asked for, the game was still running after the startup window.
    /// </summary>
    public bool Succeeded =>
        Guard.Allowed
        && Environment is { SdkCompatible: true, TargetSupported: true }
        && Build is { Succeeded: true }
        && Problem is null
        && StartupFailure is null;
}

/// <summary>
/// Checks, builds and optionally starts a migrated copy: guard, environment, build, then launch. It never builds or runs a
/// folder that is not a completed migrated copy, never elevates, and never uses the network on its own (the build's
/// restore does). One instance remembers which games were approved, so the user is asked once per game.
/// </summary>
public sealed class ProjectRunner
{
    private readonly IProcessRunner _processRunner;
    private readonly GameLauncher _launcher;
    private readonly HintMatcher _matcher;
    private readonly BuildRunner _buildRunner;
    private readonly ManifestBuildRecorder _recorder = new();
    private readonly Func<string, GameLocatorResult> _locateGame;

    /// <summary>Creates a runner.</summary>
    /// <param name="processRunner">Runs the dotnet commands. Defaults to the real process runner.</param>
    /// <param name="launcher">Starts the game. Defaults to a launcher with a 10 second startup window. Keep one per session.</param>
    /// <param name="matcher">Finds hints. Defaults to a matcher over the shipped catalog.</param>
    public ProjectRunner(IProcessRunner? processRunner = null, GameLauncher? launcher = null, HintMatcher? matcher = null)
        : this(processRunner, launcher, matcher, null)
    {
    }

    /// <summary>Creates a runner whose game lookup can be replaced, for tests.</summary>
    internal ProjectRunner(IProcessRunner? processRunner, GameLauncher? launcher, HintMatcher? matcher, Func<string, GameLocatorResult>? locateGame)
    {
        _processRunner = processRunner ?? new ProcessRunner();
        _launcher = launcher ?? new GameLauncher();
        _matcher = matcher ?? new HintMatcher();
        _buildRunner = new BuildRunner(_processRunner, _matcher);
        _locateGame = locateGame ?? (folder => GameLocator.Find(folder));
    }

    /// <summary>Runs the pipeline.</summary>
    /// <param name="migratedFolder">The migrated copy.</param>
    /// <param name="options">What to do.</param>
    /// <param name="confirm">Asks the user before a game is started for the first time. Returns true to allow.</param>
    /// <param name="progress">Receives a message when each stage starts, or null.</param>
    /// <param name="buildOutput">Receives the build output lines as they arrive, or null.</param>
    /// <param name="cancellationToken">Cancels the run. A started build is stopped, and the method throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>
    /// What happened. Problems (a refused folder, a missing SDK, a failed build, a declined launch, an unexpected error) are
    /// returned in the result, not thrown. If the result has a <see cref="RunnerResult.RunningGame"/>, the caller stops it.
    /// </returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async Task<RunnerResult> BuildAndRunAsync(
        string migratedFolder,
        RunnerOptions options,
        Func<LaunchConfirmation, Task<bool>> confirm,
        IProgress<RunnerProgress>? progress,
        IProgress<OutputLine>? buildOutput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(migratedFolder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(confirm);

        var state = new State();
        try
        {
            await RunPipelineAsync(migratedFolder, options, confirm, progress, buildOutput, state, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            state.Problem = "The run stopped because of an unexpected error: " + ex.Message;
        }

        var guard = state.Guard ?? new GuardVerdict(false, state.Problem ?? "The folder was not checked.", null);
        return new RunnerResult(guard, state.Environment, state.Build, state.Game, state.RunningGame, state.StartupFailure, state.Problem, state.Warnings.Count == 0 ? null : state.Warnings);
    }

    private async Task RunPipelineAsync(
        string folder,
        RunnerOptions options,
        Func<LaunchConfirmation, Task<bool>> confirm,
        IProgress<RunnerProgress>? progress,
        IProgress<OutputLine>? buildOutput,
        State state,
        CancellationToken ct)
    {
        // 1. Guard.
        progress?.Report(new RunnerProgress(RunnerProgress.CheckingFolder, folder));
        var guard = MigratedCopyGuard.Check(folder);
        state.Guard = guard;
        if (!guard.Allowed)
        {
            state.Problem = guard.Message;
            return;
        }

        // 2. Environment.
        progress?.Report(new RunnerProgress(RunnerProgress.CheckingEnvironment, null));
        var environment = await new DotnetLocator(_processRunner).DetectAsync(options.Target, ct).ConfigureAwait(false);
        state.Environment = environment;
        if (!environment.SdkCompatible || !environment.TargetSupported)
        {
            state.Problem = environment.Problem ?? "This computer cannot build the project.";
            return;
        }

        // Audio is built with ffmpeg and ffprobe: tell the user before the build if they are missing.
        var audio = await AudioToolCheck.CheckAsync(folder, _processRunner, ct).ConfigureAwait(false);
        if (audio.Warning is not null)
        {
            state.Warnings.Add(audio.Warning);
            buildOutput?.Report(new OutputLine("warning: " + audio.Warning, IsError: true));
        }

        // 3. Build. Restore is part of it.
        var timeout = options.BuildTimeout ?? BuildRunner.DefaultTimeout;
        progress?.Report(new RunnerProgress(RunnerProgress.Building, guard.ProjectOrSolutionPath));
        var build = await _buildRunner.BuildAsync(folder, guard.ProjectOrSolutionPath!, timeout, buildOutput, ct).ConfigureAwait(false);
        state.Build = build;
        ct.ThrowIfCancellationRequested();

        try
        {
            await _recorder.RecordAsync(folder, build, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            state.Problem = "The build ran, but its result could not be written to the migration manifest: " + ex.Message;
            return;
        }

        // 4. Stop on failure.
        if (!build.Succeeded)
        {
            state.Problem = build.TimedOut
                ? $"The build did not finish within {timeout.TotalMinutes:0.##} minutes and was stopped."
                : "The build failed.";
            return;
        }

        if (!options.Launch)
        {
            return;
        }

        // 5. Find and launch the game.
        progress?.Report(new RunnerProgress(RunnerProgress.Launching, null));
        var located = _locateGame(folder);
        if (located.Target is null)
        {
            state.Problem = located.Problem ?? "The game was not found.";
            return;
        }

        var game = located.Target;
        state.Game = game;
        var handle = await _launcher.LaunchAsync(
            game.ExecutablePath,
            Path.GetDirectoryName(game.ExecutablePath)!,
            confirm,
            ct,
            game.LaunchArguments).ConfigureAwait(false);
        if (handle is null)
        {
            state.Problem = "Launch declined.";
            return;
        }

        // 6. Wait through the startup window.
        bool survived;
        try
        {
            survived = await WaitForStartupAsync(handle, options.EarlyCrashWindow, ct).ConfigureAwait(false);
        }
        catch
        {
            await handle.StopAsync().ConfigureAwait(false);
            throw;
        }

        if (survived)
        {
            state.RunningGame = handle;
            progress?.Report(new RunnerProgress(RunnerProgress.Running, game.ExecutablePath));
            return;
        }

        var session = await handle.Completion.ConfigureAwait(false);
        var hints = new RuntimeFailureDetector(_matcher).Detect(session.Stdout, session.Stderr);
        state.StartupFailure = session with { RuntimeHints = hints, StartupFailure = session.ExitCode is not 0 };
        state.Problem = session.ExitCode is 0
            ? "The game closed right after it started."
            : $"The game stopped right after it started (exit code {session.ExitCode}).";
    }

    private static async Task<bool> WaitForStartupAsync(RunHandle handle, TimeSpan? window, CancellationToken ct)
    {
        if (window is not { } custom)
        {
            return await handle.WaitForStartupAsync(ct).ConfigureAwait(false);
        }

        await Task.WhenAny(handle.Completion, Task.Delay(custom, ct)).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return !handle.HasExited;
    }

    private sealed class State
    {
        public GuardVerdict? Guard { get; set; }

        public EnvironmentInfo? Environment { get; set; }

        public BuildResult? Build { get; set; }

        public GameTarget? Game { get; set; }

        public RunHandle? RunningGame { get; set; }

        public RunSession? StartupFailure { get; set; }

        public string? Problem { get; set; }

        public List<string> Warnings { get; } = [];
    }
}
