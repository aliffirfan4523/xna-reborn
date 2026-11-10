using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using XnaReborn.Core.Models;

namespace XnaReborn.App.Tests.Report;

public sealed class StartHereTests
{
    private static Finding F(string id, string title, Classification c, Severity s = Severity.Error) =>
        new(id, title, s, c, "Game.csproj", null, null);

    private static IEnumerable<Finding> Many(int n, string id, string title, Classification c, Severity s = Severity.Error) =>
        Enumerable.Range(0, n).Select(_ => F(id, title, c, s));

    private static AnalysisReport Report(IEnumerable<Finding> findings, int score = 10, int projected = 24, string? noProject = null)
    {
        var list = findings.ToList();
        return new AnalysisReport(
            1, "test", [], list, [],
            Enum.GetValues<Severity>().ToDictionary(s => s, s => list.Count(f => f.Severity == s)),
            Enum.GetValues<Classification>().ToDictionary(c => c, c => list.Count(f => f.Classification == c)),
            score, projected, [], noProject);
    }

    [Fact]
    public void Every_step_is_shown_in_order_with_its_counts_and_the_score_change()
    {
        var tip = StartHereTip.From(Report(
            [.. Many(2, "A", "Auto", Classification.AutoFixable), .. Many(3, "U", "Storage", Classification.Unsupported), F("M", "Manual", Classification.Manual)]))!;

        Assert.Equal(
        [
            "1. 2 issues are fixed for you when you migrate (score 10 -> 24).",
            "2. 3 use features MonoGame does not have. Remove or replace them. Most: Storage, 3 places.",
            "3. 1 need a change by hand. Click one to see how.",
            "4. Then migrate and build. Build errors come with hints.",
        ],
        tip.Steps.Select(s => s.NumberedText));
        Assert.Null(tip.BlockerLine);
        Assert.Null(tip.CleanLine);
    }

    [Fact]
    public void A_step_with_a_zero_count_is_skipped_and_the_rest_are_renumbered()
    {
        var tip = StartHereTip.From(Report(Many(4, "M", "Manual", Classification.Manual)))!;

        Assert.Equal(["1. 4 need a change by hand. Click one to see how.", "2. Then migrate and build. Build errors come with hints."], tip.Steps.Select(s => s.NumberedText));
    }

    [Fact]
    public void One_auto_fixable_finding_reads_as_one_issue()
    {
        var tip = StartHereTip.From(Report([F("A", "Auto", Classification.AutoFixable)]))!;

        Assert.StartsWith("1. 1 issue is fixed for you", tip.Steps[0].NumberedText, StringComparison.Ordinal);
    }

    [Fact]
    public void The_top_three_unsupported_rules_are_named_by_count_then_rule_id()
    {
        var tip = StartHereTip.From(Report(
        [
            .. Many(12, "U1", "Storage", Classification.Unsupported),
            .. Many(3, "U3", "Net", Classification.Unsupported),
            .. Many(3, "U2", "Guide", Classification.Unsupported),
            .. Many(1, "U4", "Zune", Classification.Unsupported),
        ]))!;

        Assert.Contains("Most: Storage, 12 places; Guide, 3 places; Net, 3 places.", tip.Steps[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Zune", tip.Steps[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_unsupported_and_manual_steps_are_filter_buttons_and_the_others_are_text()
    {
        var tip = StartHereTip.From(Report(
            [F("A", "Auto", Classification.AutoFixable), F("U", "Unsup", Classification.Unsupported), F("M", "Manual", Classification.Manual)]))!;

        Assert.Equal([null, Classification.Unsupported, Classification.Manual, null], tip.Steps.Select(s => s.FilterTo));
        Assert.All(tip.Steps.Where(s => s.IsButton), s => Assert.False(string.IsNullOrWhiteSpace(s.ButtonLabel)));
    }

    [Fact]
    public void A_blocker_gets_its_own_first_line_naming_each_blocker_once()
    {
        var tip = StartHereTip.From(Report(
        [
            .. Many(2, "G", "GamerServices", Classification.Unsupported, Severity.Blocker),
            F("M", "Manual", Classification.Manual),
        ]))!;

        Assert.True(tip.HasBlocker);
        Assert.StartsWith("Blocker: GamerServices.", tip.BlockerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void A_report_without_findings_shows_one_clean_line_and_no_steps()
    {
        var tip = StartHereTip.From(Report([], 100, 100))!;

        Assert.True(tip.IsClean);
        Assert.Equal("Nothing to fix, go to Migration setup.", tip.CleanLine);
        Assert.Empty(tip.Steps);
        Assert.False(tip.HasBlocker);
    }

    [Fact]
    public void No_report_or_no_project_gives_no_tip()
    {
        Assert.Null(StartHereTip.From(null));
        Assert.Null(StartHereTip.From(Report([], 0, 0, noProject: "No project.")));
    }

    [Fact]
    public void The_view_model_follows_the_session_report_and_the_step_buttons_set_the_classification_filter()
    {
        using var t = TestServices.Create();
        var vm = t.Get<ReportViewModel>();
        Assert.False(vm.HasStartHere);

        t.Session.Report = ReportTestData.Small();
        Assert.True(vm.HasStartHere);

        vm.ShowClassificationCommand.Execute(Classification.Unsupported);
        Assert.Equal(Classification.Unsupported, vm.SelectedClassification.Value);
        Assert.Equal(1, vm.VisibleCount);
        vm.ShowClassificationCommand.Execute(Classification.Manual);
        Assert.Equal(2, vm.VisibleCount);

        t.Session.Report = ReportTestData.Small(noProjectReason: "none");
        Assert.False(vm.HasStartHere);
    }

    [Fact]
    public void Collapsing_the_tip_is_kept_when_the_report_changes()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var vm = t.Get<ReportViewModel>();
        Assert.True(vm.IsStartHereExpanded);

        vm.IsStartHereExpanded = false;
        t.Session.Report = ReportTestData.Small();

        Assert.False(vm.IsStartHereExpanded);
    }

    [AvaloniaFact]
    public async Task The_real_xna31_legacy_report_shows_a_tip_with_working_keyboard_reachable_buttons()
    {
        var real = await ReportTestData.RealLegacyAsync();
        using var t = TestServices.Create();
        t.Session.Report = real;
        var window = new MainWindow { DataContext = t.Main, Height = 3000, Width = 1069 };
        window.Show();
        await t.OpenPageAsync(WorkflowStep.Report);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var view = window.GetVisualDescendants().OfType<ReportView>().Single();
        var vm = t.Get<ReportViewModel>();

        var panel = view.GetVisualDescendants().OfType<Expander>().Single(e => AutomationProperties.GetAutomationId(e) == "StartHerePanel");
        Assert.True(panel.IsVisible);
        Assert.Equal(real.Findings.Count > 0, vm.StartHere!.Steps.Count > 0);
        Assert.NotEmpty(vm.StartHere!.Steps);

        var buttons = panel.GetVisualDescendants().OfType<Button>().Where(b => b.IsVisible && b is not Avalonia.Controls.Primitives.ToggleButton).ToList();
        Assert.Equal(vm.StartHere.Steps.Count(s => s.IsButton), buttons.Count);
        Assert.All(buttons, b =>
        {
            Assert.True(b.Focusable);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(b)));
        });

        if (buttons.Count > 0)
        {
            buttons[0].Command!.Execute(buttons[0].CommandParameter);
            Assert.NotNull(vm.SelectedClassification.Value);
        }

        panel.IsExpanded = false;
        Assert.False(vm.IsStartHereExpanded);
        window.Close();
    }
}
