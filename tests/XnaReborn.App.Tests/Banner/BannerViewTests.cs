using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.Views;

namespace XnaReborn.App.Tests.Banner;

public sealed class BannerViewTests
{
    [AvaloniaFact]
    public void Details_are_collapsed_until_expanded_and_the_banner_has_an_accessible_name()
    {
        using var t = TestServices.Create();
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        t.Session.ShowError("Could not read the project.", "stack trace");
        Dispatcher.UIThread.RunJobs();

        var banner = window.GetVisualDescendants().OfType<Border>().Single(b => AutomationProperties.GetAutomationId(b) == "ErrorBanner");
        var expander = banner.GetVisualDescendants().OfType<Expander>().Single();
        Assert.Equal("Error", AutomationProperties.GetName(banner));
        Assert.True(expander.IsVisible);
        Assert.False(expander.IsExpanded);

        t.Main.IsDetailsExpanded = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(expander.IsExpanded);

        t.Session.ShowError("Another error.");
        Dispatcher.UIThread.RunJobs();
        Assert.False(expander.IsVisible);
        Assert.False(t.Main.IsDetailsExpanded);
    }
}
