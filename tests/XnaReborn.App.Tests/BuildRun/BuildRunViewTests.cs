using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.Controls;
using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using XnaReborn.Runner.Hints;

namespace XnaReborn.App.Tests.BuildRun;

public sealed class BuildRunViewTests
{
    [AvaloniaFact]
    public async Task The_page_shows_the_environment_the_log_and_the_grouped_diagnostics_after_a_failed_build()
    {
        using var t = TestServices.Create();
        var group = new DiagnosticGroup(
            BuildRunTestData.GamerServicesHint,
            [BuildRunTestData.Error("CS0234", "The type or namespace 'GamerServices' does not exist")]);
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.FailedBuild(group), problem: "The build failed.");
        var vm = await BuildRunTestData.OpenAsync(t);
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(window.GetVisualDescendants().OfType<BuildRunView>().SingleOrDefault());
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == ".NET SDK 8.0.100: found.");
        Assert.True(Button("BuildButton", window).IsEffectivelyEnabled);
        Assert.True(Button("BuildAndRunButton", window).IsEffectivelyEnabled);
        Assert.False(Button("CancelButton", window).IsEffectivelyEnabled);
        Assert.False(Button("StopButton", window).IsEffectivelyEnabled);
        Assert.False(Button("SummaryButton", window).IsEffectivelyEnabled);

        await vm.BuildCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(window.GetVisualDescendants().OfType<BuildLogView>().SingleOrDefault());
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "Build succeeded.");
        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text).ToList();
        Assert.Contains("GamerServices no longer exists", texts);
        Assert.Contains("Related rule: XNA009", texts);
        Assert.Contains("CS0234", texts);
        Assert.Contains("Game1.cs:12", texts);
        Assert.Contains("The build failed: 1 error in 3.2 s.", texts);
        Assert.True(Button("SummaryButton", window).IsEffectivelyEnabled);
    }

    private static Button Button(string id, Window window) =>
        window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == id);
}
