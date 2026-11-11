using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Controls;
using XnaReborn.App.Services;
using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using XnaReborn.Runner;
using XnaReborn.Runner.Hints;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Run;

namespace XnaReborn.App.Tests.BuildRun;

/// <summary>The pipeline cards, the log filter and the log controls of the Build and run page.</summary>
public sealed class BuildRunPipelineTests
{
    [Fact]
    public async Task Before_a_run_the_sdk_card_is_done_and_build_and_launch_wait()
    {
        using var t = TestServices.Create();
        var vm = await BuildRunTestData.OpenAsync(t);

        Assert.Equal(PipelineState.Done, vm.SdkStep.State);
        Assert.Equal(PipelineState.Waiting, vm.BuildStep.State);
        Assert.Equal(PipelineState.Waiting, vm.LaunchStep.State);
        Assert.Equal("Waiting", vm.BuildStep.StateText);
    }

    [Fact]
    public async Task A_missing_sdk_fails_the_sdk_card()
    {
        var script = new ScriptedRunService
        {
            Environment = new("", [], false, "Windows 11", true, "Install the .NET SDK from https://dotnet.microsoft.com/download"),
        };
        using var t = TestServices.Create(s => s.AddSingleton<IRunService>(script));
        var vm = await BuildRunTestData.OpenAsync(t);

        Assert.Equal(PipelineState.Failed, vm.SdkStep.State);
        Assert.Contains("Install the .NET SDK", vm.SdkStep.Detail);
    }

    [Fact]
    public async Task A_failed_build_marks_build_failed_with_its_time_and_skips_launch()
    {
        using var t = TestServices.Create();
        var group = new DiagnosticGroup(BuildRunTestData.GamerServicesHint, [BuildRunTestData.Error("CS0234", "missing")]);
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.FailedBuild(group), problem: "The build failed.");
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.Equal(PipelineState.Failed, vm.BuildStep.State);
        Assert.True(vm.BuildStep.HasElapsed);
        Assert.Equal("Build: Failed, 3.2 s", vm.BuildStep.AutomationName);
        Assert.Equal(PipelineState.Skipped, vm.LaunchStep.State);
    }

    [Fact]
    public async Task A_good_build_only_run_marks_build_done_and_launch_skipped()
    {
        using var t = TestServices.Create();
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.SucceededBuild());
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.Equal(PipelineState.Done, vm.BuildStep.State);
        Assert.Equal(PipelineState.Skipped, vm.LaunchStep.State);
    }

    [Fact]
    public async Task The_launch_card_waits_for_the_users_answer_and_a_no_skips_it()
    {
        var script = new ScriptedRunService();
        PipelineState? whileAsking = null;
        PipelineState? buildWhileAsking = null;
        script.OnRun = async (_, confirm, _, _, _) =>
        {
            await confirm(new LaunchConfirmation("C:\\out\\Game.exe", "Run it?"));
            return BuildRunTestData.Result(BuildRunTestData.SucceededBuild(), problem: "Launch declined.");
        };
        using var t = TestServices.Create(s => s.AddSingleton<IRunService>(script));
        var vm = await BuildRunTestData.OpenAsync(t);
        t.Dialogs.ConfirmAnswers.Enqueue(false);
        t.Dialogs.OnConfirm = () =>
        {
            whileAsking = vm.LaunchStep.State;
            buildWhileAsking = vm.BuildStep.State;
        };

        await vm.BuildAndRunCommand.ExecuteAsync(null);

        Assert.Equal(PipelineState.Running, whileAsking);
        Assert.Equal(PipelineState.Done, buildWhileAsking);
        Assert.Equal(PipelineState.Skipped, vm.LaunchStep.State);
    }

    [Fact]
    public async Task The_filter_and_errors_only_narrow_the_shown_lines_but_not_the_log()
    {
        var script = new ScriptedRunService();
        script.OnRun = (_, _, _, output, _) =>
        {
            output!.Report(new OutputLine("restore ok", false));
            output.Report(new OutputLine("Game1.cs(3): error CS0234", true));
            output.Report(new OutputLine("compile Other.cs", false));
            return Task.FromResult(BuildRunTestData.Result(BuildRunTestData.SucceededBuild()));
        };
        using var t = TestServices.Create(s => s.AddSingleton<IRunService>(script));
        var vm = await BuildRunTestData.OpenAsync(t);
        await vm.BuildCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.VisibleLogLines.Count);

        vm.LogFilter = "COMPILE";
        Assert.Equal(["compile Other.cs"], vm.VisibleLogLines.Select(l => l.Text));

        vm.LogFilter = string.Empty;
        vm.ErrorsOnly = true;
        Assert.Equal(["Game1.cs(3): error CS0234"], vm.VisibleLogLines.Select(l => l.Text));
        Assert.Equal(3, vm.LogLines.Count);
        Assert.Equal("1 line shown of 3, 1 error line.", vm.LogSummary);
    }

    [AvaloniaFact]
    public async Task The_page_shows_three_cards_with_state_words_and_the_log_controls()
    {
        using var t = TestServices.Create();
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.SucceededBuild());
        var vm = await BuildRunTestData.OpenAsync(t);
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await vm.BuildCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        var chips = window.GetVisualDescendants().OfType<Chip>().ToList();
        Assert.Contains(chips, c => c.Text == "Done" && AutomationProperties.GetAutomationId(c) == "BuildStepState");
        Assert.Contains(chips, c => c.Text == "Skipped" && AutomationProperties.GetAutomationId(c) == "LaunchStepState");
        var ids = window.GetVisualDescendants().OfType<Control>().Select(AutomationProperties.GetAutomationId).ToHashSet();
        Assert.Contains("LogFilter", ids);
        Assert.Contains("LogAutoScroll", ids);
        Assert.Contains("CopyLogButton", ids);
        Assert.Contains("LogSummary", ids);
        Assert.NotNull(window.GetVisualDescendants().OfType<BuildLogView>().Single());
    }
}
