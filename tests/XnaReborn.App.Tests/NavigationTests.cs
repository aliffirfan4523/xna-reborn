using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.ViewModels;

namespace XnaReborn.App.Tests;

public sealed class NavigationTests
{
    // Prerequisites seen from a fresh session whose current page is Open, so the "back is allowed" rule does not apply.
    [Theory]
    [InlineData(WorkflowStep.Open, false, false, false, false, true)]
    [InlineData(WorkflowStep.Report, false, false, false, false, false)]
    [InlineData(WorkflowStep.Report, true, false, false, false, true)]
    [InlineData(WorkflowStep.MigrationSetup, false, false, false, false, false)]
    [InlineData(WorkflowStep.MigrationSetup, true, false, false, false, true)]
    [InlineData(WorkflowStep.Diff, true, false, false, false, false)]
    [InlineData(WorkflowStep.Diff, true, true, false, false, true)]
    [InlineData(WorkflowStep.Diff, true, false, true, false, true)]
    [InlineData(WorkflowStep.Diff, true, false, true, true, true)]
    [InlineData(WorkflowStep.BuildRun, true, true, false, false, false)]
    [InlineData(WorkflowStep.BuildRun, true, false, true, false, false)]
    [InlineData(WorkflowStep.BuildRun, true, false, true, true, true)]
    [InlineData(WorkflowStep.Summary, true, true, false, false, false)]
    [InlineData(WorkflowStep.Summary, true, false, true, false, true)]
    [InlineData(WorkflowStep.Summary, true, false, true, true, true)]
    public void CanGoTo_follows_the_prerequisites(WorkflowStep step, bool report, bool dryRun, bool migration, bool completed, bool expected)
    {
        using var t = TestServices.Create();
        if (report)
        {
            t.Session.Report = SampleData.Report();
        }

        if (dryRun)
        {
            t.Session.DryRunResult = SampleData.Migration(completed: false);
        }

        if (migration)
        {
            t.Session.MigrationResult = SampleData.Migration(completed);
        }

        Assert.Equal(expected, t.Main.CanGoTo(step));
    }

    [Fact]
    public void GoTo_does_nothing_when_a_prerequisite_is_missing()
    {
        using var t = TestServices.Create();

        t.Main.GoTo(WorkflowStep.Diff);
        t.Main.GoToCommand.Execute(WorkflowStep.Summary);

        Assert.Equal(WorkflowStep.Open, t.Main.CurrentStep);
        Assert.False(t.Main.GoToCommand.CanExecute(WorkflowStep.Diff));
    }

    [Fact]
    public async Task GoTo_moves_to_a_page_whose_prerequisite_exists()
    {
        using var t = TestServices.Create();
        t.Session.Report = SampleData.Report();

        await t.Main.GoToAsync(WorkflowStep.MigrationSetup);

        Assert.Equal(WorkflowStep.MigrationSetup, t.Main.CurrentStep);
        Assert.IsType<MigrationSetupViewModel>(t.Main.CurrentPage);
        Assert.True(t.Main.GoToCommand.CanExecute(WorkflowStep.Report));
    }

    [Fact]
    public async Task Going_back_is_always_allowed_even_when_the_session_was_cleared()
    {
        using var t = TestServices.Create();
        await t.OpenPageAsync(WorkflowStep.Summary);

        t.Session.Report = null;

        foreach (var earlier in Enum.GetValues<WorkflowStep>().Where(s => s <= WorkflowStep.Summary))
        {
            Assert.True(t.Main.CanGoTo(earlier), earlier + " must stay reachable from Summary");
        }

        await t.Main.GoToAsync(WorkflowStep.Open);

        Assert.Equal(WorkflowStep.Open, t.Main.CurrentStep);
        Assert.False(t.Main.CanGoTo(WorkflowStep.Report));
    }

    [Fact]
    public async Task Pages_are_resolved_once_and_kept()
    {
        var resolved = new Dictionary<Type, int>();
        var main = new MainViewModel(
            new WorkflowSession(),
            new FakeClipboardService(),
            new StubServiceProvider(type =>
            {
                resolved[type] = resolved.GetValueOrDefault(type) + 1;
                return new ProbePage(type.Name);
            }),
            new FakeSettingsService());

        await main.GoToAsync(WorkflowStep.Open);
        var first = main.CurrentPage;
        await main.GoToAsync(WorkflowStep.Open);

        Assert.Same(first, main.CurrentPage);
        Assert.Equal(1, resolved[typeof(OpenProjectViewModel)]);
    }

    [Fact]
    public async Task OnNavigatedToAsync_runs_each_time_the_page_becomes_current()
    {
        var pages = new Dictionary<Type, ProbePage>();
        var session = new WorkflowSession { Report = SampleData.Report() };
        var main = new MainViewModel(
            session,
            new FakeClipboardService(),
            new StubServiceProvider(type => pages[type] = new ProbePage(type.Name)),
            new FakeSettingsService());

        await main.GoToAsync(WorkflowStep.Report);
        await main.GoToAsync(WorkflowStep.Open);
        await main.GoToAsync(WorkflowStep.Report);

        Assert.Equal(2, pages[typeof(ReportViewModel)].NavigatedCount);
        Assert.Equal(1, pages[typeof(OpenProjectViewModel)].NavigatedCount);
    }

    [Fact]
    public async Task A_page_that_fails_to_refresh_shows_the_banner_and_the_navigation_still_happens()
    {
        var session = new WorkflowSession { Report = SampleData.Report() };
        var main = new MainViewModel(
            session,
            new FakeClipboardService(),
            new StubServiceProvider(type => new ProbePage(type.Name) { Failure = new InvalidOperationException("boom") }),
            new FakeSettingsService());

        await main.GoToAsync(WorkflowStep.Report);

        Assert.Equal(WorkflowStep.Report, main.CurrentStep);
        Assert.True(main.HasError);
        Assert.Contains("boom", main.ErrorDetails, StringComparison.Ordinal);
    }

    private sealed class ProbePage : PageViewModel
    {
        public ProbePage(string title)
            : base(title, new WorkflowSession(), new NullNavigator(), new FakeDialogService())
        {
        }

        public int NavigatedCount { get; private set; }

        public Exception? Failure { get; init; }

        public override Task OnNavigatedToAsync()
        {
            NavigatedCount++;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private sealed class NullNavigator : INavigator
    {
        public void GoTo(WorkflowStep step)
        {
        }

        public bool CanGoTo(WorkflowStep step) => true;

        public void CloseSettings()
        {
        }
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        private readonly Func<Type, object> _factory;

        public StubServiceProvider(Func<Type, object> factory)
        {
            _factory = factory;
        }

        public object? GetService(Type serviceType) => _factory(serviceType);
    }
}
