using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.Tests.BuildRun;
using XnaReborn.App.Views;

namespace XnaReborn.App.Tests.Accessibility;

/// <summary>Studio UI task 9.4 (R8.3): a failure is shown with an icon and the word "Error", not only in red.</summary>
public sealed class NonColorCueTests
{
    [AvaloniaFact]
    public async Task A_failed_build_is_shown_with_an_error_icon_and_the_word_Error_next_to_the_message()
    {
        using var t = TestServices.Create();
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.FailedBuild(), problem: "The build failed.");
        var vm = await BuildRunTestData.OpenAsync(t);
        var window = new MainWindow { DataContext = t.Main };
        window.Show();

        await vm.BuildCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.IsStatusError);
        var icon = (string)Application.Current!.FindResource("SeverityErrorIcon")!;
        var visibleTexts = window.GetVisualDescendants().OfType<TextBlock>().Where(x => x.IsEffectivelyVisible).Select(x => x.Text).ToList();
        Assert.Contains(icon, visibleTexts);
        Assert.Contains("Error", visibleTexts);
        Assert.Contains(vm.StatusText, visibleTexts);
    }
}
