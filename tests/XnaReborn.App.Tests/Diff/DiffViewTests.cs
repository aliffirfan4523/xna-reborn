using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.Controls;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using XnaReborn.Migrator.Output;

namespace XnaReborn.App.Tests.Diff;

public sealed class DiffViewTests
{
    [AvaloniaFact]
    public async Task The_page_is_shown_through_the_view_locator_with_the_file_list_and_the_viewer()
    {
        using var t = TestServices.Create();
        var window = await ShowAsync(t, real: true);

        var view = Assert.Single(window.GetVisualDescendants().OfType<DiffView>());
        var files = Find<ListBox>(view, "DiffFiles");
        Assert.Equal(7, files.ItemCount);
        Assert.Equal("Changed files", AutomationProperties.GetName(files));
        var viewer = Find<DiffViewer>(view, "DiffViewer");
        Assert.True(viewer.IsVisible);
        Assert.StartsWith("using A;", viewer.OldPane.Document.Text, StringComparison.Ordinal);
        Assert.Contains("using B;", viewer.NewPane.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_dry_run_shows_the_preview_banner_and_a_button_back_to_setup()
    {
        using var t = TestServices.Create();
        var window = await ShowAsync(t, real: false);

        var banner = Find<Border>(window, "DryRunBanner");
        Assert.True(banner.IsVisible);
        Assert.Contains(banner.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "Preview, nothing written yet");
        var back = Find<Button>(window, "DiffBackToSetup");
        Assert.True(back.IsEffectivelyVisible);
        Assert.False(Find<Button>(window, "DiffContinue").IsEffectivelyEnabled);

        back.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WorkflowStep.MigrationSetup, t.Main.CurrentStep);
    }

    [AvaloniaFact]
    public async Task A_real_migration_hides_the_banner_and_enables_continue()
    {
        using var t = TestServices.Create();
        var window = await ShowAsync(t, real: true);

        Assert.False(Find<Border>(window, "DryRunBanner").IsVisible);
        var next = Find<Button>(window, "DiffContinue");
        Assert.True(next.IsEffectivelyEnabled);

        next.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WorkflowStep.BuildRun, t.Main.CurrentStep);
    }

    [AvaloniaFact]
    public async Task Selecting_a_file_in_the_list_changes_the_diff()
    {
        using var t = TestServices.Create();
        var window = await ShowAsync(t, real: true);
        var vm = t.Get<DiffViewModel>();

        vm.SelectedFile = vm.Files.Single(f => f.Path == "Shim.cs");
        Dispatcher.UIThread.RunJobs();

        var viewer = Find<DiffViewer>(window, "DiffViewer");
        Assert.Equal("class Shim { }", viewer.NewPane.Document.Text);
        Assert.Equal(string.Empty, viewer.OldPane.Document.Text);
    }

    [AvaloniaFact]
    public async Task A_file_without_text_shows_the_placeholder_instead_of_the_viewer()
    {
        using var t = TestServices.Create();
        var window = await ShowAsync(t, real: true);
        var vm = t.Get<DiffViewModel>();

        vm.SelectedFile = vm.Files.Single(f => f.Path == "Content/logo.png");
        Dispatcher.UIThread.RunJobs();

        Assert.False(Find<DiffViewer>(window, "DiffViewer").IsVisible);
        var placeholder = Find<Border>(window, "DiffPlaceholder");
        Assert.True(placeholder.IsVisible);
        Assert.Contains(placeholder.GetVisualDescendants().OfType<TextBlock>(), x => x.Text?.Contains("copied as it is", StringComparison.Ordinal) == true);
    }

    [AvaloniaFact]
    public async Task The_marker_buttons_move_through_the_markers_and_update_the_status()
    {
        using var t = TestServices.Create();
        var window = await ShowAsync(t, real: true);
        var status = Find<TextBlock>(window, "MarkerStatus");
        Assert.Equal("3 manual-work markers", status.Text);

        var next = Find<Button>(window, "NextMarker");
        next.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();
        next.Command.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Marker 2 of 3", status.Text);
        Assert.Equal("Program.cs", t.Get<DiffViewModel>().SelectedFile?.Path);
        Assert.Equal(1, Find<DiffViewer>(window, "DiffViewer").ActiveRow);

        Find<Button>(window, "PreviousMarker").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Marker 1 of 3", status.Text);
        Assert.Equal("Game1.cs", t.Get<DiffViewModel>().SelectedFile?.Path);
    }

    [AvaloniaFact]
    public async Task The_marker_buttons_are_disabled_when_there_are_no_markers()
    {
        using var t = TestServices.Create();
        var window = await ShowAsync(t, real: true, [new FileChange("A.cs", ChangeKind.Modified, "x\n", "y\n", null)]);

        Assert.False(Find<Button>(window, "NextMarker").IsEffectivelyEnabled);
        Assert.False(Find<Button>(window, "PreviousMarker").IsEffectivelyEnabled);
        Assert.Equal("No manual-work markers", Find<TextBlock>(window, "MarkerStatus").Text);
    }

    [AvaloniaFact]
    public async Task The_count_per_kind_is_shown_as_text()
    {
        using var t = TestServices.Create();
        var window = await ShowAsync(t, real: true);

        var counts = Find<ItemsControl>(window, "KindCounts");
        var texts = counts.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text).ToList();
        Assert.Contains("Modified: 2", texts);
        Assert.Contains("New: 2", texts);
        Assert.Contains("Manual work: 1", texts);
        Assert.Contains("Copied: 1", texts);
        Assert.Contains("Skipped: 1", texts);
    }

    [AvaloniaFact]
    public async Task The_unified_switch_shows_one_pane_with_removed_and_added_rows()
    {
        using var t = TestServices.Create();
        var window = await ShowAsync(t, real: true);
        var viewer = Find<DiffViewer>(window, "DiffViewer");
        Assert.True(viewer.OldPane.IsVisible);
        Assert.False(viewer.UnifiedPane.IsVisible);

        Find<Button>(window, "ViewUnified").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewer.UnifiedPane.IsVisible);
        Assert.False(viewer.OldPane.IsVisible);
        Assert.Equal("Unified view, selected", AutomationProperties.GetName(Find<Button>(window, "ViewUnified")));
        Assert.Contains("using A;", viewer.UnifiedPane.Document.Text, StringComparison.Ordinal);
        Assert.Contains("using B;", viewer.UnifiedPane.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_marker_jump_in_unified_mode_outlines_the_marker_row_and_shows_the_callout()
    {
        using var t = TestServices.Create();
        var window = await ShowAsync(t, real: true);
        var vm = t.Get<DiffViewModel>();
        vm.ShowUnifiedCommand.Execute(null);
        Assert.False(Find<XnaReborn.App.Controls.Banner>(window, "MarkerCallout").IsEffectivelyVisible);

        vm.NextMarkerCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var viewer = Find<DiffViewer>(window, "DiffViewer");
        Assert.True(viewer.ActiveRow >= 0);
        Assert.Equal("// XNAREBORN: XNA022: fix by hand", viewer.UnifiedPane.Document.GetText(viewer.UnifiedPane.Document.GetLineByNumber(viewer.ActiveRow + 1)).Trim());
        Assert.True(Find<XnaReborn.App.Controls.Banner>(window, "MarkerCallout").IsEffectivelyVisible);
        Assert.Contains("XNA022", vm.CurrentMarkerText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_kind_filter_limits_the_list_and_a_marker_jump_clears_it()
    {
        using var t = TestServices.Create();
        var window = await ShowAsync(t, real: true);
        var vm = t.Get<DiffViewModel>();
        var files = Find<ListBox>(window, "DiffFiles");

        vm.FilterByKindCommand.Execute(ChangeKind.Created);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, files.ItemCount);
        Assert.Equal("2 of 7 files", vm.FilesSummary);
        Assert.Equal(ChangeKind.Created, vm.SelectedFile?.Kind);
        Assert.Equal(7, vm.Files.Count);

        vm.NextMarkerCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(vm.FilterKind);
        Assert.Equal(7, files.ItemCount);
        Assert.Equal("Game1.cs", vm.SelectedFile?.Path);
    }

    private static async Task<MainWindow> ShowAsync(TestServices t, bool real, IReadOnlyList<FileChange>? changes = null)
    {
        if (real)
        {
            await DiffTestData.OpenMigrationAsync(t, changes);
        }
        else
        {
            await DiffTestData.OpenDryRunAsync(t, changes);
        }

        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Find<T>(Visual root, string automationId)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => AutomationProperties.GetAutomationId(c) == automationId);
}
