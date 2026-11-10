using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Services;
using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.ViewModels;
using XnaReborn.Migrator;
using XnaReborn.Runner;
using XnaReborn.Runner.Diagnostics;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Hints;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Run;

namespace XnaReborn.App.Tests.BuildRun;

public sealed class BuildRunViewModelTests
{
    private const string _missingSdk =
        "The dotnet command was not found. Install the .NET 8 SDK from https://dotnet.microsoft.com/download and try again.";

    // ---- environment (7.1) ----

    [Fact]
    public async Task Opening_the_page_checks_the_environment_and_shows_the_sdk_and_target()
    {
        using var t = TestServices.Create();

        var vm = await BuildRunTestData.OpenAsync(t);

        Assert.Equal(".NET SDK 8.0.100: found.", vm.SdkText);
        Assert.Equal("Target DesktopGL: supported on Windows 11.", vm.TargetText);
        Assert.False(vm.HasEnvironmentProblem);
        Assert.True(vm.BuildCommand.CanExecute(null));
        Assert.True(vm.BuildAndRunCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_missing_sdk_shows_the_message_and_link_and_disables_both_build_buttons()
    {
        using var t = TestServices.Create();
        t.Runner.Environment = new EnvironmentInfo(null, [], false, "Windows 11", true, _missingSdk);

        var vm = await BuildRunTestData.OpenAsync(t);

        Assert.Equal(".NET SDK: not found.", vm.SdkText);
        Assert.Equal(_missingSdk, vm.EnvironmentProblem);
        Assert.Equal("https://dotnet.microsoft.com/download", vm.EnvironmentLink);
        Assert.False(vm.BuildCommand.CanExecute(null));
        Assert.False(vm.BuildAndRunCommand.CanExecute(null));
        Assert.Empty(t.Runner.RunCalls);
    }

    [Fact]
    public async Task A_target_that_cannot_run_here_disables_build_and_says_so()
    {
        using var t = TestServices.Create();
        t.Runner.Environment = new EnvironmentInfo("8.0.100", ["8.0.100"], true, "Linux 6.5", false, "The WindowsDX target needs Windows.");

        var vm = await BuildRunTestData.OpenAsync(t, MonoGameTarget.WindowsDX);

        Assert.Equal("Target WindowsDX: not supported on Linux 6.5.", vm.TargetText);
        Assert.False(vm.BuildCommand.CanExecute(null));
        Assert.Null(vm.EnvironmentLink);
    }

    [Fact]
    public async Task Check_again_picks_up_a_newly_installed_sdk()
    {
        using var t = TestServices.Create();
        t.Runner.Environment = new EnvironmentInfo(null, [], false, "Windows 11", true, _missingSdk);
        var vm = await BuildRunTestData.OpenAsync(t);
        Assert.False(vm.BuildCommand.CanExecute(null));

        t.Runner.Environment = SampleData.Environment();
        await vm.RefreshEnvironmentCommand.ExecuteAsync(null);

        Assert.True(vm.BuildCommand.CanExecute(null));
        Assert.False(vm.HasEnvironmentProblem);
    }

    [Fact]
    public async Task The_runner_gets_the_output_folder_and_the_target_kind_of_the_migration_options()
    {
        using var t = TestServices.Create();
        var vm = await BuildRunTestData.OpenAsync(t, MonoGameTarget.WindowsDX);

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.Equal([(BuildRunTestData.OutputFolder, MonoGameTargetKind.WindowsDX, false)], t.Runner.RunCalls);
    }

    [Fact]
    public async Task Without_migration_options_nothing_can_be_built()
    {
        using var t = TestServices.Create();
        t.Session.Report = SampleData.Report();
        t.Session.MigrationResult = SampleData.Migration(completed: true);
        await t.OpenPageAsync(WorkflowStep.BuildRun);
        var vm = (BuildRunViewModel)t.Main.CurrentPage;

        Assert.False(vm.HasMigration);
        Assert.NotNull(vm.NoMigrationMessage);
        Assert.False(vm.BuildCommand.CanExecute(null));
        Assert.False(vm.BuildAndRunCommand.CanExecute(null));
    }

    // ---- log (7.1) ----

    [Fact]
    public async Task Log_lines_from_a_worker_thread_arrive_in_order_and_complete()
    {
        var script = new ScriptedRunService();
        script.OnRun = async (_, _, _, output, _) =>
        {
            await Task.Run(() =>
            {
                for (var i = 1; i <= 500; i++)
                {
                    output!.Report(new OutputLine($"line {i}", i % 100 == 0));
                }
            });
            return BuildRunTestData.Result(BuildRunTestData.SucceededBuild());
        };
        using var t = TestServices.Create(s => s.AddSingleton<IRunService>(script));
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.Equal(Enumerable.Range(1, 500).Select(i => $"line {i}"), vm.LogLines.Select(l => l.Text));
        Assert.Equal(Enumerable.Range(1, 500), vm.LogLines.Select(l => l.Number));
        Assert.Equal(5, vm.LogLines.Count(l => l.IsError));
    }

    [Fact]
    public async Task A_new_run_starts_with_an_empty_log()
    {
        using var t = TestServices.Create();
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildCommand.ExecuteAsync(null);
        await vm.BuildCommand.ExecuteAsync(null);

        Assert.Equal(["Build succeeded."], vm.LogLines.Select(l => l.Text));
        Assert.Equal(1, vm.LogLines[0].Number);
    }

    // ---- diagnostics (7.2) ----

    [Fact]
    public async Task A_failed_build_shows_groups_with_hint_rule_and_file_and_line()
    {
        var gamer = new DiagnosticGroup(
            BuildRunTestData.GamerServicesHint,
            [BuildRunTestData.Error("CS0234", "The type or namespace 'GamerServices' does not exist"), BuildRunTestData.Error("CS0246", "Guide not found", "Menu.cs", 40)]);
        var unknown = new DiagnosticGroup(null, [BuildRunTestData.Error("CS0103", "The name 'x' does not exist"), BuildRunTestData.Warning("CS0168", "Variable unused")]);
        using var t = TestServices.Create();
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.FailedBuild(gamer, unknown), problem: "The build failed.");
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Groups.Count);
        var first = vm.Groups[0];
        Assert.Equal("GamerServices no longer exists", first.Title);
        Assert.Equal("Remove the using and the Xbox Live code.", first.Advice);
        Assert.Equal("XNA009", first.RuleId);
        Assert.Equal("Related rule: XNA009", first.RuleText);
        Assert.Equal("2 errors", first.CountText);
        Assert.Equal(["Game1.cs:12", "Menu.cs:40"], first.Items.Select(i => i.Location));
        Assert.True(first.IsExpanded);
        Assert.True(first.IsError);

        var second = vm.Groups[1];
        Assert.Equal("No known fix for CS0103", second.Title);
        Assert.False(second.HasAdvice);
        Assert.False(second.HasRuleId);
        Assert.Equal("1 error, 1 warning", second.CountText);
        Assert.False(second.IsExpanded);

        Assert.Equal("2 groups: 3 errors, 1 warning.", vm.DiagnosticsSummary);
        Assert.Equal("The build failed: 3 errors in 3.2 s.", vm.StatusText);
        Assert.True(vm.IsStatusError);
        Assert.NotNull(t.Session.RunnerResult);
    }

    [Fact]
    public async Task A_content_error_without_a_file_shows_its_asset_path()
    {
        var asset = new BuildDiagnostic(BuildDiagnosticSeverity.Error, "MGCB", "Could not import", null, null, null, null, "Textures/hero.png");
        using var t = TestServices.Create();
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.FailedBuild(new DiagnosticGroup(null, [asset])), problem: "The build failed.");
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.Equal("Textures/hero.png", vm.Groups[0].Items[0].Location);
    }

    // ---- Build and Run, confirmation (7.3) ----

    [Fact]
    public async Task Build_alone_never_asks_and_never_launches()
    {
        using var t = TestServices.Create();
        t.Runner.ConfirmationToAsk = new LaunchConfirmation("C:\\out\\Game.exe", "message");
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.SucceededBuild());
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.False(t.Runner.RunCalls.Single().Launch);
        Assert.Empty(t.Dialogs.Confirmations);
        Assert.Null(t.Runner.ConfirmationAnswer);
        Assert.False(vm.IsGameRunning);
        Assert.Equal("The build succeeded in 1.5 s.", vm.StatusText);
    }

    [Fact]
    public async Task Build_and_run_asks_with_the_launch_text_and_No_means_nothing_runs()
    {
        using var t = TestServices.Create();
        t.Runner.ConfirmationToAsk = new LaunchConfirmation("C:\\out\\Game.exe", "You are about to run C:\\out\\Game.exe.");
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.SucceededBuild(), problem: "Launch declined.");
        t.Dialogs.ConfirmAnswers.Enqueue(false);
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildAndRunCommand.ExecuteAsync(null);

        var asked = Assert.Single(t.Dialogs.Confirmations);
        Assert.Equal("You are about to run C:\\out\\Game.exe.", asked.Message);
        Assert.Equal("Run game", asked.ConfirmText);
        Assert.Equal("Don't run", asked.CancelText);
        Assert.False(t.Runner.ConfirmationAnswer);
        Assert.True(t.Runner.RunCalls.Single().Launch);
        Assert.False(vm.IsGameRunning);
        Assert.False(vm.IsStatusError);
        Assert.Equal("Launch cancelled. The game was not started.", vm.StatusText);
        Assert.False(vm.StopCommand.CanExecute(null));
    }

    [Fact]
    public async Task Build_and_run_with_Yes_passes_the_answer_to_the_runner_and_shows_the_running_game()
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
            t.Runner.ConfirmationToAsk = new LaunchConfirmation("C:\\out\\Game.exe", "message");
            t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.SucceededBuild(), running: game);
            t.Dialogs.ConfirmAnswers.Enqueue(true);
            var vm = await BuildRunTestData.OpenAsync(t);

            await vm.BuildAndRunCommand.ExecuteAsync(null);

            Assert.True(t.Runner.ConfirmationAnswer);
            Assert.True(vm.IsGameRunning);
            Assert.False(vm.IsBusy);
            Assert.Contains("The game is running", vm.StatusText);
            Assert.True(vm.StopCommand.CanExecute(null));
            Assert.False(vm.BuildCommand.CanExecute(null));
            Assert.Same(game, t.Session.RunnerResult!.RunningGame);
            Assert.True(vm.GoToSummaryCommand.CanExecute(null));
        }
        finally
        {
            await game.StopAsync();
        }
    }

    [Fact]
    public async Task Stop_ends_the_game_through_its_run_handle()
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
            await vm.BuildAndRunCommand.ExecuteAsync(null);
            Assert.True(vm.IsGameRunning);

            await vm.StopCommand.ExecuteAsync(null);

            Assert.True(game.HasExited);
            Assert.False(vm.IsGameRunning);
            Assert.Equal("The game was stopped.", vm.StatusText);
            Assert.False(vm.IsStatusError);
            Assert.False(vm.StopCommand.CanExecute(null));
            Assert.True(vm.BuildCommand.CanExecute(null));
        }
        finally
        {
            await game.StopAsync();
        }
    }

    [Fact]
    public async Task A_game_that_comes_back_after_a_No_is_stopped_at_once()
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
            t.Runner.ConfirmationToAsk = new LaunchConfirmation("C:\\out\\Game.exe", "message");
            t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.SucceededBuild(), running: game);
            t.Dialogs.ConfirmAnswers.Enqueue(false);
            var vm = await BuildRunTestData.OpenAsync(t);

            await vm.BuildAndRunCommand.ExecuteAsync(null);

            Assert.True(game.HasExited);
            Assert.False(vm.IsGameRunning);
            Assert.Null(t.Session.RunnerResult!.RunningGame);
        }
        finally
        {
            await game.StopAsync();
        }
    }

    [Fact]
    public async Task A_game_that_comes_back_from_a_build_only_run_is_stopped_at_once()
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

            await vm.BuildCommand.ExecuteAsync(null);

            Assert.True(game.HasExited);
            Assert.False(vm.IsGameRunning);
        }
        finally
        {
            await game.StopAsync();
        }
    }

    [Fact]
    public async Task The_confirmation_dialog_is_asked_even_when_the_runner_asks_from_another_thread()
    {
        var script = new ScriptedRunService();
        script.OnRun = async (_, confirm, _, _, _) =>
        {
            var answer = await Task.Run(() => confirm(new LaunchConfirmation("C:\\out\\Game.exe", "message")));
            return BuildRunTestData.Result(BuildRunTestData.SucceededBuild(), problem: answer ? null : "Launch declined.");
        };
        using var t = TestServices.Create(s => s.AddSingleton<IRunService>(script));
        t.Dialogs.ConfirmAnswers.Enqueue(false);
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildAndRunCommand.ExecuteAsync(null);

        Assert.Single(t.Dialogs.Confirmations);
        Assert.Equal("Launch cancelled. The game was not started.", vm.StatusText);
    }

    // ---- cancel and failures (7.3) ----

    [Fact]
    public async Task Cancel_stops_a_running_build_and_leaves_no_result()
    {
        var started = new TaskCompletionSource();
        var script = new ScriptedRunService();
        script.OnRun = async (_, _, _, output, ct) =>
        {
            output!.Report(new OutputLine("Restoring...", false));
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        };
        using var t = TestServices.Create(s => s.AddSingleton<IRunService>(script));
        var vm = await BuildRunTestData.OpenAsync(t);

        var run = vm.BuildCommand.ExecuteAsync(null);
        await started.Task;
        Assert.True(vm.IsBusy);
        Assert.True(vm.CancelCommand.CanExecute(null));
        Assert.False(vm.BuildCommand.CanExecute(null));

        vm.CancelCommand.Execute(null);
        await run;

        Assert.False(vm.IsBusy);
        Assert.False(vm.CancelCommand.CanExecute(null));
        Assert.Equal("Cancelled. Nothing was started.", vm.StatusText);
        Assert.Equal(["Restoring..."], vm.LogLines.Select(l => l.Text));
        Assert.Null(t.Session.RunnerResult);
        Assert.False(t.Session.HasError);
        Assert.False(vm.GoToSummaryCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_service_exception_shows_the_error_banner_and_frees_the_page()
    {
        using var t = TestServices.Create();
        t.Runner.Failure = new InvalidOperationException("boom");
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.True(t.Session.HasError);
        Assert.Contains("boom", t.Session.ErrorDetails);
        Assert.False(vm.IsBusy);
        Assert.True(vm.IsStatusError);
        Assert.True(vm.BuildCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_refused_folder_or_environment_problem_from_the_runner_is_shown_as_the_status()
    {
        using var t = TestServices.Create();
        t.Runner.Result = BuildRunTestData.Result(problem: "This folder is not a completed migrated copy.");
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.Equal("This folder is not a completed migrated copy.", vm.StatusText);
        Assert.True(vm.IsStatusError);
    }

    // ---- startup failure (7.4) ----

    [Fact]
    public async Task A_startup_failure_shows_the_message_the_runtime_hints_and_the_captured_output()
    {
        var failure = new RunSession(1, true, "starting up", "Unhandled exception: NoAudioHardwareException", [BuildRunTestData.RuntimeHint], false);
        using var t = TestServices.Create();
        t.Runner.Result = BuildRunTestData.Result(
            BuildRunTestData.SucceededBuild(),
            failure: failure,
            problem: "The game stopped right after it started (exit code 1).");
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildAndRunCommand.ExecuteAsync(null);

        Assert.True(vm.HasStartupFailure);
        Assert.Equal("The game stopped right after it started (exit code 1).", vm.StartupFailureMessage);
        var hint = Assert.Single(vm.RuntimeHints);
        Assert.Equal("A graphics driver is missing", hint.Title);
        Assert.Equal("Install the OpenGL driver.", hint.Advice);
        Assert.False(vm.HasNoRuntimeHints);
        Assert.Equal("Standard output\nstarting up\n\nStandard error\nUnhandled exception: NoAudioHardwareException\n", vm.GameOutput);
        Assert.True(vm.IsStatusError);
        Assert.Equal("The game stopped right after it started (exit code 1).", vm.StatusText);
        Assert.False(vm.IsGameRunning);
    }

    [Fact]
    public async Task A_startup_failure_without_hints_says_there_is_no_known_fix()
    {
        var failure = new RunSession(2, true, string.Empty, "crash", [], false);
        using var t = TestServices.Create();
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.SucceededBuild(), failure: failure, problem: "The game stopped.");
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildAndRunCommand.ExecuteAsync(null);

        Assert.True(vm.HasNoRuntimeHints);
        Assert.Equal("Standard error\ncrash\n", vm.GameOutput);
    }

    // ---- summary and re-entry ----

    [Fact]
    public async Task After_a_build_the_page_can_go_to_the_summary()
    {
        using var t = TestServices.Create();
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.SucceededBuild());
        var vm = await BuildRunTestData.OpenAsync(t);
        Assert.False(vm.GoToSummaryCommand.CanExecute(null));

        await vm.BuildCommand.ExecuteAsync(null);
        Assert.True(vm.GoToSummaryCommand.CanExecute(null));
        vm.GoToSummaryCommand.Execute(null);

        Assert.Equal(WorkflowStep.Summary, t.Main.CurrentStep);
        Assert.Same(t.Runner.Result, t.Session.RunnerResult);
    }

    [Fact]
    public async Task Coming_back_after_the_session_was_reset_clears_the_old_results()
    {
        using var t = TestServices.Create();
        var vm = await BuildRunTestData.OpenAsync(t);
        await vm.BuildCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.LogLines);

        t.Session.Report = SampleData.Report(score: 10);
        t.Session.MigrationResult = SampleData.Migration(completed: true);
        t.Session.MigrationOptions = new MigrationOptions(BuildRunTestData.OutputFolder, MonoGameTarget.DesktopGL, false, false, new HashSet<string>());
        await vm.OnNavigatedToAsync();

        Assert.Empty(vm.LogLines);
        Assert.False(vm.HasResult);
        Assert.Equal("Ready.", vm.StatusText);
    }

    [Fact]
    public async Task Coming_back_with_an_unchanged_session_keeps_the_log_and_checks_the_environment_again()
    {
        var script = new ScriptedRunService
        {
            OnRun = (_, _, _, output, _) =>
            {
                output!.Report(new OutputLine("kept", false));
                return Task.FromResult(BuildRunTestData.Result(BuildRunTestData.SucceededBuild()));
            },
        };
        using var t = TestServices.Create(s => s.AddSingleton<IRunService>(script));
        var vm = await BuildRunTestData.OpenAsync(t);
        await vm.BuildCommand.ExecuteAsync(null);
        var checks = script.EnvironmentChecks;

        await vm.OnNavigatedToAsync();

        Assert.Equal(["kept"], vm.LogLines.Select(l => l.Text));
        Assert.Equal(checks + 1, script.EnvironmentChecks);
    }
}
