using XnaReborn.App.Services;
using XnaReborn.Runner;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Run;

namespace XnaReborn.App.Tests.BuildRun;

/// <summary>A run service whose build is a delegate, so a test controls the log, the timing and the result. Starts nothing.</summary>
internal sealed class ScriptedRunService : IRunService
{
    public delegate Task<RunnerResult> RunScript(
        bool launch,
        Func<LaunchConfirmation, Task<bool>> confirm,
        IProgress<RunnerProgress>? progress,
        IProgress<OutputLine>? output,
        CancellationToken cancellationToken);

    public RunScript OnRun { get; set; } = (_, _, _, _, _) => throw new InvalidOperationException("No script.");

    public EnvironmentInfo Environment { get; set; } = new("8.0.100", ["8.0.100"], true, "Windows 11", true, null);

    /// <summary>When set, replaces the environment check, so a test can hold it open and cancel it.</summary>
    public Func<CancellationToken, Task<EnvironmentInfo>>? OnCheckEnvironment { get; set; }

    public int EnvironmentChecks { get; private set; }

    public List<(string Folder, MonoGameTargetKind Target, bool Launch)> Calls { get; } = [];

    public Task<RunnerResult> BuildAndRunAsync(
        string migratedFolder,
        MonoGameTargetKind target,
        bool launch,
        Func<LaunchConfirmation, Task<bool>> confirm,
        IProgress<RunnerProgress>? progress,
        IProgress<OutputLine>? buildOutput,
        CancellationToken cancellationToken)
    {
        Calls.Add((migratedFolder, target, launch));
        return OnRun(launch, confirm, progress, buildOutput, cancellationToken);
    }

    public Task<EnvironmentInfo> CheckEnvironmentAsync(MonoGameTargetKind target, CancellationToken cancellationToken)
    {
        EnvironmentChecks++;
        return OnCheckEnvironment is { } script ? script(cancellationToken) : Task.FromResult(Environment);
    }
}
