using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;

namespace XnaReborn.App.Tests;

/// <summary>
/// How to write a headless test for a screen: build <see cref="TestServices"/>, create the window with the real
/// <c>MainViewModel</c>, show it, run the dispatcher jobs, then look at the visual tree.
/// </summary>
public sealed class MainWindowTests
{
    [AvaloniaFact]
    public void The_window_shows_one_keyboard_focusable_button_per_step_with_accessible_names()
    {
        using var t = TestServices.Create();
        var window = Show(t);

        var buttons = StepButtons(window);

        Assert.Equal(6, buttons.Count);
        Assert.Equal(
            ["Step 1: Open, current", "Step 2: Report, not available yet", "Step 3: Migration setup, not available yet", "Step 4: Review changes, not available yet", "Step 5: Build and run, not available yet", "Step 6: Summary, not available yet"],
            buttons.Select(b => AutomationProperties.GetName(b)));
        Assert.All(buttons, b =>
        {
            Assert.True(b.Focusable);
            Assert.True(b.IsTabStop);
        });
        Assert.True(buttons[0].IsEnabled);
        Assert.All(buttons.Skip(1), b => Assert.False(b.IsEnabled));
    }

    [AvaloniaFact]
    public void The_current_page_is_shown_through_the_view_locator()
    {
        using var t = TestServices.Create();
        var window = Show(t);

        Assert.NotNull(window.GetVisualDescendants().OfType<OpenProjectView>().SingleOrDefault());
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "Open a project");
    }

    [AvaloniaFact]
    public void Pressing_an_enabled_step_button_changes_the_page()
    {
        using var t = TestServices.Create();
        t.Session.Report = Fakes.SampleData.Report();
        var window = Show(t);

        var report = StepButtons(window).Single(b => AutomationProperties.GetAutomationId(b) == "Step_Report");
        Assert.True(report.IsEnabled);
        report.Command!.Execute(report.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WorkflowStep.Report, t.Main.CurrentStep);
        Assert.NotNull(window.GetVisualDescendants().OfType<ReportView>().SingleOrDefault());
        Assert.Null(window.GetVisualDescendants().OfType<OpenProjectView>().SingleOrDefault());
    }

    [AvaloniaFact]
    public void The_error_banner_appears_with_a_message_and_goes_away_on_dismiss()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        var banner = window.GetVisualDescendants().OfType<Border>().Single(b => AutomationProperties.GetAutomationId(b) == "ErrorBanner");
        Assert.False(banner.IsVisible);

        t.Session.ShowError("Could not read the project.", "details");
        Dispatcher.UIThread.RunJobs();

        Assert.True(banner.IsVisible);
        Assert.Contains(banner.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "Could not read the project.");
        var dismiss = banner.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "ErrorDismiss");
        dismiss.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(banner.IsVisible);
    }

    private static MainWindow Show(TestServices t)
    {
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static List<Button> StepButtons(MainWindow window) =>
        window.GetVisualDescendants()
            .OfType<Button>()
            .Where(b => AutomationProperties.GetAutomationId(b)?.StartsWith("Step_", StringComparison.Ordinal) == true)
            .ToList();
}
