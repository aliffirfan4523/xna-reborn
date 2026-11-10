using XnaReborn.Runner;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Run;

namespace XnaReborn.App.Services;

/// <summary>Real <see cref="IRunService"/>. Keeps one <see cref="ProjectRunner"/> so a game is confirmed once per session.</summary>
public sealed class RunService : IRunService
{
    private readonly ProjectRunner _runner = new();
    private readonly DotnetLocator _locator = new(new ProcessRunner());

    /// <inheritdoc />
    public Task<RunnerResult> BuildAndRunAsync(
        string migratedFolder,
        MonoGameTargetKind target,
        bool launch,
        Func<LaunchConfirmation, Task<bool>> confirm,
        IProgress<RunnerProgress>? progress,
        IProgress<OutputLine>? buildOutput,
        CancellationToken cancellationToken) =>
        _runner.BuildAndRunAsync(migratedFolder, new RunnerOptions(target, launch), confirm, progress, buildOutput, cancellationToken);

    /// <inheritdoc />
    public Task<EnvironmentInfo> CheckEnvironmentAsync(MonoGameTargetKind target, CancellationToken cancellationToken) =>
        _locator.DetectAsync(target, cancellationToken);
}
