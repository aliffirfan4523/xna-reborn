using XnaReborn.App.Services;
using XnaReborn.App.Tests.Fakes;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;

namespace XnaReborn.App.Tests.MigrationSetup;

/// <summary>A temp folder with a source project folder inside it. Deleted on dispose.</summary>
public sealed class SetupTree : IDisposable
{
    public SetupTree()
    {
        Root = Path.Combine(Path.GetTempPath(), "xnareborn-setup-" + Guid.NewGuid().ToString("N"));
        Source = Path.Combine(Root, "MyGame");
        Directory.CreateDirectory(Source);
        File.WriteAllText(Path.Combine(Source, "Game.csproj"), "<Project />");
    }

    public string Root { get; }

    public string Source { get; }

    public string Sibling => Path.Combine(Root, "MyGame-monogame");

    /// <summary>Creates a folder next to the source that already holds a file.</summary>
    public string NonEmptyFolder()
    {
        var folder = Path.Combine(Root, "old-output");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "keep.txt"), "keep");
        return folder;
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}

/// <summary>
/// Plans with the real planner, checks folders with the real <see cref="OutputGuard"/> (like the real service), and
/// records runs without writing anything. A refused folder makes <see cref="MigrateAsync"/> throw, as the real migrator does.
/// </summary>
public sealed class GuardingMigrationService : IMigrationService
{
    /// <summary>The runs, in order.</summary>
    public List<(MigrationOptions Options, bool ConfirmedNonEmpty)> Calls { get; } = [];

    /// <summary>When set, a run waits for this before it finishes, and stops when its token is cancelled.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Thrown by a run when set.</summary>
    public Exception? Failure { get; set; }

    public MigrationPlan CreatePlan(AnalysisReport report, IReadOnlySet<string> excluded) =>
        new MigrationPlanner().CreatePlan(report, excluded);

    public GuardResult CheckOutput(string sourceRoot, string outputFolder, bool confirmedNonEmpty) =>
        new OutputGuard().Check(sourceRoot, outputFolder, confirmedNonEmpty);

    public async Task<MigrationResult> MigrateAsync(
        string sourcePath,
        AnalysisReport report,
        MigrationOptions options,
        bool confirmedNonEmpty,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var verdict = CheckOutput(SourceRootOf(sourcePath), options.OutputFolder, confirmedNonEmpty);
        if (!verdict.Allowed)
        {
            throw new OutputGuardException(verdict);
        }

        Calls.Add((options, confirmedNonEmpty));
        if (Failure is not null)
        {
            throw Failure;
        }

        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken);
        }

        return SampleData.Migration(completed: !options.DryRun);
    }

    public string SourceRootOf(string sourcePath) => new MigrationService().SourceRootOf(sourcePath);
}

public static class SetupReports
{
    /// <summary>A report with one auto-fixable finding for each rule id.</summary>
    public static AnalysisReport WithRules(params string[] ruleIds) => SampleData.Report() with
    {
        Findings = [.. ruleIds.Select(id => new Finding(id, "Title " + id, Severity.Warning, Classification.AutoFixable, "Game.csproj", new SourceLocation("Game1.cs", 1, 1), null))],
    };
}
