using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Services;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using XnaReborn.Migrator;

namespace XnaReborn.App.Tests.Flash;

/// <summary>Flash tasks 16.3 and 16.4: the summary page lists markers by rule with links, and the approximated behaviors.</summary>
public sealed class FlashSummaryTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xnareborn-appflashsummary-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private static string Fixture(string name) => Path.Combine(RepoPaths.Fixture("flash"), name);

    // Runs the real analysis and migration services on a fixture and puts the results into the session, as the earlier pages would.
    private async Task<TestServices> MigratedAsync(string fixture)
    {
        var t = TestServices.Create(s =>
        {
            s.AddSingleton<IAnalysisService, AnalysisService>();
            s.AddSingleton<IMigrationService, MigrationService>();
        });
        var analysis = t.Get<IAnalysisService>();
        var migration = t.Get<IMigrationService>();
        var report = await analysis.AnalyzeAsync(Fixture(fixture), null, TestContext.Current.CancellationToken);
        var output = Path.Combine(_temp, fixture);
        var options = new MigrationOptions(output, MonoGameTarget.DesktopGL, false, false, new HashSet<string>());
        t.Session.SourcePath = Fixture(fixture);
        t.Session.Report = report;
        t.Session.MigrationOptions = options;
        t.Session.MigrationResult = await migration.MigrateAsync(Fixture(fixture), report, options, false, null, TestContext.Current.CancellationToken);
        return t;
    }

    [Fact]
    public async Task Markers_are_grouped_by_rule_with_titles_counts_and_links()
    {
        using var t = await MigratedAsync("flx-gaps");

        await t.Main.GoToAsync(WorkflowStep.Summary);
        var vm = t.Get<SummaryViewModel>();

        Assert.True(vm.IsFlash);
        Assert.True(vm.HasFlashMarkers);
        var manifest = t.Session.MigrationResult!.Manifest;
        Assert.Equal(manifest.Markers!.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)), vm.FlashMarkers.Select(g => (g.RuleId, g.Links.Count)));
        var with = vm.FlashMarkers.Single(g => g.RuleId == "FL020");
        Assert.Equal("FL020 'with' statement: 1 marker", with.Header);
        Assert.EndsWith("GapWith.as, line " + with.Links[0].Line, with.Links[0].Text, StringComparison.Ordinal);
        Assert.Equal(FlashApproximations(), vm.FlashApproximations);
    }

    [Fact]
    public async Task A_link_opens_the_converted_file_in_the_output_folder()
    {
        using var t = await MigratedAsync("flx-gaps");
        await t.Main.GoToAsync(WorkflowStep.Summary);
        var vm = t.Get<SummaryViewModel>();
        var link = vm.FlashMarkers.Single(g => g.RuleId == "FL020").Links[0];

        await vm.OpenMarkerCommand.ExecuteAsync(link);

        var opened = Assert.Single(t.Shell.OpenedFiles);
        Assert.Equal(Path.Combine(_temp, "flx-gaps", link.ConvertedFile), opened);
        Assert.True(File.Exists(opened));
        Assert.Contains("XNAREBORN: FL020", File.ReadAllText(opened), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_conversion_without_markers_says_none_but_still_lists_the_approximations()
    {
        using var t = await MigratedAsync("flx-hello");

        await t.Main.GoToAsync(WorkflowStep.Summary);
        var vm = t.Get<SummaryViewModel>();

        Assert.True(vm.IsFlash);
        Assert.False(vm.HasFlashMarkers);
        Assert.True(vm.HasFlashApproximations);
    }

    [Fact]
    public async Task An_xna_migration_shows_no_flash_sections()
    {
        using var t = TestServices.Create();

        await t.OpenPageAsync(WorkflowStep.Summary);
        var vm = t.Get<SummaryViewModel>();

        Assert.False(vm.IsFlash);
        Assert.Empty(vm.FlashMarkers);
        Assert.Empty(vm.FlashApproximations);
    }

    [AvaloniaFact]
    public async Task The_page_shows_the_marker_links_and_the_approximated_behaviors()
    {
        using var t = await MigratedAsync("flx-gaps");
        var window = new MainWindow { DataContext = t.Main, Height = 3000 };
        window.Show();
        await t.Main.GoToAsync(WorkflowStep.Summary);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var view = window.GetVisualDescendants().OfType<SummaryView>().Single();
        Assert.True(Find<StackPanel>(view, "FlashMarkersPanel").IsVisible);
        Assert.True(Find<StackPanel>(view, "FlashApproximationsPanel").IsVisible);
        var links = Find<StackPanel>(view, "FlashMarkersPanel").GetVisualDescendants().OfType<Button>().ToList();
        Assert.Equal(t.Session.MigrationResult!.Manifest.MarkerSites!.Count, links.Count);
        Assert.All(links, b => Assert.StartsWith("Open the converted file for ", AutomationProperties.GetName(b), StringComparison.Ordinal));
        var approximations = Find<StackPanel>(view, "FlashApproximationsPanel").GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text).ToList();
        Assert.Contains(approximations, x => x is not null && x.Contains("Number becomes double", StringComparison.Ordinal));

        links[0].Command!.Execute(links[0].CommandParameter);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Single(t.Shell.OpenedFiles);
        window.Close();
    }

    [AvaloniaFact]
    public async Task For_an_xna_migration_the_flash_sections_are_hidden()
    {
        using var t = TestServices.Create();
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        await t.OpenPageAsync(WorkflowStep.Summary);
        Dispatcher.UIThread.RunJobs();

        var view = window.GetVisualDescendants().OfType<SummaryView>().Single();
        Assert.False(Find<StackPanel>(view, "FlashMarkersPanel").IsVisible);
        Assert.False(Find<StackPanel>(view, "FlashApproximationsPanel").IsVisible);
        window.Close();
    }

    private static IReadOnlyList<string> FlashApproximations() => XnaReborn.Flash.Converter.FlashMigrator.KnownDifferences;

    private static T Find<T>(Control root, string automationId)
        where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => AutomationProperties.GetAutomationId(c) == automationId);
}
