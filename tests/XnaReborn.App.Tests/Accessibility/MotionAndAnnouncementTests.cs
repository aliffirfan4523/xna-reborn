using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.Controls;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using AppBanner = XnaReborn.App.Controls.Banner;

namespace XnaReborn.App.Tests.Accessibility;

/// <summary>Reduced motion, live announcements, and the keyboard shortcuts that must keep working.</summary>
public sealed class MotionAndAnnouncementTests
{
    private const string _reduceVariable = "XNAREBORN_REDUCE_MOTION";

    [AvaloniaFact]
    public void The_reduce_motion_variable_turns_reduced_motion_on()
    {
        using var _ = Reduced(true);
        Assert.True(MotionPreference.Reduce());
    }

    [AvaloniaFact]
    public void The_main_window_carries_the_reduce_motion_class_when_motion_is_reduced()
    {
        using (Reduced(true))
        {
            using var t = TestServices.Create();
            Assert.Contains("reduce-motion", new MainWindow { DataContext = t.Main }.Classes);
        }

    }

    [AvaloniaFact]
    public void An_indeterminate_progress_bar_stops_sliding_when_motion_is_reduced()
    {
        foreach (var reduce in new[] { true, false })
        {
            var bar = new ProgressBar { IsIndeterminate = true, Width = 300 };
            var window = new Window { Content = bar, Width = 400, Height = 100 };
            if (reduce)
            {
                window.Classes.Add("reduce-motion");
            }

            window.Show();
            Dispatcher.UIThread.RunJobs();
            var sliding = bar.GetVisualDescendants().OfType<Border>().Where(b => b.Name is "IndeterminateProgressBarIndicator" or "IndeterminateProgressBarIndicator2");

            Assert.All(sliding, b => Assert.Equal(!reduce, b.IsVisible));
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_page_change_fade_does_not_run_when_motion_is_reduced()
    {
        using var _ = Reduced(true);
        using var t = TestServices.Create();
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        await t.OpenPageAsync(WorkflowStep.Report);
        Dispatcher.UIThread.RunJobs();

        var host = window.FindControl<ContentControl>("PageHost")!;
        Assert.Equal(1.0, host.Opacity);
        Assert.Null(host.RenderTransform);
    }

    [AvaloniaFact]
    public void An_error_banner_interrupts_and_the_other_kinds_wait()
    {
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(new AppBanner { Kind = StatusKind.Error, Title = "Failed" }));
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(new AppBanner { Kind = StatusKind.Warning, Title = "Careful" }));
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(new AppBanner { Kind = StatusKind.Success, Title = "Done" }));
    }

    [AvaloniaFact]
    public void A_working_progress_label_is_announced_and_a_percent_is_not()
    {
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(new ProgressLabel { Label = "Reading", IsIndeterminate = true }));
        Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(new ProgressLabel { Label = "Reading", Value = 40 }));
    }

    [AvaloniaFact]
    public async Task The_status_bar_is_a_polite_live_region()
    {
        using var t = TestServices.Create();
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        await t.OpenPageAsync(WorkflowStep.Open);

        var status = window.GetVisualDescendants().OfType<TextBlock>().Single(b => AutomationProperties.GetAutomationId(b) == "StatusText");
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(status));
    }

    [AvaloniaFact]
    public async Task Ctrl_digit_still_jumps_between_unlocked_steps()
    {
        using var t = TestServices.Create();
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        await t.OpenPageAsync(WorkflowStep.Summary);
        window.Focus();
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        var kb = window.KeyBindings[1]; Assert.True(kb.Command is not null, "command null; gesture " + kb.Gesture + " param " + kb.CommandParameter + " dc " + window.DataContext); Assert.True(kb.Command!.CanExecute(kb.CommandParameter), "cannot execute");
        window.KeyPress(Key.D2, RawInputModifiers.Control, PhysicalKey.Digit2, "2");
        await WaitForAsync(() => t.Main.StatusText.StartsWith("Step 2 of 6", StringComparison.Ordinal));
        Assert.StartsWith("Step 2 of 6", t.Main.StatusText);

        window.KeyPress(Key.D1, RawInputModifiers.Control, PhysicalKey.Digit1, "1");
        await WaitForAsync(() => t.Main.StatusText.StartsWith("Step 1 of 6", StringComparison.Ordinal));
        Assert.StartsWith("Step 1 of 6", t.Main.StatusText);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 50 && !condition(); i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static IDisposable Reduced(bool on)
    {
        var before = Environment.GetEnvironmentVariable(_reduceVariable);
        Environment.SetEnvironmentVariable(_reduceVariable, on ? "1" : "0");
        return new Restore(before);
    }

    private sealed class Restore(string? value) : IDisposable
    {
        public void Dispose() => Environment.SetEnvironmentVariable(_reduceVariable, value);
    }
}
