using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Services;
using XnaReborn.App.Tests.BuildRun;
using XnaReborn.App.Tests.MigrationSetup;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using XnaReborn.Runner.Environment;

namespace XnaReborn.App.Tests.Cancellation;

/// <summary>
/// Studio UI task 9.2 (R6.2): every long operation shows progress and a Cancel button, the Escape key presses it, and the
/// session is left in a state the user can continue from. Each test starts the operation in a window, checks that progress
/// and Cancel are on screen, presses Escape, and checks what is left.
/// </summary>
public sealed class CancelEveryOperationTests
{
    // A cancel that does not work must fail the test, not hang it.
    private static readonly TimeSpan _limit = TimeSpan.FromSeconds(15);

    [AvaloniaFact]
    public async Task Analysis_shows_progress_and_Escape_cancels_it()
    {
        using var t = TestServices.Create();
        t.Analysis.Gate = new TaskCompletionSource().Task;
        var window = Show(t);
        var vm = t.Get<OpenProjectViewModel>();

        var run = vm.OpenPathAsync(RepoPaths.Fixture("xna40-basic"));
        Dispatcher.UIThread.RunJobs();
        Assert.True(ProgressIsShown(window));
        Assert.True(CancelIsShown(window));

        Escape(window);
        var opened = await run.WaitAsync(_limit);
        Dispatcher.UIThread.RunJobs();

        Assert.False(opened);
        Assert.False(vm.IsBusy);
        Assert.Equal("Analysis canceled.", vm.Message);
        Assert.Null(t.Session.Report);
        Assert.Null(t.Session.SourcePath);
        Assert.Empty(t.Recent.Items);
        Assert.False(ProgressIsShown(window));
        Assert.True(t.Get<OpenProjectViewModel>().BrowseFolderCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task A_preview_shows_progress_and_Escape_cancels_it_without_a_result()
    {
        using var tree = new SetupTree();
        var service = new GuardingMigrationService { Gate = new TaskCompletionSource() };
        using var t = TestServices.Create(s => s.AddSingleton<IMigrationService>(service));
        var vm = await OpenSetupAsync(t, tree);
        var window = Show(t);

        var run = vm.PreviewCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(ProgressIsShown(window));
        Assert.True(CancelIsShown(window));
        Assert.False(vm.PreviewCommand.CanExecute(null));

        Escape(window);
        await run.WaitAsync(_limit);
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsBusy);
        Assert.Equal("The preview was cancelled.", vm.StatusMessage);
        Assert.Null(t.Session.DryRunResult);
        Assert.Null(t.Session.MigrationOptions);
        Assert.False(t.Session.HasError);
        Assert.True(vm.PreviewCommand.CanExecute(null));
        Assert.True(vm.MigrateCommand.CanExecute(null));
        Assert.False(Directory.Exists(tree.Sibling));
    }

    [AvaloniaFact]
    public async Task A_migration_shows_progress_and_Escape_cancels_it_and_says_what_stays_behind()
    {
        using var tree = new SetupTree();
        var service = new GuardingMigrationService { Gate = new TaskCompletionSource() };
        using var t = TestServices.Create(s => s.AddSingleton<IMigrationService>(service));
        var vm = await OpenSetupAsync(t, tree);
        var window = Show(t);

        var run = vm.MigrateCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(ProgressIsShown(window));
        Assert.True(CancelIsShown(window));

        Escape(window);
        await run.WaitAsync(_limit);
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsBusy);
        Assert.Contains("cancelled", vm.StatusMessage);
        Assert.Contains("INCOMPLETE.txt", vm.StatusMessage);
        Assert.Null(t.Session.MigrationResult);
        Assert.Null(t.Session.MigrationOptions);
        Assert.False(t.Session.HasError);
        Assert.Equal(WorkflowStep.MigrationSetup, t.Main.CurrentStep);
        Assert.True(vm.MigrateCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task A_preview_after_a_real_migration_keeps_the_options_of_that_migration()
    {
        using var tree = new SetupTree();
        var service = new GuardingMigrationService();
        using var t = TestServices.Create(s => s.AddSingleton<IMigrationService>(service));
        var vm = await OpenSetupAsync(t, tree);
        await vm.MigrateCommand.ExecuteAsync(null);
        var migratedTo = t.Session.MigrationOptions!.OutputFolder;
        await t.Main.GoToAsync(WorkflowStep.MigrationSetup);

        vm.OutputFolder = Path.Combine(tree.Root, "another-output");
        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.NotNull(t.Session.DryRunResult);
        Assert.Equal(migratedTo, t.Session.MigrationOptions!.OutputFolder);
    }

    [AvaloniaFact]
    public async Task The_environment_check_shows_progress_and_Escape_cancels_it()
    {
        var hold = new TaskCompletionSource<EnvironmentInfo>();
        var script = new ScriptedRunService();
        script.OnCheckEnvironment = async ct => await hold.Task.WaitAsync(ct);
        using var t = TestServices.Create(s => s.AddSingleton<IRunService>(script));
        PrepareBuildRun(t);
        var window = Show(t);

        var opening = t.Main.GoToAsync(WorkflowStep.BuildRun);
        Dispatcher.UIThread.RunJobs();
        var vm = t.Get<BuildRunViewModel>();
        Assert.True(vm.IsBusy);
        Assert.True(ProgressIsShown(window));
        Assert.True(CancelIsShown(window));
        Assert.False(vm.BuildCommand.CanExecute(null));

        Escape(window);
        await opening.WaitAsync(_limit);
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsBusy);
        Assert.Equal("Environment check cancelled.", vm.SdkText);
        Assert.False(t.Session.HasError);
        Assert.False(vm.BuildCommand.CanExecute(null));
        Assert.True(vm.RefreshEnvironmentCommand.CanExecute(null));

        // The user can check again and then build.
        script.OnCheckEnvironment = null;
        await vm.RefreshEnvironmentCommand.ExecuteAsync(null);
        Assert.Equal(".NET SDK 8.0.100: found.", vm.SdkText);
        Assert.True(vm.BuildCommand.CanExecute(null));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_build_or_a_launch_shows_progress_and_Escape_cancels_it_and_starts_nothing(bool launch)
    {
        var started = new TaskCompletionSource();
        var script = new ScriptedRunService();
        script.OnRun = async (_, _, _, output, ct) =>
        {
            output!.Report(new XnaReborn.Runner.Processes.OutputLine("Restoring...", false));
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        };
        using var t = TestServices.Create(s => s.AddSingleton<IRunService>(script));
        var vm = await BuildRunTestData.OpenAsync(t);
        var window = Show(t);

        var run = launch ? vm.BuildAndRunCommand.ExecuteAsync(null) : vm.BuildCommand.ExecuteAsync(null);
        await started.Task;
        Dispatcher.UIThread.RunJobs();
        Assert.True(ProgressIsShown(window));
        Assert.True(CancelIsShown(window));
        Assert.Equal(launch, script.Calls.Single().Launch);

        Escape(window);
        await run.WaitAsync(_limit);
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsBusy);
        Assert.False(vm.IsGameRunning);
        Assert.Equal("Cancelled. Nothing was started.", vm.StatusText);
        Assert.Null(t.Session.RunnerResult);
        Assert.False(t.Session.HasError);
        Assert.True(vm.BuildCommand.CanExecute(null));
        Assert.True(vm.BuildAndRunCommand.CanExecute(null));
        Assert.False(ProgressIsShown(window));
    }

    private static async Task<MigrationSetupViewModel> OpenSetupAsync(TestServices t, SetupTree tree)
    {
        t.Session.SourcePath = tree.Source;
        t.Session.Report = SetupReports.WithRules("XNA022");
        await t.OpenPageAsync(WorkflowStep.MigrationSetup);
        return t.Get<MigrationSetupViewModel>();
    }

    private static void PrepareBuildRun(TestServices t)
    {
        t.Session.SourcePath = RepoPaths.Fixture("xna40-basic");
        t.Session.Report = Fakes.SampleData.Report();
        t.Session.MigrationResult = Fakes.SampleData.Migration(completed: true);
        t.Session.MigrationOptions = new XnaReborn.Migrator.MigrationOptions(
            BuildRunTestData.OutputFolder, XnaReborn.Migrator.MonoGameTarget.DesktopGL, false, false, new HashSet<string>());
    }

    private static MainWindow Show(TestServices t)
    {
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Escape(MainWindow window)
    {
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static bool ProgressIsShown(MainWindow window) =>
        window.GetVisualDescendants().OfType<ProgressBar>().Any(p => p.IsEffectivelyVisible);

    private static bool CancelIsShown(MainWindow window) =>
        window.GetVisualDescendants().OfType<Button>().Any(b => b.Content as string == "Cancel" && b.IsEffectivelyVisible && b.IsEffectivelyEnabled);
}
