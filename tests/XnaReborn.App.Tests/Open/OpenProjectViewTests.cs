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

    [AvaloniaFact]
    public void Remove_forgets_the_recent_project_and_shows_the_friendly_empty_state()
    {
        using var t = TestServices.Create();
        t.Recent.Add("C:/projects/Old");
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        t.Main.CurrentPage.OnNavigatedToAsync().GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<XnaReborn.App.Controls.EmptyState>(), e => e.IsEffectivelyVisible);

        var remove = window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Remove C:/projects/Old from recent projects");
        remove.Command!.Execute(remove.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(t.Recent.Items);
        Assert.Contains(window.GetVisualDescendants().OfType<XnaReborn.App.Controls.EmptyState>(), e => e.IsEffectivelyVisible && e.Title == "No recent projects yet");
    }

    [AvaloniaFact]
    public void A_bad_path_shows_a_plain_error_banner_with_a_next_step_button()
    {
        using var t = TestServices.Create();
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        var vm = t.Get<XnaReborn.App.ViewModels.OpenProjectViewModel>();
        vm.OpenPathAsync("C:/does/not/exist").GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();

        var banner = window.GetVisualDescendants().OfType<XnaReborn.App.Controls.Banner>()
            .Single(b => AutomationProperties.GetAutomationId(b) == "OpenMessage");
        Assert.True(banner.IsEffectivelyVisible);
        Assert.Contains("does not exist", banner.Message);
        Assert.Contains(banner.GetVisualDescendants().OfType<Button>(), b => AutomationProperties.GetName(b) == "Choose another project folder");
    }

    [AvaloniaFact]
    public void The_drop_zone_shows_the_drag_state_with_text_not_only_color()
    {
        using var t = TestServices.Create();
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        var vm = t.Get<XnaReborn.App.ViewModels.OpenProjectViewModel>();
        var zone = window.GetVisualDescendants().OfType<Border>().Single(b => AutomationProperties.GetAutomationId(b) == "DropZone");

        vm.IsDragOver = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("dragover", zone.Classes);
        Assert.Contains(zone.GetVisualDescendants().OfType<TextBlock>(), x => x.IsEffectivelyVisible && x.Text == "Release to analyze this project");
    }
}
