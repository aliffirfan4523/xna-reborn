using XnaReborn.App.Services;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;

namespace XnaReborn.App.Tests.Fakes;

/// <summary>Plans with the real (pure) planner and returns scripted answers for everything that would touch the disk.</summary>
public sealed class FakeMigrationService : IMigrationService
{
    /// <summary>The answer of <see cref="CheckOutput"/>.</summary>
    public GuardResult OutputVerdict { get; set; } = new(true, GuardProblem.None, "OK");

    /// <summary>What <see cref="MigrateAsync"/> returns. When null, a sample result is made: completed unless the run is a dry run.</summary>
    public MigrationResult? Result { get; set; }

    /// <summary>Thrown by <see cref="MigrateAsync"/> when set.</summary>
    public Exception? Failure { get; set; }

    /// <summary>The calls to <see cref="MigrateAsync"/>, in order.</summary>
    public List<(string SourcePath, MigrationOptions Options, bool ConfirmedNonEmpty)> MigrateCalls { get; } = [];

    public MigrationPlan CreatePlan(AnalysisReport report, IReadOnlySet<string> excluded) =>
        new MigrationPlanner().CreatePlan(report, excluded);

    public GuardResult CheckOutput(string sourceRoot, string outputFolder, bool confirmedNonEmpty) => OutputVerdict;

    public Task<MigrationResult> MigrateAsync(
        string sourcePath,
        AnalysisReport report,
        MigrationOptions options,
        bool confirmedNonEmpty,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        MigrateCalls.Add((sourcePath, options, confirmedNonEmpty));
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is not null)
        {
            return Task.FromException<MigrationResult>(Failure);
        }

        progress?.Report(new MigrationProgress("Done", 1, 1, null));
        return Task.FromResult(Result ?? SampleData.Migration(completed: !options.DryRun));
    }

    public string SourceRootOf(string sourcePath) => new MigrationService().SourceRootOf(sourcePath);
}
