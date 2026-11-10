using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;

namespace XnaReborn.App.Services;

/// <summary>Plans and runs a migration. Wraps <c>ProjectMigrator</c>, <c>MigrationPlanner</c> and <c>OutputGuard</c>.</summary>
public interface IMigrationService
{
    /// <summary>Builds the plan checklist from a report.</summary>
    /// <param name="report">The analysis report.</param>
    /// <param name="excluded">Ids of plan items the user switched off.</param>
    MigrationPlan CreatePlan(AnalysisReport report, IReadOnlySet<string> excluded);

    /// <summary>Checks an output folder without creating anything.</summary>
    /// <param name="sourceRoot">The source folder, see <see cref="SourceRootOf"/>.</param>
    /// <param name="outputFolder">The chosen output folder.</param>
    /// <param name="confirmedNonEmpty">True when the user agreed to replace a non-empty folder.</param>
    GuardResult CheckOutput(string sourceRoot, string outputFolder, bool confirmedNonEmpty);

    /// <summary>Runs the migration. With <c>DryRun</c> set in the options, nothing is written.</summary>
    /// <param name="sourcePath">The folder, .sln or .csproj that was analyzed.</param>
    /// <param name="report">The report for <paramref name="sourcePath"/>.</param>
    /// <param name="options">What the user chose.</param>
    /// <param name="confirmedNonEmpty">True when the user agreed to replace a non-empty folder.</param>
    /// <param name="progress">Receives progress, or null.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<MigrationResult> MigrateAsync(
        string sourcePath,
        AnalysisReport report,
        MigrationOptions options,
        bool confirmedNonEmpty,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>Returns the source folder for a path: the folder of a .sln or .csproj, or the folder itself.</summary>
    /// <param name="sourcePath">The path the user opened.</param>
    string SourceRootOf(string sourcePath);
}
