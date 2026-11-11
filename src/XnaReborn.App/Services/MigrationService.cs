using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;

namespace XnaReborn.App.Services;

/// <summary>Real <see cref="IMigrationService"/>.</summary>
public sealed class MigrationService : IMigrationService
{
    private readonly ProjectMigrator _migrator = new();

    /// <inheritdoc />
    public MigrationPlan CreatePlan(AnalysisReport report, IReadOnlySet<string> excluded) =>
        report.CatalogName == FlashServiceAdapter.CatalogName
            ? FlashServiceAdapter.CreatePlan(report, excluded)
            : new MigrationPlanner().CreatePlan(report, excluded);

    /// <inheritdoc />
    public GuardResult CheckOutput(string sourceRoot, string outputFolder, bool confirmedNonEmpty) =>
        new OutputGuard().Check(sourceRoot, outputFolder, confirmedNonEmpty);

    /// <inheritdoc />
    public Task<MigrationResult> MigrateAsync(
        string sourcePath,
        AnalysisReport report,
        MigrationOptions options,
        bool confirmedNonEmpty,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken) =>
        report.CatalogName == FlashServiceAdapter.CatalogName
            ? FlashServiceAdapter.MigrateAsync(sourcePath, report, options, confirmedNonEmpty, progress, cancellationToken)
            : _migrator.MigrateAsync(sourcePath, report, options, confirmedNonEmpty, progress, cancellationToken);

    /// <inheritdoc />
    public string SourceRootOf(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var full = Path.GetFullPath(sourcePath);
        var extension = Path.GetExtension(full);
        var isFile = extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase);
        return isFile ? Path.GetDirectoryName(full) ?? full : full;
    }
}
