using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.Views;

namespace XnaReborn.App.Tests.Open;

public sealed class OpenProjectViewTests
{
    [AvaloniaFact]
    public void The_page_shows_browse_buttons_and_the_recent_list_and_a_recent_click_opens_the_report()
    {
        using var t = TestServices.Create();
        var folder = RepoPaths.Fixture("xna40-basic");
        t.Recent.Add(folder);
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        t.Main.CurrentPage.OnNavigatedToAsync().GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();

        var buttons = window.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Contains(buttons, b => AutomationProperties.GetAutomationId(b) == "BrowseFolder");
        Assert.Contains(buttons, b => AutomationProperties.GetAutomationId(b) == "BrowseFile");
        var recent = buttons.Single(b => AutomationProperties.GetName(b) == "Open " + folder);

        recent.Command!.Execute(recent.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(XnaReborn.App.ViewModels.WorkflowStep.Report, t.Main.CurrentStep);
        Assert.Equal(folder, t.Session.SourcePath);
    }
}
