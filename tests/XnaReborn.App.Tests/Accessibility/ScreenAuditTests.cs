using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using XnaReborn.App.Services;
using XnaReborn.App.Tests.BuildRun;
using XnaReborn.App.Tests.Diff;
using XnaReborn.App.Tests.Report;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using XnaReborn.Migrator;
using XnaReborn.Runner.Hints;

namespace XnaReborn.App.Tests.Accessibility;

/// <summary>
/// Accessibility audit of every screen (and the shell, the error banner and the dialog): a control a person can operate
/// has a name, is big enough, shows keyboard focus, and icons never speak for themselves. Each page is opened in the
/// states that show the most controls.
/// </summary>
public sealed class ScreenAuditTests
{
    [AvaloniaFact]
    public async Task Open_screen_passes_the_audit()
    {
        using var t = TestServices.Create();
        t.Recent.Items.Add(new RecentProject("C:/projects/Old", DateTimeOffset.UtcNow));
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Open);

        AssertClean(window, "Open");
    }

    [AvaloniaFact]
    public async Task Report_screen_passes_the_audit_with_a_finding_selected()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Report);
        var vm = t.Get<ReportViewModel>();
        vm.SelectedRow = vm.Rows[0];
        Dispatcher.UIThread.RunJobs();

        AssertClean(window, "Report");
    }

    [AvaloniaFact]
    public async Task Migration_setup_screen_passes_the_audit()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.MigrationSetup);

        AssertClean(window, "Migration setup");
    }

    [AvaloniaFact]
    public async Task Diff_screen_passes_the_audit_after_a_migration()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        var vm = await DiffTestData.OpenMigrationAsync(t);
        vm.SelectedFile = vm.Files[0];
        Dispatcher.UIThread.RunJobs();

        AssertClean(window, "Diff");
    }

    [AvaloniaFact]
    public async Task Diff_screen_passes_the_audit_in_preview_mode()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        var vm = await DiffTestData.OpenDryRunAsync(t);
        vm.SelectedFile = vm.Files[0];
        Dispatcher.UIThread.RunJobs();

        AssertClean(window, "Diff preview");
    }

    [AvaloniaFact]
    public async Task Build_and_run_screen_passes_the_audit_after_a_failed_build()
    {
        using var t = TestServices.Create();
        var group = new DiagnosticGroup(
            BuildRunTestData.GamerServicesHint,
            [BuildRunTestData.Error("CS0234", "The type or namespace 'GamerServices' does not exist")]);
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.FailedBuild(group), problem: "The build failed.");
        var window = Show(t);
        var vm = await BuildRunTestData.OpenAsync(t);
        await vm.BuildCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        AssertClean(window, "Build and run");
    }

    [AvaloniaFact]
    public async Task Summary_screen_passes_the_audit()
    {
        using var t = TestServices.Create();
        t.Session.Report = Fakes.SampleData.Report();
        t.Session.MigrationOptions = new MigrationOptions(BuildRunTestData.OutputFolder, MonoGameTarget.DesktopGL, false, false, new HashSet<string>());
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Summary);

        AssertClean(window, "Summary");
    }

    [AvaloniaFact]
    public async Task Settings_screen_passes_the_audit()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Open);
        await t.Main.OpenSettingsAsync();
        Dispatcher.UIThread.RunJobs();

        AssertClean(window, "Settings");
        Assert.Empty(CheckHeadings(window, "Settings"));
    }

    [AvaloniaFact]
    public async Task Shell_with_an_error_banner_passes_the_audit_and_announces_the_error()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Open);
        t.Session.ShowError("Could not read the project.", "details");
        Dispatcher.UIThread.RunJobs();

        AssertClean(window, "Shell with error");
        var banner = FindById<Avalonia.Controls.Border>(window, "ErrorBanner");
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(banner));
    }

    [AvaloniaFact]
    public async Task Every_screen_has_one_level_1_heading_and_no_level_is_skipped()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        t.Session.MigrationOptions = new MigrationOptions(BuildRunTestData.OutputFolder, MonoGameTarget.DesktopGL, false, false, new HashSet<string>());
        var window = Show(t);
        var problems = new List<string>();

        foreach (var step in new[] { WorkflowStep.Open, WorkflowStep.Report, WorkflowStep.MigrationSetup, WorkflowStep.Summary })
        {
            await t.OpenPageAsync(step);
            problems.AddRange(CheckHeadings(window, step.ToString()));
        }

        var diff = await DiffTestData.OpenMigrationAsync(t);
        diff.SelectedFile = diff.Files[0];
        Dispatcher.UIThread.RunJobs();
        problems.AddRange(CheckHeadings(window, "Diff"));

        await BuildRunTestData.OpenAsync(t);
        Dispatcher.UIThread.RunJobs();
        problems.AddRange(CheckHeadings(window, "BuildRun"));

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static List<string> CheckHeadings(MainWindow window, string page)
    {
        Dispatcher.UIThread.RunJobs();
        var headings = ScreenAudit.Headings(window);
        var problems = new List<string>();
        if (headings.Count(h => h.Level == 1) != 1)
        {
            problems.Add($"{page}: expected one level-1 heading, found [{string.Join(", ", headings.Where(h => h.Level == 1).Select(h => h.Text))}]");
        }

        var previous = 0;
        foreach (var (level, text) in headings)
        {
            if (level > previous + 1)
            {
                problems.Add($"{page}: heading '{text}' is level {level} after level {previous}");
            }

            previous = level;
        }

        return problems;
    }

    private static void AssertClean(MainWindow window, string page)
    {
        var problems = ScreenAudit.Everything(window).Concat(ScreenAudit.StopsWithoutFocusRing(window)).ToList();
        Assert.True(problems.Count == 0, page + ":" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private static T FindById<T>(Avalonia.Visual root, string id)
        where T : Avalonia.Controls.Control =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<T>().Single(c => AutomationProperties.GetAutomationId(c) == id);

    private static MainWindow Show(TestServices t)
    {
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }
}
