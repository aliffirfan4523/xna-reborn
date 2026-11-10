using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;

namespace XnaReborn.App.Tests.Summary;

public sealed class SummaryViewTests
{
    [AvaloniaFact]
    public async Task The_summary_page_shows_the_scores_and_its_buttons_are_named()
    {
        using var t = TestServices.Create();
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        await t.OpenPageAsync(WorkflowStep.Summary);
        Dispatcher.UIThread.RunJobs();

        var view = window.GetVisualDescendants().OfType<SummaryView>().Single();
        var texts = view.GetVisualDescendants().OfType<TextBlock>().ToList();
        Assert.Contains(texts, x => AutomationProperties.GetAutomationId(x) == "BeforeScore" && x.Text == "40");
        Assert.Contains(texts, x => AutomationProperties.GetAutomationId(x) == "AfterScore" && x.Text == "90");
        var buttons = view.GetVisualDescendants().OfType<Button>().Select(b => b.Content).ToList();
        Assert.Equal(["Open output folder", "Open manifest", "Start over"], buttons);
    }
}
