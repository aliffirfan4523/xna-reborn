using XnaReborn.Migrator.Content;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator.Content;

/// <summary>Replacing the family in a spritefont copy.</summary>
[Trait("Category", "Migrator")]
public class FontSubstituterTests
{
    private const string _template =
        "\uFEFF<?xml version=\"1.0\" encoding=\"utf-8\"?>{NL}<!-- <FontName>Decoy</FontName> -->{NL}" +
        "<XnaContent xmlns:Graphics=\"x\">{NL}  <Asset Type=\"Graphics:FontDescription\">{NL}" +
        "    <FontName>Pescadero</FontName>{NL}    <Size>14</Size>{NL}    <!-- <FontName>Other</FontName> -->{NL}" +
        "    <FontName>Second</FontName>{NL}  </Asset>{NL}</XnaContent>{NL}";

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Replaces_only_the_first_font_name_content(string newline)
    {
        var input = _template.Replace("{NL}", newline);
        var expected = input.Replace("<FontName>Pescadero</FontName>", "<FontName>Georgia</FontName>");

        Assert.Equal(expected, FontSubstituter.Substitute(input, "Georgia"));
    }

    [Fact]
    public void Keeps_attributes_and_prefix()
    {
        var input = "<A>\r\n  <g:FontName  id=\"a>b\" >  Pescadero </g:FontName>\r\n</A>";

        Assert.Equal("<A>\r\n  <g:FontName  id=\"a>b\" >Georgia</g:FontName>\r\n</A>", FontSubstituter.Substitute(input, "Georgia"));
    }

    [Fact]
    public void Escapes_the_new_family()
    {
        Assert.Equal("<A><FontName>A &amp; B</FontName></A>", FontSubstituter.Substitute("<A><FontName>X</FontName></A>", "A & B"));
    }

    [Fact]
    public void Works_on_a_fixture_file_and_the_result_checks_as_not_xna_only()
    {
        var text = File.ReadAllText(Path.Combine(FixturePaths.Of("xna40-custom-content"), "Content", "Fonts", "Hud.spritefont"));

        var result = FontSubstituter.Substitute(text, "Georgia");

        Assert.Equal(text.Replace("<FontName>Pericles</FontName>", "<FontName>Georgia</FontName>"), result);
        Assert.Equal(new FontCheckResult("Georgia", false, null, null), FontChecker.Check(result));
    }

    [Theory]
    [InlineData("<A><!-- <FontName>X</FontName> --></A>")]
    [InlineData("<A/>")]
    [InlineData("<A><FontName/></A>")]
    [InlineData("")]
    public void No_font_name_throws(string text)
    {
        Assert.Throws<InvalidOperationException>(() => FontSubstituter.Substitute(text, "Arial"));
    }

    [Fact]
    public void Empty_new_family_throws()
    {
        Assert.Throws<ArgumentException>(() => FontSubstituter.Substitute("<A><FontName>X</FontName></A>", " "));
    }
}
