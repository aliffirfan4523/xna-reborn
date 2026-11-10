using System.Security.Cryptography;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Controls;
using XnaReborn.App.Services;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;

namespace XnaReborn.App.Tests.Checkpoints;

/// <summary>
/// Studio UI checkpoints 4 and 8. These use the REAL analyzer, catalog, migrator and (when enabled) runner, with only the
/// dialogs, clipboard and shell faked. The owner also ran the whole flow by hand on Pirate-Treasure-Hunter on 2026-11-11.
/// </summary>
public sealed class CheckpointTests
{
    private const string _integration = "XNAREBORN_INTEGRATION";
    private const string _launch = "XNAREBORN_LAUNCH";

    /// <summary>Task 4: open a fixture through the Open screen view model and the Report page shows what the analyzer found.</summary>
    [AvaloniaFact]
    public async Task Opening_a_fixture_shows_the_report_page_with_the_analyzers_own_score_findings_and_counts()
    {
        var path = RepoPaths.Fixture("xna31-legacy");
        var expected = await new AnalysisService().AnalyzeAsync(path, null, TestContext.Current.CancellationToken);
        using var t = TestServices.Create(s => s.AddSingleton<IAnalysisService>(new AnalysisService()));
        var window = new MainWindow { DataContext = t.Main, Height = 3000 };
        window.Show();

        var opened = await t.Get<OpenProjectViewModel>().OpenPathAsync(path);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.True(opened);
        Assert.Equal(WorkflowStep.Report, t.Main.CurrentStep);
        var view = window.GetVisualDescendants().OfType<ReportView>().Single();
        var gauge = view.GetVisualDescendants().OfType<ScoreGauge>().Single();
        Assert.Equal(expected.Score, gauge.Score);
        Assert.Equal(expected.ProjectedScore, gauge.ProjectedScore);
        Assert.NotEmpty(expected.Findings);
        Assert.Equal(expected.Findings.Count, Find<ListBox>(view, "FindingsList").GetVisualDescendants().OfType<ListBoxItem>().Count());
        Assert.Equal($"Auto-fixable: {Count(expected, Core.Models.Classification.AutoFixable)}", TextOf(view, "CountAutoFixable"));
        Assert.Equal($"Manual: {Count(expected, Core.Models.Classification.Manual)}", TextOf(view, "CountManual"));
        Assert.Equal($"Unsupported: {Count(expected, Core.Models.Classification.Unsupported)}", TextOf(view, "CountUnsupported"));
        window.Close();
    }

    /// <summary>Task 8, offline part: open, analyze and migrate a copy for real. No NuGet, no build.</summary>
    [AvaloniaFact]
    public async Task Open_analyze_and_migrate_a_copy_leaves_the_original_untouched_and_the_summary_shows_both_scores()
    {
        using var temp = new TempFolder();
        var original = RepoPaths.Fixture("xna31-rewritable");
        var before = HashTree(original);
        var copy = CopyFixture(original, temp.Path);
        using var t = TestServices.Create(UseRealServices);
        var window = new MainWindow { DataContext = t.Main, Height = 3000 };
        window.Show();

        await MigrateAsync(t, copy, Path.Combine(temp.Path, "migrated"));
        await t.Main.GoToAsync(WorkflowStep.Summary);
        Dispatcher.UIThread.RunJobs();

        AssertSummary(window, t);
        Assert.Equal(before, HashTree(original));
        Assert.Equal(before, HashTree(copy));
        window.Close();
    }

    /// <summary>Task 8, full flow: also builds the migrated copy (needs NuGet) and, with XNAREBORN_LAUNCH=1, runs the game.</summary>
    [AvaloniaFact]
    public async Task Full_flow_opens_analyzes_migrates_builds_and_optionally_runs()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable(_integration) == "1", $"Integration test: set {_integration}=1 to run it (it restores NuGet packages).");
        var launch = Environment.GetEnvironmentVariable(_launch) == "1";
        using var temp = new TempFolder();
        var original = RepoPaths.Fixture("xna31-rewritable");
        var before = HashTree(original);
        var copy = CopyFixture(original, temp.Path);
        using var t = TestServices.Create(UseRealServices);
        t.Dialogs.DefaultConfirmAnswer = true;
        var window = new MainWindow { DataContext = t.Main, Height = 3000 };
        window.Show();

        await MigrateAsync(t, copy, Path.Combine(temp.Path, "migrated"));
        await t.Main.GoToAsync(WorkflowStep.BuildRun);
        var vm = t.Get<BuildRunViewModel>();
        var command = launch ? (IAsyncRelayCommand)vm.BuildAndRunCommand : vm.BuildCommand;
        Assert.True(command.CanExecute(null), "Build is not available: " + vm.StatusText);
        try
        {
            await command.ExecuteAsync(null);

            Assert.True(vm.HasResult, vm.StatusText);
            Assert.True(t.Session.RunnerResult?.Build?.Succeeded, vm.StatusText);
        }
        finally
        {
            if (vm.StopCommand.CanExecute(null))
            {
                await vm.StopCommand.ExecuteAsync(null);
            }
        }

        await t.Main.GoToAsync(WorkflowStep.Summary);
        Dispatcher.UIThread.RunJobs();
        AssertSummary(window, t);
        Assert.Equal(before, HashTree(original));
        window.Close();
    }

    private static void UseRealServices(IServiceCollection s)
    {
        s.AddSingleton<IAnalysisService>(new AnalysisService());
        s.AddSingleton<IMigrationService, MigrationService>();
        s.AddSingleton<IRunService, RunService>();
    }

    private static async Task MigrateAsync(TestServices t, string source, string output)
    {
        Assert.True(await t.Get<OpenProjectViewModel>().OpenPathAsync(source));
        Assert.Equal(WorkflowStep.Report, t.Main.CurrentStep);
        await t.Main.GoToAsync(WorkflowStep.MigrationSetup);
        var setup = t.Get<MigrationSetupViewModel>();
        setup.OutputFolder = output;
        Assert.True(setup.MigrateCommand.CanExecute(null), setup.OutputMessage);

        await setup.MigrateCommand.ExecuteAsync(null);

        Assert.False(t.Session.HasError, t.Session.ErrorMessage);
        Assert.True(t.Session.MigrationResult?.Completed);
        Assert.True(Directory.Exists(output));
    }

    private static void AssertSummary(MainWindow window, TestServices t)
    {
        Assert.Equal(WorkflowStep.Summary, t.Main.CurrentStep);
        var report = t.Session.Report!;
        var view = window.GetVisualDescendants().OfType<SummaryView>().Single();
        Assert.Equal(report.Score.ToString(System.Globalization.CultureInfo.InvariantCulture), TextOf(view, "BeforeScore"));
        Assert.Equal(report.ProjectedScore.ToString(System.Globalization.CultureInfo.InvariantCulture), TextOf(view, "AfterScore"));
        Assert.True(report.ProjectedScore > report.Score, "The fixture should improve after migration.");
    }

    private static int Count(Core.Models.AnalysisReport report, Core.Models.Classification kind) =>
        report.Findings.Count(f => f.Classification == kind);

    private static string CopyFixture(string source, string tempRoot)
    {
        var copy = Path.Combine(tempRoot, "source");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(copy, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        return copy;
    }

    private static string HashTree(string folder) =>
        string.Join(
            "\n",
            Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => Path.GetRelativePath(folder, f) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)))));

    private static T Find<T>(Control root, string automationId)
        where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => AutomationProperties.GetAutomationId(c) == automationId);

    private static string? TextOf(Control root, string automationId) => Find<TextBlock>(root, automationId).Text;

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xnareborn-checkpoint-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A build server may still hold a file. The folder is in the temp directory and can be removed later.
            }
        }
    }
}
