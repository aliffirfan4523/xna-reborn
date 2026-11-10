using System.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Services;
using XnaReborn.App.Tests.BuildRun;
using XnaReborn.App.Tests.MigrationSetup;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;

namespace XnaReborn.App.Tests.Shutdown;

/// <summary>
/// Studio UI task 9.2 and PR 52 follow-up: closing the main window cancels running work and stops a game that was started
/// from the Build and run page. The "game" is a harmless ping or sleep process started through the real launcher.
/// </summary>
public sealed class WindowCloseTests
{
    // A cancel that does not work must fail the test, not hang it.
    private static readonly TimeSpan _limit = TimeSpan.FromSeconds(15);

    [AvaloniaFact]
    public async Task Closing_the_window_stops_a_running_game()
    {
        var game = await BuildRunTestData.StartIdleProcessAsync();
        if (game is null)
        {
            Assert.Skip("No ping or sleep program on this machine.");
            return;
        }

        try
        {
            using var t = TestServices.Create();
            t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.SucceededBuild(), running: game);
            var vm = await BuildRunTestData.OpenAsync(t);
            var window = Show(t);
            await vm.BuildAndRunCommand.ExecuteAsync(null);
            Assert.True(vm.IsGameRunning);
            Assert.False(game.HasExited);

            window.Close();

            Assert.True(await WaitUntil(() => game.HasExited && !window.IsVisible), "The game or the window was still there.");
            Assert.False(vm.IsGameRunning);
        }
        finally
        {
            await game.StopAsync();
        }
    }

    [AvaloniaFact]
    public async Task Closing_the_window_cancels_a_running_build_and_waits_until_it_has_stopped()
    {
        var started = new TaskCompletionSource();
        var stopped = new TaskCompletionSource();
        var script = new ScriptedRunService();
        script.OnRun = async (_, _, _, _, ct) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            finally
            {
                stopped.SetResult();
            }

            throw new InvalidOperationException("unreachable");
        };
        using var t = TestServices.Create(s => s.AddSingleton<IRunService>(script));
        var vm = await BuildRunTestData.OpenAsync(t);
        var window = Show(t);
        var run = vm.BuildCommand.ExecuteAsync(null);
        await started.Task;

        window.Close();

        Assert.True(await WaitUntil(() => !window.IsVisible), "The window did not close.");
        Assert.True(stopped.Task.IsCompleted, "The window closed before the build had stopped.");
        await run.WaitAsync(_limit);
        Assert.False(vm.IsBusy);
    }

    [AvaloniaFact]
    public async Task A_game_that_a_cancelled_run_still_hands_back_is_stopped_when_the_window_closes()
    {
        var game = await BuildRunTestData.StartIdleProcessAsync();
        if (game is null)
        {
            Assert.Skip("No ping or sleep program on this machine.");
            return;
        }

        try
        {
            var started = new TaskCompletionSource();
            var script = new ScriptedRunService();
            script.OnRun = async (_, _, _, _, ct) =>
            {
                started.SetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    // A runner that already had a started game when the cancel arrived hands it back instead of throwing.
                }

                return BuildRunTestData.Result(BuildRunTestData.SucceededBuild(), running: game);
            };
            using var t = TestServices.Create(s => s.AddSingleton<IRunService>(script));
            t.Dialogs.ConfirmAnswers.Enqueue(true);
            var vm = await BuildRunTestData.OpenAsync(t);
            var window = Show(t);
            var run = vm.BuildAndRunCommand.ExecuteAsync(null);
            await started.Task;

            window.Close();

            Assert.True(await WaitUntil(() => game.HasExited && !window.IsVisible), "The game or the window was still there.");
            await run.WaitAsync(_limit);
            Assert.False(vm.IsGameRunning);
            Assert.Null(t.Session.RunnerResult!.RunningGame);
        }
        finally
        {
            await game.StopAsync();
        }
    }

    [AvaloniaFact]
    public async Task Closing_the_window_cancels_a_running_analysis()
    {
        using var t = TestServices.Create();
        t.Analysis.Gate = new TaskCompletionSource().Task;
        var window = Show(t);
        var vm = t.Get<OpenProjectViewModel>();
        var run = vm.OpenPathAsync(RepoPaths.Fixture("xna40-basic"));
        Assert.True(vm.IsBusy);

        window.Close();

        Assert.False(await run.WaitAsync(_limit));
        Assert.Null(t.Session.Report);
        Assert.False(vm.IsBusy);
        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public async Task Closing_the_window_cancels_a_running_migration()
    {
        using var tree = new SetupTree();
        var service = new GuardingMigrationService { Gate = new TaskCompletionSource() };
        using var t = TestServices.Create(s => s.AddSingleton<IMigrationService>(service));
        t.Session.SourcePath = tree.Source;
        t.Session.Report = SetupReports.WithRules("XNA022");
        await t.OpenPageAsync(WorkflowStep.MigrationSetup);
        var vm = t.Get<MigrationSetupViewModel>();
        var window = Show(t);
        var run = vm.MigrateCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);

        window.Close();
        await run.WaitAsync(_limit);

        Assert.False(vm.IsBusy);
        Assert.Null(t.Session.MigrationResult);
        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public async Task With_nothing_running_the_window_closes_at_once()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Open);

        window.Close();

        Assert.False(window.IsVisible);
    }

    private static MainWindow Show(TestServices t)
    {
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static async Task<bool> WaitUntil(Func<bool> done)
    {
        var clock = Stopwatch.StartNew();
        while (!done())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(15))
            {
                return false;
            }

            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        return true;
    }
}
