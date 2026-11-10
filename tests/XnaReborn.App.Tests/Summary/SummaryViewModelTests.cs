using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.ViewModels;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;
using XnaReborn.Runner.Build;

namespace XnaReborn.App.Tests.Summary;

public sealed class SummaryViewModelTests
{
    private const string _output = @"C:\out\game";

    [Fact]
    public async Task Scores_counts_and_manual_list_come_from_the_report_and_the_manifest()
    {
        using var t = TestServices.Create();
        SeedMigration(t);

        await t.OpenPageAsync(WorkflowStep.Summary);
        var vm = t.Get<SummaryViewModel>();

        Assert.Equal(40, vm.BeforeScore);
        Assert.Equal(90, vm.AfterScore);
        Assert.Equal("40 to 90 (+50)", vm.ScoreText);
        Assert.Equal(2, vm.AutoFixedCount);
        Assert.Equal(2, vm.ManualCount);
        Assert.Equal(1, vm.UnsupportedCount);
        Assert.Equal(
            [("Content/Font.spritefont", "Font swapped"), ("Content/a.wma", "Manual work")],
            vm.ManualItems.Select(i => (i.Path, i.Kind)));
        Assert.True(vm.HasManualItems);
        Assert.Equal(_output, vm.OutputFolder);
    }

    [Fact]
    public async Task Without_a_runner_result_the_build_text_says_not_built()
    {
        using var t = TestServices.Create();
        SeedMigration(t);

        await t.OpenPageAsync(WorkflowStep.Summary);

        Assert.Equal("Not built yet.", t.Get<SummaryViewModel>().BuildText);
    }

    [Fact]
    public async Task A_successful_and_a_failed_build_are_described()
    {
        using var t = TestServices.Create();
        SeedMigration(t);
        var build = new BuildResult(true, TimeSpan.Zero, [], string.Empty, 0, false, false);
        t.Session.RunnerResult = SampleData.Runner() with { Build = build };
        await t.OpenPageAsync(WorkflowStep.Summary);
        Assert.Equal("Build succeeded.", t.Get<SummaryViewModel>().BuildText);

        t.Session.RunnerResult = SampleData.Runner() with { Build = build with { Succeeded = false }, Problem = "The build failed." };
        await t.Main.GoToAsync(WorkflowStep.Summary);

        Assert.Equal("Build did not succeed. The build failed.", t.Get<SummaryViewModel>().BuildText);
    }

    [Fact]
    public async Task No_manual_work_gives_an_empty_list()
    {
        using var t = TestServices.Create();
        await t.OpenPageAsync(WorkflowStep.Summary);

        var vm = t.Get<SummaryViewModel>();

        Assert.False(vm.HasManualItems);
        Assert.Empty(vm.ManualItems);
        Assert.Equal(0, vm.AutoFixedCount);
    }

    [Fact]
    public async Task Open_folder_and_open_manifest_call_the_shell_with_the_output_paths()
    {
        using var t = TestServices.Create();
        SeedMigration(t);
        await t.OpenPageAsync(WorkflowStep.Summary);
        var vm = t.Get<SummaryViewModel>();

        await vm.OpenOutputFolderCommand.ExecuteAsync(null);
        await vm.OpenManifestCommand.ExecuteAsync(null);

        Assert.Equal([_output], t.Shell.OpenedFolders);
        Assert.Equal([Path.Combine(_output, ProjectMigrator.ManifestMarkdownName)], t.Shell.OpenedFiles);
    }

    [Fact]
    public async Task Open_buttons_are_disabled_without_an_output_folder()
    {
        using var t = TestServices.Create();
        await t.OpenPageAsync(WorkflowStep.Summary);
        var vm = t.Get<SummaryViewModel>();

        Assert.False(vm.OpenOutputFolderCommand.CanExecute(null));
        Assert.False(vm.OpenManifestCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_shell_failure_shows_the_error_banner()
    {
        using var t = TestServices.Create();
        SeedMigration(t);
        t.Shell.Failure = new FileNotFoundException("gone");
        await t.OpenPageAsync(WorkflowStep.Summary);

        await t.Get<SummaryViewModel>().OpenOutputFolderCommand.ExecuteAsync(null);

        Assert.True(t.Main.HasError);
        Assert.Contains("gone", t.Main.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Start_over_clears_the_session_and_goes_to_the_open_page()
    {
        using var t = TestServices.Create();
        SeedMigration(t);
        await t.OpenPageAsync(WorkflowStep.Summary);

        t.Get<SummaryViewModel>().StartOverCommand.Execute(null);

        Assert.Null(t.Session.Report);
        Assert.Null(t.Session.SourcePath);
        Assert.Null(t.Session.MigrationResult);
        Assert.Null(t.Session.RunnerResult);
        Assert.Equal(WorkflowStep.Open, t.Main.CurrentStep);
    }

    private static void SeedMigration(TestServices t)
    {
        Finding F(string rule, Classification c) => new(rule, rule, Severity.Warning, c, "a.csproj", null, null);
        t.Session.SourcePath = RepoPaths.Fixture("xna40-basic");
        t.Session.Report = SampleData.Report() with
        {
            Findings =
            [
                F("XNA001", Classification.AutoFixable),
                F("XNA001", Classification.AutoFixable),
                F("XNA002", Classification.AutoFixable),
                F("XNA010", Classification.Manual),
                F("XNA020", Classification.Unsupported),
            ],
        };
        var manifest = new Manifest(
            1,
            "test",
            "test-catalog",
            true,
            [
                new ManifestChange("Content/Font.spritefont", ChangeKind.Modified, [], "Font swapped to Segoe UI."),
                new ManifestChange("Content/a.wma", ChangeKind.ManualWork, [], "Convert the audio."),
                new ManifestChange("Game1.cs", ChangeKind.Modified, ["XNA001"], null),
            ],
            null);
        t.Session.MigrationOptions = new MigrationOptions(_output, MonoGameTarget.DesktopGL, false, false, new HashSet<string>());
        t.Session.MigrationResult = new MigrationResult(
            new MigrationPlan([new PlanItem("p1", "XNA001", "Fix", [])], []),
            [],
            manifest,
            true);
    }
}
