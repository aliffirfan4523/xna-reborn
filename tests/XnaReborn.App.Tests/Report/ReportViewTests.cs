using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.Controls;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using XnaReborn.Core.Models;

namespace XnaReborn.App.Tests.Report;

public sealed class ReportViewTests
{
    [AvaloniaFact]
    public async Task The_page_shows_the_score_the_counts_and_a_list_with_group_headers()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var (window, view) = await ShowAsync(t);

        var gauge = view.GetVisualDescendants().OfType<ScoreGauge>().Single();
        Assert.Equal(42, gauge.Score);
        Assert.Equal(77, gauge.ProjectedScore);
        Assert.Equal("Auto-fixable: 3", TextOf(view, "CountAutoFixable"));
        Assert.Equal("Manual: 2", TextOf(view, "CountManual"));
        Assert.Equal("Unsupported: 1", TextOf(view, "CountUnsupported"));

        var list = Find<ListBox>(view, "FindingsList");
        Assert.Equal(6, list.ItemCount);
        Assert.Equal(6, list.GetVisualDescendants().OfType<ListBoxItem>().Count());
        var texts = list.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text).ToList();
        Assert.Contains("Auto-fixable (3)", texts);
        Assert.Contains("Manual (2)", texts);
        Assert.Contains("Unsupported (1)", texts);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Every_row_has_a_severity_badge_with_an_icon_and_a_label_and_an_accessible_name()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var (window, view) = await ShowAsync(t);
        var list = Find<ListBox>(view, "FindingsList");

        var badges = list.GetVisualDescendants().OfType<SeverityBadge>().ToList();

        Assert.Equal(6, badges.Count);
        Assert.Equal(["Error", "Error", "Warning", "Error", "Info", "Blocker"], badges.Select(b => b.GetVisualDescendants().OfType<TextBlock>().ElementAt(1).Text));
        Assert.All(badges, b => Assert.False(string.IsNullOrWhiteSpace(b.GetVisualDescendants().OfType<TextBlock>().First().Text)));
        Assert.All(badges, b => Assert.StartsWith("Severity: ", AutomationProperties.GetName(b), StringComparison.Ordinal));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Selecting_a_finding_shows_its_explanation_resolution_location_and_snippet()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var (window, view) = await ShowAsync(t);
        var vm = t.Get<ReportViewModel>();
        Assert.True(Find<TextBlock>(view, "SelectionHint").IsVisible);

        Find<ListBox>(view, "FindingsList").SelectedItem = vm.Rows.Single(r => r.Item.RuleId == "XNA022");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("XNA022", vm.SelectedFinding?.RuleId);
        Assert.False(Find<TextBlock>(view, "SelectionHint").IsVisible);
        var details = Find<ContentControl>(view, "FindingDetails");
        var texts = details.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text).ToList();
        Assert.Contains("SpriteBlendMode was replaced by BlendState.", texts);
        Assert.Contains("Pass BlendState.AlphaBlend.", texts);
        Assert.Contains("Game1.cs:44", texts);
        Assert.Equal("spriteBatch.Begin(SpriteBlendMode.AlphaBlend);", details.GetVisualDescendants().OfType<SelectableTextBlock>().Single().Text);
        Assert.Single(details.GetVisualDescendants().OfType<SeverityBadge>());
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_finding_without_a_snippet_shows_no_code_block()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var (window, view) = await ShowAsync(t);
        var vm = t.Get<ReportViewModel>();

        vm.SelectedRow = vm.Rows.Single(r => r.Item.RuleId == "XNA004");
        Dispatcher.UIThread.RunJobs();

        var snippet = Find<ContentControl>(view, "FindingDetails").GetVisualDescendants().OfType<SelectableTextBlock>().Single();
        Assert.False(snippet.GetVisualAncestors().OfType<StackPanel>().First().IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Typing_in_the_search_box_and_choosing_filters_narrows_the_list()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var (window, view) = await ShowAsync(t);
        var vm = t.Get<ReportViewModel>();
        var list = Find<ListBox>(view, "FindingsList");

        Find<TextBox>(view, "SearchBox").Text = "game1.cs";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, list.ItemCount);
        Assert.Equal("Showing 2 of 6 findings", TextOf(view, "FilterSummary"));

        Find<ListBox>(view, "SeverityFilter").SelectedItem = vm.SeverityChoices.Single(c => c.Value == Severity.Warning);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, list.ItemCount);
        Assert.True(Find<TextBlock>(view, "NoMatchesMessage").IsVisible);

        Find<Button>(view, "ClearFilters").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(6, list.ItemCount);
        Assert.False(Find<TextBlock>(view, "NoMatchesMessage").IsVisible);
        Assert.Equal("", Find<TextBox>(view, "SearchBox").Text);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_classification_filter_is_a_keyboard_reachable_control_with_an_accessible_name()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var (window, view) = await ShowAsync(t);

        var combo = Find<ListBox>(view, "ClassificationFilter");
        combo.SelectedItem = t.Get<ReportViewModel>().ClassificationChoices.Single(c => c.Value == Classification.Unsupported);
        Dispatcher.UIThread.RunJobs();

        Assert.True(combo.GetVisualDescendants().OfType<ListBoxItem>().All(i => i.Focusable));
        Assert.Equal("Filter by classification", AutomationProperties.GetName(combo));
        Assert.Equal(1, Find<ListBox>(view, "FindingsList").ItemCount);
        foreach (var id in new[] { "ExportMarkdown", "ExportJson", "CopyReport", "ContinueButton", "ClearFilters" })
        {
            var button = Find<Button>(view, id);
            Assert.True(button.Focusable);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task The_export_buttons_save_the_report_through_the_save_dialog()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var (window, view) = await ShowAsync(t);
        var path = Path.Combine(Path.GetTempPath(), "xnareborn-view-" + Guid.NewGuid().ToString("N") + ".md");
        t.Dialogs.SavePathToReturn = path;
        try
        {
            var command = Find<Button>(view, "ExportMarkdown").Command!;
            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)command).ExecuteAsync(null);

            Assert.Equal("# Fake report, score 42", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(path, TextOf(view, "StatusMessage"), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_continue_button_goes_to_the_migration_setup_page()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var (window, view) = await ShowAsync(t);
        var button = Find<Button>(view, "ContinueButton");
        Assert.True(button.IsEffectivelyEnabled);

        button.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WorkflowStep.MigrationSetup, t.Main.CurrentStep);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_report_without_an_XNA_project_shows_the_reason_and_no_score_or_list_and_disables_continue()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small(noProjectReason: "No .csproj with an XNA reference was found.");
        var (window, view) = await ShowAsync(t);

        var panel = Find<Border>(view, "NoProjectPanel");
        Assert.True(panel.IsVisible);
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "No .csproj with an XNA reference was found.");
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "No XNA project found");
        Assert.False(view.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "DashScroll").IsVisible);
        Assert.False(Find<Button>(view, "ContinueButton").IsEffectivelyEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Diagnostics_are_listed_only_when_the_analysis_reported_some()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var (window, view) = await ShowAsync(t);
        Assert.False(Find<Expander>(view, "DiagnosticsPanel").IsVisible);

        t.Session.Report = ReportTestData.Small(diagnostics: [new Diagnostic("ANA002", "Could not read a file.", "Game.csproj")]);
        Dispatcher.UIThread.RunJobs();

        var panel = Find<Expander>(view, "DiagnosticsPanel");
        Assert.True(panel.IsVisible);
        var texts = panel.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text).ToList();
        Assert.Contains("ANA002", texts);
        Assert.Contains("Could not read a file.", texts);
        Assert.Contains("Game.csproj", texts);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_project_without_findings_says_so()
    {
        using var t = TestServices.Create();
        t.Session.Report = Fakes.SampleData.Report(score: 100, projectedScore: 100);
        var (window, view) = await ShowAsync(t);

        Assert.True(Find<TextBlock>(view, "NoFindingsMessage").IsVisible);
        Assert.False(Find<TextBlock>(view, "NoMatchesMessage").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_real_report_of_the_xna31_legacy_fixture_renders_and_shows_a_selected_snippet()
    {
        var real = await ReportTestData.RealLegacyAsync();
        using var t = TestServices.Create();
        t.Session.Report = real;
        var (window, view) = await ShowAsync(t);
        var vm = t.Get<ReportViewModel>();

        var list = Find<ListBox>(view, "FindingsList");
        Assert.Equal(real.Findings.Count, list.ItemCount);
        Assert.Equal(real.Score, view.GetVisualDescendants().OfType<ScoreGauge>().Single().Score);

        list.SelectedItem = vm.Rows.First(r => r.Item.RuleId == "XNA024");
        Dispatcher.UIThread.RunJobs();

        var details = Find<ContentControl>(view, "FindingDetails");
        Assert.Contains("RenderState", details.GetVisualDescendants().OfType<SelectableTextBlock>().Single().Text, StringComparison.Ordinal);
        Assert.Contains(details.GetVisualDescendants().OfType<TextBlock>(), x => x.Text is { } s && s.StartsWith("Game1.cs:", StringComparison.Ordinal));
        Assert.Contains(vm.SelectedFinding!.Explanation, details.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text));
        window.Close();
    }

    private static async Task<(MainWindow Window, ReportView View)> ShowAsync(TestServices t)
    {
        var window = new MainWindow { DataContext = t.Main, Height = 3000 };
        window.Show();
        await t.OpenPageAsync(WorkflowStep.Report);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return (window, window.GetVisualDescendants().OfType<ReportView>().Single());
    }

    [AvaloniaFact]
    public async Task Severity_counts_add_up_to_the_findings_and_the_tiles_show_them()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var (window, view) = await ShowAsync(t);
        var vm = t.Get<ReportViewModel>();

        Assert.Equal(vm.TotalCount, vm.BlockerCount + vm.ErrorCount + vm.WarningCount + vm.InfoCount);
        var tiles = view.GetVisualDescendants().OfType<StatTile>().ToList();
        Assert.Contains(tiles, x => x.Label == "Blockers" && x.Value == vm.BlockerCount.ToString());
        Assert.Contains(tiles, x => x.Label == "Warnings" && x.Value == vm.WarningCount.ToString());
        window.Close();
    }

    [AvaloniaFact]
    public async Task Choosing_a_filter_chip_narrows_the_list_and_the_empty_state_shows_only_when_nothing_matches()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var (window, view) = await ShowAsync(t);
        var vm = t.Get<ReportViewModel>();

        Assert.False(vm.ShowEmptyList);
        Find<ListBox>(view, "ClassificationFilter").SelectedItem = vm.ClassificationChoices.Single(c => c.Value == Classification.Unsupported);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, Find<ListBox>(view, "FindingsList").ItemCount);

        vm.SearchText = "no finding has this text";
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.ShowEmptyList);
        Assert.True(Find<TextBlock>(view, "NoMatchesMessage").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Without_a_report_the_page_shows_an_empty_state_with_a_way_back()
    {
        using var t = TestServices.Create();
        var (window, view) = await ShowAsync(t);
        t.Session.Report = null;
        Dispatcher.UIThread.RunJobs();

        Assert.True(t.Get<ReportViewModel>().ShowNoReport);
        Assert.True(Find<EmptyState>(view, "NoReportState").IsVisible);
        Assert.True(Find<Button>(view, "ChooseProjectFromEmpty").IsEffectivelyEnabled);
        window.Close();
    }
    private static T Find<T>(Control root, string automationId)
        where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => AutomationProperties.GetAutomationId(c) == automationId);

    private static string? TextOf(Control root, string automationId) => Find<TextBlock>(root, automationId).Text;
}
