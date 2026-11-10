using System.ComponentModel;

namespace XnaReborn.App.Tests;

public sealed class ErrorBannerTests
{
    [Fact]
    public void ShowError_makes_the_banner_visible_and_raises_change_notifications()
    {
        using var t = TestServices.Create();
        var raised = new List<string?>();
        ((INotifyPropertyChanged)t.Main).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        t.Session.ShowError("Could not read the project.", "line 1\nline 2");

        Assert.True(t.Main.HasError);
        Assert.Equal("Could not read the project.", t.Main.ErrorMessage);
        Assert.Equal("line 1\nline 2", t.Main.ErrorDetails);
        Assert.True(t.Main.HasErrorDetails);
        Assert.Contains(nameof(t.Main.HasError), raised);
        Assert.Contains(nameof(t.Main.ErrorMessage), raised);
    }

    [Fact]
    public void An_error_without_details_has_nothing_to_expand()
    {
        using var t = TestServices.Create();

        t.Session.ShowError("Short.");

        Assert.False(t.Main.HasErrorDetails);
    }

    [Fact]
    public async Task Copy_puts_the_message_and_the_details_on_the_clipboard()
    {
        using var t = TestServices.Create();
        t.Session.ShowError("Could not read the project.", "stack trace here");

        await t.Main.CopyDetailsCommand.ExecuteAsync(null);

        Assert.NotNull(t.Clipboard.Text);
        Assert.Contains("Could not read the project.", t.Clipboard.Text, StringComparison.Ordinal);
        Assert.Contains("stack trace here", t.Clipboard.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Copy_without_details_copies_only_the_message()
    {
        using var t = TestServices.Create();
        t.Session.ShowError("Short.");

        await t.Main.CopyDetailsCommand.ExecuteAsync(null);

        Assert.Equal("Short.", t.Clipboard.Text);
    }

    [Fact]
    public async Task A_failing_clipboard_keeps_the_error_and_adds_the_copy_failure_to_the_details()
    {
        using var t = TestServices.Create();
        t.Clipboard.Failure = new InvalidOperationException("clipboard is locked");
        t.Session.ShowError("Original.", "original details");

        await t.Main.CopyDetailsCommand.ExecuteAsync(null);

        Assert.Equal("Original.", t.Main.ErrorMessage);
        Assert.Contains("original details", t.Main.ErrorDetails, StringComparison.Ordinal);
        Assert.Contains("clipboard is locked", t.Main.ErrorDetails, StringComparison.Ordinal);
    }

    [Fact]
    public void Dismiss_clears_the_error()
    {
        using var t = TestServices.Create();
        t.Session.ShowError("Boom.", "details");

        t.Main.DismissCommand.Execute(null);

        Assert.False(t.Main.HasError);
        Assert.Null(t.Main.ErrorMessage);
        Assert.Null(t.Main.ErrorDetails);
        Assert.False(t.Session.HasError);
    }
}
