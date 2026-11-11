using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.Controls;
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
/// Studio UI task 9.4 (R8.2): each page can be driven with the Tab key alone, in a sensible order, and every control a user
/// can operate has an accessible name. Tab order is read by pressing the real Tab key in a headless window.
/// </summary>
public sealed class KeyboardAccessTests
{
    [AvaloniaFact]
    public async Task Open_page_tab_stops_are_the_browse_buttons_then_the_recent_projects()
    {
        using var t = TestServices.Create();
        t.Recent.Items.Add(new RecentProject("C:/projects/Old", DateTimeOffset.UtcNow));
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Open);

        Assert.Equal(["BrowseFolder", "BrowseFile", "Open C:/projects/Old"], PageStops(window));
        AssertAllNamed(window);
    }

    [AvaloniaFact]
    public async Task Report_page_tab_stops_follow_the_reading_order()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Report);
        var vm = t.Get<ReportViewModel>();
        vm.SelectedRow = vm.Rows[0];
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ["ExportMarkdown", "ExportJson", "CopyReport", "ContinueButton", "StartHerePanel", "Show unsupported findings", "Show manual findings", "SearchBox", "ListBoxItem", "ListBoxItem", "ListBoxItem", "CopyFinding", "Snippet"],
            PageStops(window));
        AssertAllNamed(window);
    }

    [AvaloniaFact]
    public async Task Migration_setup_tab_stops_follow_the_form_from_top_to_bottom()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.MigrationSetup);

        Assert.Equal(
            ["OutputFolderBox", "BrowseButton", "TargetDesktopGL", "TargetWindowsDX", "ShimsToggle", "FontSubstitutionToggle", "ResizeToggle", "FullscreenToggle", "HighDpiToggle", "PlanItemCheck", "PreviewButton", "MigrateButton"],
            PageStops(window));
        AssertAllNamed(window);
    }

    [AvaloniaFact]
    public async Task Diff_page_tab_stops_pass_through_both_text_panes_without_trapping_the_keyboard()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        var vm = await DiffTestData.OpenMigrationAsync(t);
        vm.SelectedFile = vm.Files[0];
        Dispatcher.UIThread.RunJobs();

        // The text panes swallow Tab by default; the order must go on past the first pane to the second and to the buttons.
        Assert.Equal(
            ["PreviousMarker", "NextMarker", "ListBoxItem", "Resize file list", "DiffOldEditor", "Resize panes", "DiffNewEditor", "DiffBack", "DiffContinue"],
            PageStops(window));
        AssertAllNamed(window);
    }

    [AvaloniaFact]
    public async Task Diff_preview_page_starts_with_the_button_back_to_setup()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        var vm = await DiffTestData.OpenDryRunAsync(t);
        vm.SelectedFile = vm.Files[0];
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ["DiffBackToSetup", "PreviousMarker", "NextMarker", "ListBoxItem", "Resize file list", "DiffOldEditor", "Resize panes", "DiffNewEditor"],
            PageStops(window));
        AssertAllNamed(window);
    }

    [AvaloniaFact]
    public async Task Shift_Tab_goes_back_out_of_a_text_pane()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        var vm = await DiffTestData.OpenMigrationAsync(t);
        vm.SelectedFile = vm.Files[0];
        Dispatcher.UIThread.RunJobs();
        var viewer = window.GetVisualDescendants().OfType<DiffViewer>().Single();

        viewer.OldPane.TextArea.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();

        var focused = Assert.IsAssignableFrom<Control>(window.FocusManager!.GetFocusedElement());
        Assert.Equal("Resize file list", AccessibilityProbe.Label(focused));
    }

    [AvaloniaFact]
    public async Task Build_and_run_tab_stops_cover_the_buttons_the_build_log_the_problems_and_the_game_output()
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

        Assert.Equal(
            ["RecheckButton", "BuildButton", "BuildAndRunButton", "SummaryButton", "PublishPlatform", "PublishButton", "Build log lines", "GamerServices no longer exists, 1 error", "SelectableTextBlock"],
            PageStops(window));
        AssertAllNamed(window);
    }

    [AvaloniaFact]
    public async Task Summary_page_tab_stops_are_the_three_buttons()
    {
        using var t = TestServices.Create();
        t.Session.Report = Fakes.SampleData.Report();
        t.Session.MigrationOptions = new MigrationOptions(BuildRunTestData.OutputFolder, MonoGameTarget.DesktopGL, false, false, new HashSet<string>());
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Summary);

        Assert.Equal(["OpenOutputFolder", "OpenManifest", "StartOver"], PageStops(window));
        AssertAllNamed(window);
    }

    [AvaloniaFact]
    public async Task The_error_banner_buttons_are_tab_stops_and_named_when_an_error_is_shown()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Open);
        t.Session.ShowError("Could not read the project.", "details");
        Dispatcher.UIThread.RunJobs();

        var labels = AccessibilityProbe.TabOrder(window).Select(AccessibilityProbe.Label).ToList();

        // The banner comes after the stepper and before the page, so the error is the first thing a keyboard user reaches.
        Assert.Equal(["ErrorDetailsExpander", "ErrorCopy", "ErrorDismiss", "BrowseFolder"], labels.Where(IsShellStop).Take(4));
        AssertAllNamed(window);
    }

    [AvaloniaFact]
    public void The_message_dialog_names_its_buttons_and_Escape_picks_the_cancel_button()
    {
        var window = new MessageWindow("Run this game?", "It will run.", null, "Run game", "Don't run");
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Run game", AutomationProperties.GetName(window.FindControl<Button>("ConfirmButton")!));
        Assert.Equal("Don't run", AutomationProperties.GetName(window.FindControl<Button>("CancelButton")!));

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.IsVisible);
    }

    private static MainWindow Show(TestServices t)
    {
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>The navigation rail (step buttons and the theme toggle) is not part of a page, so page checks skip it.</summary>
    private static bool IsShellStop(string label) =>
        !label.StartsWith("Step_", StringComparison.Ordinal) && label != "ThemeToggle";

    /// <summary>The tab stops of the page: everything Tab visits except the navigation rail.</summary>
    private static List<string> PageStops(MainWindow window) =>
        AccessibilityProbe.TabOrder(window)
            .Select(AccessibilityProbe.Label)
            .Where(IsShellStop)
            .ToList();

    private static void AssertAllNamed(MainWindow window)
    {
        var unnamed = AccessibilityProbe.InteractiveControls(window)
            .Where(c => string.IsNullOrWhiteSpace(AutomationProperties.GetName(c)))
            .Select(AccessibilityProbe.Describe)
            .ToList();
        Assert.True(unnamed.Count == 0, "Controls without an AutomationProperties.Name: " + string.Join("; ", unnamed));
    }
}
