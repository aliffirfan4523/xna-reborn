using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models;
using XnaReborn.Flash.Analyzer;
using XnaReborn.Flash.Analyzer.Detection;
using XnaReborn.Flash.Converter;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;

namespace XnaReborn.App.Services;

/// <summary>
/// Lets the existing analysis and migration services handle Flash (AS3 + Flixel) projects. It only maps shapes: the report and the
/// result are turned into the XNA models the screens already show. All analysis and conversion logic stays in the Flash projects.
/// </summary>
internal static class FlashServiceAdapter
{
    /// <summary>The catalog name that marks a report as a Flash report.</summary>
    public const string CatalogName = "flash-to-monogame";

    /// <summary>True when the path is a folder with Flash signals and no .NET project.</summary>
    public static bool IsFlash(string path)
    {
        var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return false;
        }

        return FlashProjectDetector.Detect(new ReadOnlyFileSystem(), Path.GetFullPath(folder)).Kind == ProjectKind.Flash;
    }

    /// <summary>Analyzes a Flash folder and returns the report in the shared shape.</summary>
    public static async Task<AnalysisReport> AnalyzeAsync(string path, IProgress<AnalysisProgress>? progress, CancellationToken cancellationToken)
    {
        var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(Path.GetFullPath(path))!;
        var flash = await FlashProjectAnalyzer.AnalyzeAsync(Path.GetFullPath(folder), null, progress, cancellationToken).ConfigureAwait(false);
        var name = flash.EntryClass ?? new DirectoryInfo(Path.GetFullPath(folder)).Name;
        var project = new ProjectInfo(
            flash.ProjectFile ?? name + ".as3proj",
            name,
            IsXna: false,
            XnaVersion.Unknown,
            XnaPlatform.Windows,
            null,
            null,
            flash.FlixelVersion is null ? [] : ["Flixel " + flash.FlixelVersion],
            [],
            [],
            [],
            [],
            Family: ProjectFamily.Other);
        var byClass = flash.Findings.GroupBy(f => f.Classification).ToDictionary(g => g.Key, g => g.Count());
        var classes = Enum.GetValues<Classification>().ToDictionary(c => c, c => byClass.GetValueOrDefault(c));
        return new AnalysisReport(
            flash.SchemaVersion,
            CatalogName,
            [project],
            flash.Findings,
            flash.TriggeredRules,
            flash.CountsBySeverity,
            classes,
            flash.Score,
            flash.ProjectedScore,
            flash.Diagnostics,
            null);
    }

    // Progress<T> posts to another thread, so the last update could arrive after the call returns. This reports at once.
    private sealed class DirectProgress(Action<FlashMigrationProgress> report) : IProgress<FlashMigrationProgress>
    {
        public void Report(FlashMigrationProgress value) => report(value);
    }

    /// <summary>One plan step for each fixable rule that fired.</summary>
    public static MigrationPlan CreatePlan(AnalysisReport report, IReadOnlySet<string> excluded)
    {
        var items = report.Findings
            .Where(f => f.Classification == Classification.AutoFixable)
            .GroupBy(f => f.RuleId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new PlanItem(g.Key, g.Key, g.First().Title, g.Select(f => f.Location?.File).Where(p => p is not null).Select(p => p!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList()))
            .ToList();
        return new MigrationPlan(items.Where(i => !excluded.Contains(i.Id)).ToList(), items.Where(i => excluded.Contains(i.Id)).ToList());
    }

    /// <summary>Converts the project. The plan is informational: the converter always applies every fix.</summary>
    public static async Task<MigrationResult> MigrateAsync(
        string sourcePath,
        AnalysisReport report,
        MigrationOptions options,
        bool confirmedNonEmpty,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var folder = Directory.Exists(sourcePath) ? sourcePath : Path.GetDirectoryName(Path.GetFullPath(sourcePath))!;
        var plan = CreatePlan(report, options.ExcludedPlanItemIds);
        if (options.DryRun)
        {
            return new MigrationResult(plan, [], new Manifest(1, "flash-0.1", CatalogName, false, [], null), false);
        }

        var mapped = progress is null ? null : new DirectProgress(p => progress.Report(new MigrationProgress(p.Stage, p.Completed, p.Total, p.Current)));
        var result = await FlashMigrator.MigrateAsync(Path.GetFullPath(folder), options.OutputFolder, confirmedNonEmpty, mapped, cancellationToken).ConfigureAwait(false);
        var changes = result.Manifest.Changes.Select(c => new FileChange(c.Path, c.Kind, null, null, c.Reason)).ToList();
        return new MigrationResult(plan, changes, result.Manifest, true);
    }
}
