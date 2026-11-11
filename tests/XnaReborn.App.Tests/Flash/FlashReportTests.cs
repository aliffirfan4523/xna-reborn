using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Services;
using XnaReborn.App.Tests.Report;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using XnaReborn.Core.Models;

namespace XnaReborn.App.Tests.Flash;

/// <summary>Flash tasks 16.2 and 16.4: the report page for Flash projects, driven through the real analysis service on the fixtures.</summary>
public sealed class FlashReportTests
{
    private static string Fixture(string name) => Path.Combine(RepoPaths.Fixture("flash"), name);

    private static TestServices Create() => TestServices.Create(s => s.AddSingleton<IAnalysisService, AnalysisService>());

    private static async Task<AnalysisReport> AnalyzeAsync(string fixture) =>
        await new AnalysisService().AnalyzeAsync(Fixture(fixture), null, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("flx-hello", "Entry class: HelloGame", "Flixel version: not known")]
    [InlineData("flx-bundled-flixel", "Entry class: BundledGame", "Flixel version: 2.55")]
    [InlineData("flx-platformer", "Entry class: PlatformerGame", "Flixel version: not known")]
    public async Task The_facts_come_from_the_report(string fixture, string entry, string flixel)
    {
        var info = FlashReportInfo.From(await AnalyzeAsync(fixture));

        Assert.NotNull(info);
        Assert.Equal(entry, info.EntryClassText);
        Assert.StartsWith(flixel, info.FlixelText, StringComparison.Ordinal);
        Assert.Contains("Flash", info.KindText, StringComparison.Ordinal);
        Assert.Null(info.Explanation);
        Assert.True(info.CanConvert);
    }

    [Fact]
    public async Task A_swf_only_folder_gets_the_do_not_decompile_explanation()
    {
        var info = FlashReportInfo.From(await AnalyzeAsync("flx-swf-only"));

        Assert.NotNull(info);
        Assert.False(info.CanConvert);
        Assert.Equal("Entry class: not found", info.EntryClassText);
        Assert.Contains(".swf", info.Explanation, StringComparison.Ordinal);
        Assert.Contains("does not decompile", info.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_flash_pro_only_folder_says_the_timeline_cannot_be_read()
    {
        var info = FlashReportInfo.From(await AnalyzeAsync("flx-fla-only"));

        Assert.NotNull(info);
        Assert.False(info.CanConvert);
        Assert.Contains(".fla", info.Explanation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("flx-plain-as3")]
    [InlineData("flx-two-entries")]
    public async Task A_folder_without_exactly_one_entry_class_cannot_be_converted(string fixture)
    {
        var info = FlashReportInfo.From(await AnalyzeAsync(fixture));

        Assert.NotNull(info);
        Assert.False(info.CanConvert);
        Assert.Contains("exactly one class that extends FlxGame", info.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_that_are_not_flash_reports_have_no_flash_facts()
    {
        Assert.Null(FlashReportInfo.From(null));
        Assert.Null(FlashReportInfo.From(ReportTestData.Small()));
    }

    [AvaloniaFact]
    public async Task Opening_a_flash_folder_shows_the_kind_the_entry_class_and_the_fl_findings()
    {
        using var t = Create();
        var window = new MainWindow { DataContext = t.Main, Height = 3000 };
        window.Show();

        var opened = await t.Get<OpenProjectViewModel>().OpenPathAsync(Fixture("flx-hello"));
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.True(opened);
        Assert.Equal(WorkflowStep.Report, t.Main.CurrentStep);
        var view = window.GetVisualDescendants().OfType<ReportView>().Single();
        Assert.True(Find<Expander>(view, "FlashPanel").IsVisible);
        Assert.Equal("Entry class: HelloGame", Find<TextBlock>(view, "FlashEntryClass").Text);
        Assert.Contains("Flash", Find<TextBlock>(view, "FlashKind").Text, StringComparison.Ordinal);
        Assert.StartsWith("Flixel version:", Find<TextBlock>(view, "FlashFlixelVersion").Text, StringComparison.Ordinal);
        Assert.False(Find<Border>(view, "FlashExplanation").IsVisible);
        Assert.False(Find<Expander>(view, "ProjectsPanel").IsVisible);
        Assert.True(Find<Button>(view, "ContinueButton").Command!.CanExecute(null));
        var rules = t.Get<ReportViewModel>().Rows.Select(r => r.Item.RuleId).ToList();
        Assert.Contains("FL001", rules);
        Assert.All(rules, r => Assert.StartsWith("FL", r, StringComparison.Ordinal));
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_swf_only_folder_shows_the_explanation_and_cannot_continue()
    {
        using var t = Create();
        var window = new MainWindow { DataContext = t.Main, Height = 3000 };
        window.Show();

        await t.Get<OpenProjectViewModel>().OpenPathAsync(Fixture("flx-swf-only"));
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var view = window.GetVisualDescendants().OfType<ReportView>().Single();
        var explanation = Find<Border>(view, "FlashExplanation");
        Assert.True(explanation.IsVisible);
        Assert.Contains("does not decompile", explanation.GetVisualDescendants().OfType<TextBlock>().Single().Text, StringComparison.Ordinal);
        Assert.False(Find<Button>(view, "ContinueButton").Command!.CanExecute(null));
        Assert.Contains(t.Get<ReportViewModel>().Rows, r => r.Item.RuleId == "FL003");
        window.Close();
    }

    [AvaloniaFact]
    public async Task An_xna_report_shows_no_flash_panel_and_can_continue()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var window = new MainWindow { DataContext = t.Main, Height = 3000 };
        window.Show();
        await t.OpenPageAsync(WorkflowStep.Report);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var view = window.GetVisualDescendants().OfType<ReportView>().Single();
        Assert.False(Find<Expander>(view, "FlashPanel").IsVisible);
        Assert.True(Find<Button>(view, "ContinueButton").Command!.CanExecute(null));
        window.Close();
    }

    private static T Find<T>(Control root, string automationId)
        where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => AutomationProperties.GetAutomationId(c) == automationId);
}
