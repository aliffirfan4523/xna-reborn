using XnaReborn.Migrator;
using XnaReborn.Runner;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Run;

namespace XnaReborn.App.Services;

/// <summary>Builds and launches a migrated copy. Wraps one <c>ProjectRunner</c> for the whole app session.</summary>
public interface IRunService
{
    /// <summary>Checks, builds and optionally starts a migrated copy. Problems come back in the result, not as exceptions.</summary>
    /// <param name="migratedFolder">The completed migrated copy.</param>
    /// <param name="target">The MonoGame target the copy was migrated for. See <see cref="TargetExtensions.ToRunnerKind"/>.</param>
    /// <param name="launch">True to start the game after a successful build.</param>
    /// <param name="confirm">Asks the user before a game starts for the first time in this session. Returns true to allow.</param>
    /// <param name="progress">Receives a message when each stage starts, or null.</param>
    /// <param name="buildOutput">Receives build output lines as they arrive, or null.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<RunnerResult> BuildAndRunAsync(
        string migratedFolder,
        MonoGameTargetKind target,
        bool launch,
        Func<LaunchConfirmation, Task<bool>> confirm,
        IProgress<RunnerProgress>? progress,
        IProgress<OutputLine>? buildOutput,
        CancellationToken cancellationToken);

    /// <summary>Reports the dotnet SDK, the operating system and whether the target can run here.</summary>
    /// <param name="target">The MonoGame target to check.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    Task<EnvironmentInfo> CheckEnvironmentAsync(MonoGameTargetKind target, CancellationToken cancellationToken);
}

/// <summary>Conversions between the migrator and runner target enums.</summary>
public static class TargetExtensions
{
    /// <summary>Maps the migrator's <see cref="MonoGameTarget"/> to the runner's <see cref="MonoGameTargetKind"/>.</summary>
    /// <param name="target">The migrator target.</param>
    public static MonoGameTargetKind ToRunnerKind(this MonoGameTarget target) => target switch
    {
        MonoGameTarget.DesktopGL => MonoGameTargetKind.DesktopGL,
        MonoGameTarget.WindowsDX => MonoGameTargetKind.WindowsDX,
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
    };
}
