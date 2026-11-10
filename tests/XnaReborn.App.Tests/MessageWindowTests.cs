using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using XnaReborn.App.Views;

namespace XnaReborn.App.Tests;

public sealed class MessageWindowTests
{
    [AvaloniaFact]
    public void A_confirmation_shows_both_buttons_and_no_details()
    {
        var window = new MessageWindow("Replace?", "The folder is not empty.", null, "Replace", "Keep");
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var buttons = new[] { "CancelButton", "ConfirmButton" }.Select(n => window.FindControl<Button>(n)!).Where(b => b.IsVisible).Select(b => b.Content).ToList();
        Assert.Equal(["Keep", "Replace"], buttons);
        Assert.False(window.FindControl<Expander>("DetailsExpander")!.IsVisible);
        Assert.Equal("The folder is not empty.", window.FindControl<TextBlock>("MessageText")!.Text);
    }

    [AvaloniaFact]
    public void An_error_shows_one_button_and_the_details()
    {
        var window = new MessageWindow("Error", "It broke.", "stack", "Close", null);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.FindControl<Button>("CancelButton")!.IsVisible);
        Assert.Equal("Close", window.FindControl<Button>("ConfirmButton")!.Content);
        Assert.True(window.FindControl<Expander>("DetailsExpander")!.IsVisible);
        Assert.Equal("stack", window.FindControl<TextBox>("DetailsText")!.Text);
    }
}
