using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Services;
using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.ViewModels;

namespace XnaReborn.App.Tests;

/// <summary>
/// A service container with a fake for every service and the real view models. Every screen worker's tests start here:
/// <c>using var t = TestServices.Create();</c> then <c>await t.OpenPageAsync(WorkflowStep.Report);</c>.
/// </summary>
public sealed class TestServices : IDisposable
{
    private readonly ServiceProvider _provider;

    private TestServices(ServiceProvider provider)
    {
        _provider = provider;
    }

    public IServiceProvider Provider => _provider;

    public WorkflowSession Session => Get<WorkflowSession>();

    public MainViewModel Main => Get<MainViewModel>();

    public FakeAnalysisService Analysis => Get<FakeAnalysisService>();

    public FakeMigrationService Migration => Get<FakeMigrationService>();

    public FakeRunService Runner => Get<FakeRunService>();

    public FakeDialogService Dialogs => Get<FakeDialogService>();

    public FakeRecentProjectsStore Recent => Get<FakeRecentProjectsStore>();

    public FakeClipboardService Clipboard => Get<FakeClipboardService>();

    public FakeShellService Shell => Get<FakeShellService>();

    public FakeSettingsService Settings => Get<FakeSettingsService>();

    /// <summary>Builds the container. <paramref name="configure"/> runs last, so a test can replace any registration.</summary>
    public static TestServices Create(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        AddFake<IAnalysisService, FakeAnalysisService>(services);
        AddFake<IMigrationService, FakeMigrationService>(services);
        AddFake<IRunService, FakeRunService>(services);
        AddFake<IDialogService, FakeDialogService>(services);
        AddFake<IRecentProjectsStore, FakeRecentProjectsStore>(services);
        AddFake<IClipboardService, FakeClipboardService>(services);
        AddFake<IShellService, FakeShellService>(services);
        AddFake<ISettingsService, FakeSettingsService>(services);
        AppServices.AddViewModels(services);
        configure?.Invoke(services);
        return new TestServices(services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
    }

    public T Get<T>()
        where T : notnull => _provider.GetRequiredService<T>();

    /// <summary>
    /// Puts into the session what <paramref name="step"/> needs (a report, a dry run, a completed migration) and shows the page.
    /// Completes after the page's <c>OnNavigatedToAsync</c> finished.
    /// </summary>
    public async Task OpenPageAsync(WorkflowStep step)
    {
        if (step >= WorkflowStep.Report)
        {
            Session.SourcePath ??= RepoPaths.Fixture("xna40-basic");
            Session.Report ??= SampleData.Report();
        }

        if (step == WorkflowStep.Diff)
        {
            Session.DryRunResult ??= SampleData.Migration(completed: false);
        }

        if (step >= WorkflowStep.BuildRun)
        {
            Session.MigrationResult ??= SampleData.Migration(completed: true);
        }

        await Main.GoToAsync(step);
        Assert.Equal(step, Main.CurrentStep);
    }

    public void Dispose() => _provider.Dispose();

    private static void AddFake<TInterface, TFake>(IServiceCollection services)
        where TInterface : class
        where TFake : class, TInterface
    {
        services.AddSingleton<TFake>();
        services.AddSingleton<TInterface>(p => p.GetRequiredService<TFake>());
    }
}
