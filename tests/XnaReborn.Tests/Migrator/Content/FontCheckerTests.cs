using XnaReborn.Migrator.Content;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator.Content;

/// <summary>Reading the family from a spritefont.</summary>
[Trait("Category", "Migrator")]
public class FontCheckerTests
{
    private static string Xml(string family) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<XnaContent xmlns:Graphics=\"x\"><Asset Type=\"Graphics:FontDescription\"><FontName>" + family + "</FontName></Asset></XnaContent>";

    [Fact]
    public void Basic_fixture_arial_is_not_xna_only()
    {
        var text = File.ReadAllText(Path.Combine(FixturePaths.Of("xna40-basic"), "Content", "Fonts", "Font.spritefont"));

        Assert.Equal(new FontCheckResult("Arial", false, null, null), FontChecker.Check(text));
    }

    [Fact]
    public void Custom_content_fixture_pericles_is_xna_only()
    {
        var text = File.ReadAllText(Path.Combine(FixturePaths.Of("xna40-custom-content"), "Content", "Fonts", "Hud.spritefont"));

        Assert.Equal(new FontCheckResult("Pericles", true, "Georgia", null), FontChecker.Check(text));
    }

    [Fact]
    public void Pescadero_is_xna_only()
    {
        Assert.Equal(new FontCheckResult("Pescadero", true, "Georgia", null), FontChecker.Check(Xml("Pescadero")));
    }

    [Fact]
    public void Family_is_trimmed()
    {
        Assert.Equal("Kootenay", FontChecker.Check(Xml("  Kootenay\n")).Family);
    }

    [Fact]
    public void First_font_name_wins_and_comments_are_ignored()
    {
        var text = "<A><!-- <FontName>Pericles</FontName> --><FontName>Arial</FontName><FontName>Pescadero</FontName></A>";

        Assert.Equal("Arial", FontChecker.Check(text).Family);
    }

    [Fact]
    public void Invalid_xml_gives_a_problem()
    {
        var result = FontChecker.Check("<XnaContent><FontName>Arial</XnaContent>");

        Assert.Null(result.Family);
        Assert.False(result.IsXnaOnly);
        Assert.Contains("not valid XML", result.Problem);
    }

    [Theory]
    [InlineData("<XnaContent />")]
    [InlineData("<A><FontName></FontName></A>")]
    [InlineData("")]
    public void Missing_or_empty_font_name_gives_a_problem(string text)
    {
        var result = FontChecker.Check(text);

        Assert.Null(result.Family);
        Assert.False(string.IsNullOrEmpty(result.Problem));
    }
}
