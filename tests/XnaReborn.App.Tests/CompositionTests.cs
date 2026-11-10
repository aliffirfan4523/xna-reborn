using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Services;
using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.ViewModels;
using XnaReborn.Migrator;
using XnaReborn.Runner.Environment;

namespace XnaReborn.App.Tests;

/// <summary>The seams the screen workers rely on: DI resolution, navigation to every page, fakes reachable from TestServices.</summary>
public sealed class CompositionTests
{
    private static readonly Type[] _pageTypes =
    [
        typeof(OpenProjectViewModel),
        typeof(ReportViewModel),
        typeof(MigrationSetupViewModel),
        typeof(DiffViewModel),
        typeof(BuildRunViewModel),
        typeof(SummaryViewModel),
    ];

    public static TheoryData<Type> PageTypes() => [.. _pageTypes];

    public static TheoryData<WorkflowStep> Steps() => [.. Enum.GetValues<WorkflowStep>()];

    [Fact]
    public void The_real_container_validates_and_resolves_everything()
    {
        using var provider = AppServices.Build(() => null);

        foreach (var type in new[] { typeof(IAnalysisService), typeof(IMigrationService), typeof(IRunService), typeof(IDialogService), typeof(IClipboardService), typeof(IRecentProjectsStore), typeof(WorkflowSession), typeof(MainViewModel) })
        {
            Assert.NotNull(provider.GetRequiredService(type));
        }

        Assert.All(_pageTypes, type => Assert.NotNull(provider.GetRequiredService(type)));
        Assert.Same(provider.GetRequiredService<MainViewModel>(), provider.GetRequiredService<INavigator>());
        Assert.Same(provider.GetRequiredService<WorkflowSession>(), provider.GetRequiredService<MainViewModel>().Session);
    }

    [Theory]
    [MemberData(nameof(PageTypes))]
    public void Every_page_view_model_resolves_from_the_test_container_as_a_singleton(Type pageType)
    {
        using var t = TestServices.Create();

        var page = (PageViewModel)t.Provider.GetRequiredService(pageType);

        Assert.Same(page, t.Provider.GetRequiredService(pageType));
        Assert.False(string.IsNullOrWhiteSpace(page.Title));
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public async Task OpenPageAsync_reaches_every_step_and_shows_the_same_instance_the_container_returns(WorkflowStep step)
    {
        using var t = TestServices.Create();

        await t.OpenPageAsync(step);

        var expected = step switch
        {
            WorkflowStep.Open => typeof(OpenProjectViewModel),
            WorkflowStep.Report => typeof(ReportViewModel),
            WorkflowStep.MigrationSetup => typeof(MigrationSetupViewModel),
            WorkflowStep.Diff => typeof(DiffViewModel),
            WorkflowStep.BuildRun => typeof(BuildRunViewModel),
            _ => typeof(SummaryViewModel),
        };
        Assert.Same(t.Provider.GetRequiredService(expected), t.Main.CurrentPage);
    }

    [AvaloniaTheory]
    [MemberData(nameof(PageTypes))]
    public void Every_page_view_model_has_a_view_the_locator_can_build(Type pageType)
    {
        using var t = TestServices.Create();
        var page = t.Provider.GetRequiredService(pageType);

        var viewType = ViewLocator.ViewTypeFor(pageType);
        var control = new ViewLocator().Build(page);

        Assert.NotNull(viewType);
        Assert.IsAssignableFrom<UserControl>(control);
        Assert.Equal(viewType, control!.GetType());
        Assert.True(new ViewLocator().Match(page));
    }

    [Fact]
    public void Every_fake_is_the_object_behind_its_interface()
    {
        using var t = TestServices.Create();

        Assert.Same(t.Analysis, t.Get<IAnalysisService>());
        Assert.Same(t.Migration, t.Get<IMigrationService>());
        Assert.Same(t.Runner, t.Get<IRunService>());
        Assert.Same(t.Dialogs, t.Get<IDialogService>());
        Assert.Same(t.Recent, t.Get<IRecentProjectsStore>());
        Assert.Same(t.Clipboard, t.Get<IClipboardService>());
    }

    [Fact]
    public void A_test_can_replace_a_fake_through_configure()
    {
        var replacement = new FakeAnalysisService();
        using var t = TestServices.Create(s => s.AddSingleton<IAnalysisService>(replacement));

        Assert.Same(replacement, t.Get<IAnalysisService>());
    }

    [Fact]
    public async Task FakeAnalysisService_records_calls_reports_progress_and_can_fail_or_wait()
    {
        using var t = TestServices.Create();
        var stages = new List<string>();

        var report = await t.Analysis.AnalyzeAsync("C:\\p", new Progress0<Core.Models.AnalysisProgress>(p => stages.Add(p.Stage)), TestContext.Current.CancellationToken);

        Assert.Same(t.Analysis.Report, report);
        Assert.Equal(["C:\\p"], t.Analysis.AnalyzedPaths);
        Assert.Equal(["Reading projects", "Done"], stages);

        t.Analysis.Failure = new InvalidOperationException("nope");
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Analysis.AnalyzeAsync("C:\\p", null, TestContext.Current.CancellationToken));

        t.Analysis.Failure = null;
        t.Analysis.Gate = new TaskCompletionSource().Task;
        using var cts = new CancellationTokenSource();
        var pending = t.Analysis.AnalyzeAsync("C:\\p", null, cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task FakeMigrationService_plans_with_the_real_planner_and_records_runs()
    {
        using var t = TestServices.Create();
        var options = new MigrationOptions("out", MonoGameTarget.DesktopGL, false, true, new HashSet<string>());

        var plan = t.Migration.CreatePlan(SampleData.Report(), new HashSet<string>());
        var dry = await t.Migration.MigrateAsync("C:\\p", SampleData.Report(), options, false, null, TestContext.Current.CancellationToken);
        var real = await t.Migration.MigrateAsync("C:\\p", SampleData.Report(), options with { DryRun = false }, true, null, TestContext.Current.CancellationToken);

        Assert.Empty(plan.Items);
        Assert.False(dry.Completed);
        Assert.True(real.Completed);
        Assert.Equal(2, t.Migration.MigrateCalls.Count);
        Assert.True(t.Migration.MigrateCalls[1].ConfirmedNonEmpty);
        Assert.True(t.Migration.CheckOutput("a", "b", false).Allowed);
    }

    [Fact]
    public async Task FakeRunService_asks_the_confirmation_callback_when_launching()
    {
        using var t = TestServices.Create();
        t.Runner.ConfirmationToAsk = new Runner.Run.LaunchConfirmation("game.dll", "Run it?");
        var asked = new List<string>();

        var result = await t.Runner.BuildAndRunAsync(
            "C:\\out",
            MonoGameTargetKind.DesktopGL,
            launch: true,
            c =>
            {
                asked.Add(c.Message);
                return Task.FromResult(true);
            },
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.Same(t.Runner.Result, result);
        Assert.Equal(["Run it?"], asked);
        Assert.True(t.Runner.ConfirmationAnswer);
        Assert.Equal(("C:\\out", MonoGameTargetKind.DesktopGL, true), t.Runner.RunCalls.Single());
        Assert.True((await t.Runner.CheckEnvironmentAsync(MonoGameTargetKind.DesktopGL, TestContext.Current.CancellationToken)).SdkCompatible);
    }

    [Fact]
    public async Task FakeDialogService_plays_back_scripted_answers_and_records_what_was_shown()
    {
        using var t = TestServices.Create();
        t.Dialogs.FolderToPick = "C:\\picked";
        t.Dialogs.ConfirmAnswers.Enqueue(true);

        Assert.Equal("C:\\picked", await t.Dialogs.PickFolderAsync("Output"));
        Assert.True(await t.Dialogs.ConfirmAsync("T", "M", "Yes", "No"));
        Assert.False(await t.Dialogs.ConfirmAsync("T", "M", "Yes", "No"));
        await t.Dialogs.ShowErrorAsync("E", "msg", "d");
        Assert.Null(await t.Dialogs.PickProjectFileAsync());
        Assert.Null(await t.Dialogs.SaveFileAsync("S", "report", "md"));

        Assert.Equal(["Output"], t.Dialogs.FolderPickerTitles);
        Assert.Equal(2, t.Dialogs.Confirmations.Count);
        Assert.Equal(("E", "msg", "d"), t.Dialogs.Errors.Single());
    }

    [Fact]
    public void FakeRecentProjectsStore_follows_the_same_rules_as_the_real_store()
    {
        using var t = TestServices.Create();

        for (var i = 0; i < 12; i++)
        {
            t.Recent.Add("C:\\p" + i);
        }

        t.Recent.Add("c:\\P11");

        var paths = t.Recent.Load().Select(r => r.Path).ToList();
        Assert.Equal(RecentProjectsStore.MaxItems, paths.Count);
        Assert.Equal("c:\\P11", paths[0]);
        Assert.Single(paths, p => p.Equals("C:\\p11", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(MonoGameTarget.DesktopGL, MonoGameTargetKind.DesktopGL)]
    [InlineData(MonoGameTarget.WindowsDX, MonoGameTargetKind.WindowsDX)]
    public void The_migrator_target_maps_to_the_runner_target(MonoGameTarget target, MonoGameTargetKind expected)
    {
        Assert.Equal(expected, target.ToRunnerKind());
    }

    [Fact]
    public void SourceRootOf_returns_the_folder_of_a_project_or_solution_file_and_a_folder_as_is()
    {
        var folder = Path.Combine(Path.GetTempPath(), "xnareborn-src");
        var service = new MigrationService();

        Assert.Equal(folder, service.SourceRootOf(Path.Combine(folder, "Game.sln")));
        Assert.Equal(folder, service.SourceRootOf(Path.Combine(folder, "Game.CSPROJ")));
        Assert.Equal(folder, service.SourceRootOf(folder));
    }

    private sealed class Progress0<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public Progress0(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value) => _handler(value);
    }
}
