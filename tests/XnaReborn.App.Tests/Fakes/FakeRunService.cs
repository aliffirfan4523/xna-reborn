using XnaReborn.App.Services;
using XnaReborn.Runner;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Run;

namespace XnaReborn.App.Tests.Fakes;

/// <summary>Never builds or starts anything. Returns scripted results and can ask the confirmation callback.</summary>
public sealed class FakeRunService : IRunService
{
    /// <summary>What <see cref="BuildAndRunAsync"/> returns.</summary>
    public RunnerResult Result { get; set; } = SampleData.Runner();

    /// <summary>What <see cref="CheckEnvironmentAsync"/> returns.</summary>
    public EnvironmentInfo Environment { get; set; } = SampleData.Environment();

    /// <summary>When set and <c>launch</c> is true, this is passed to the <c>confirm</c> callback before returning.</summary>
    public LaunchConfirmation? ConfirmationToAsk { get; set; }

    /// <summary>What the <c>confirm</c> callback answered, or null when it was not asked.</summary>
    public bool? ConfirmationAnswer { get; private set; }

    /// <summary>Thrown by <see cref="BuildAndRunAsync"/> when set.</summary>
    public Exception? Failure { get; set; }

    /// <summary>The calls to <see cref="BuildAndRunAsync"/>, in order.</summary>
    public List<(string Folder, MonoGameTargetKind Target, bool Launch)> RunCalls { get; } = [];

    public async Task<RunnerResult> BuildAndRunAsync(
        string migratedFolder,
        MonoGameTargetKind target,
        bool launch,
        Func<LaunchConfirmation, Task<bool>> confirm,
        IProgress<RunnerProgress>? progress,
        IProgress<OutputLine>? buildOutput,
        CancellationToken cancellationToken)
    {
        RunCalls.Add((migratedFolder, target, launch));
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is not null)
        {
            throw Failure;
        }

        progress?.Report(new RunnerProgress(RunnerProgress.Building, migratedFolder));
        buildOutput?.Report(new OutputLine("Build succeeded.", false));
        if (launch && ConfirmationToAsk is not null)
        {
            ConfirmationAnswer = await confirm(ConfirmationToAsk);
        }

        return Result;
    }

    public Task<EnvironmentInfo> CheckEnvironmentAsync(MonoGameTargetKind target, CancellationToken cancellationToken) =>
        Task.FromResult(Environment);
}
