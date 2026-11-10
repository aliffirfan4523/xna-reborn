namespace XnaReborn.App.Tests.Banner;

public sealed class BannerCommandTests
{
    [Fact]
    public async Task Copy_text_is_the_message_then_a_blank_line_then_the_details()
    {
        using var t = TestServices.Create();
        t.Session.ShowError("Message.", "Details.");

        await t.Main.CopyDetailsCommand.ExecuteAsync(null);

        Assert.Equal("Message." + Environment.NewLine + Environment.NewLine + "Details.", t.Clipboard.Text);
    }

    [Fact]
    public void A_new_error_collapses_the_details_again()
    {
        using var t = TestServices.Create();
        t.Session.ShowError("One.", "details");
        t.Main.IsDetailsExpanded = true;

        t.Session.ShowError("Two.", "more");

        Assert.False(t.Main.IsDetailsExpanded);
    }

    [Fact]
    public void Dismiss_hides_the_banner_and_collapses_the_details()
    {
        using var t = TestServices.Create();
        t.Session.ShowError("One.", "details");
        t.Main.IsDetailsExpanded = true;

        t.Main.DismissCommand.Execute(null);

        Assert.False(t.Main.HasError);
        Assert.False(t.Main.HasErrorDetails);
        Assert.False(t.Main.IsDetailsExpanded);
    }
}
