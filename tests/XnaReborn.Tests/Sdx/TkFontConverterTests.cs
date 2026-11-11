using XnaReborn.Migrator.Content;

namespace XnaReborn.Tests.Sdx;

/// <summary>Task 9.2: TkFont XML becomes a .spritefont, field by field.</summary>
[Trait("Category", "Sdx")]
public sealed class TkFontConverterTests
{
    private const string _full = """
        <?xml version="1.0" encoding="utf-8"?>
        <TkFont>
          <FontName>Arial</FontName>
          <Size>16</Size>
          <Spacing>1.5</Spacing>
          <UseKerning>true</UseKerning>
          <Style>Bold Italic</Style>
          <CharacterRegions>
            <CharacterRegion><Start>32</Start><End>126</End></CharacterRegion>
            <CharacterRegion><Start>160</Start><End>255</End></CharacterRegion>
          </CharacterRegions>
          <DefaultCharacter>63</DefaultCharacter>
        </TkFont>
        """;

    private const string _expected = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<XnaContent xmlns:Graphics=\"Microsoft.Xna.Framework.Content.Pipeline.Graphics\">\n"
        + "  <Asset Type=\"Graphics:FontDescription\">\n"
        + "    <FontName>Arial</FontName>\n"
        + "    <Size>16</Size>\n"
        + "    <Spacing>1.5</Spacing>\n"
        + "    <UseKerning>true</UseKerning>\n"
        + "    <Style>Bold, Italic</Style>\n"
        + "    <DefaultCharacter>&#63;</DefaultCharacter>\n"
        + "    <CharacterRegions>\n"
        + "      <CharacterRegion>\n"
        + "        <Start>&#32;</Start>\n"
        + "        <End>&#126;</End>\n"
        + "      </CharacterRegion>\n"
        + "      <CharacterRegion>\n"
        + "        <Start>&#160;</Start>\n"
        + "        <End>&#255;</End>\n"
        + "      </CharacterRegion>\n"
        + "    </CharacterRegions>\n"
        + "  </Asset>\n"
        + "</XnaContent>\n";

    private static string Root(string inner) => $"<TkFont>{inner}</TkFont>";

    [Fact]
    public void Every_mapped_field_is_converted()
    {
        var result = TkFontConverter.Convert(_full);

        Assert.Null(result.Error);
        Assert.Equal(_expected, result.SpriteFont);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Defaults_follow_the_toolkit_font_description()
    {
        var result = TkFontConverter.Convert(Root("<FontName>Segoe UI</FontName>"));

        Assert.Contains("<Size>23</Size>", result.SpriteFont, StringComparison.Ordinal);
        Assert.Contains("<Spacing>0</Spacing>", result.SpriteFont, StringComparison.Ordinal);
        Assert.Contains("<UseKerning>false</UseKerning>", result.SpriteFont, StringComparison.Ordinal);
        Assert.Contains("<Style>Regular</Style>", result.SpriteFont, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultCharacter", result.SpriteFont, StringComparison.Ordinal);
        Assert.DoesNotContain("CharacterRegions", result.SpriteFont, StringComparison.Ordinal);
    }

    [Fact]
    public void A_font_name_with_markup_characters_is_escaped()
    {
        var result = TkFontConverter.Convert(Root("<FontName>A &amp; B &lt;1&gt;</FontName>"));

        Assert.Contains("<FontName>A &amp; B &lt;1&gt;</FontName>", result.SpriteFont, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Regular", "Regular")]
    [InlineData("Bold", "Bold")]
    [InlineData("italic", "Italic")]
    [InlineData("Bold, Italic", "Bold, Italic")]
    [InlineData("Italic Bold", "Bold, Italic")]
    public void Style_keeps_bold_and_italic(string toolkit, string expected)
    {
        var result = TkFontConverter.Convert(Root($"<FontName>A</FontName><Style>{toolkit}</Style>"));

        Assert.Contains($"<Style>{expected}</Style>", result.SpriteFont, StringComparison.Ordinal);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Underline_and_strikeout_are_dropped_with_a_warning()
    {
        var result = TkFontConverter.Convert(Root("<FontName>A</FontName><Style>Bold Underline Strikeout</Style>"));

        Assert.Contains("<Style>Bold</Style>", result.SpriteFont, StringComparison.Ordinal);
        Assert.Equal(["Style 'Underline' has no MonoGame equivalent and was dropped.", "Style 'Strikeout' has no MonoGame equivalent and was dropped."], result.Warnings);
    }

    [Fact]
    public void Line_spacing_and_premultiply_with_non_default_values_are_warnings()
    {
        var result = TkFontConverter.Convert(Root("<FontName>A</FontName><LineSpacing>2</LineSpacing><NoPremultiply>true</NoPremultiply><Format>Rgba32</Format><AntiAlias>ClearType</AntiAlias>"));

        Assert.Equal(4, result.Warnings.Count);
        Assert.StartsWith("LineSpacing is '2'", result.Warnings[0], StringComparison.Ordinal);
        Assert.StartsWith("NoPremultiply is 'true'", result.Warnings[1], StringComparison.Ordinal);
        Assert.StartsWith("Format is 'Rgba32'", result.Warnings[2], StringComparison.Ordinal);
        Assert.StartsWith("AntiAlias is 'ClearType'", result.Warnings[3], StringComparison.Ordinal);
        Assert.DoesNotContain("LineSpacing", result.SpriteFont, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_values_of_the_dropped_fields_give_no_warning()
    {
        var result = TkFontConverter.Convert(Root("<FontName>A</FontName><LineSpacing>0</LineSpacing><NoPremultiply>false</NoPremultiply><Format>auto</Format><AntiAlias>Default</AntiAlias>"));

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void A_zero_default_character_means_none()
    {
        var result = TkFontConverter.Convert(Root("<FontName>A</FontName><DefaultCharacter>0</DefaultCharacter>"));

        Assert.DoesNotContain("DefaultCharacter", result.SpriteFont, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(160)]
    public void A_whitespace_default_character_is_left_out_with_a_warning(int code)
    {
        var result = TkFontConverter.Convert(Root($"<FontName>A</FontName><DefaultCharacter>{code}</DefaultCharacter>"));

        Assert.DoesNotContain("DefaultCharacter", result.SpriteFont, StringComparison.Ordinal);
        Assert.Contains("whitespace character", Assert.Single(result.Warnings), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<Other><FontName>A</FontName></Other>", "not 'TkFont'")]
    [InlineData("<XnaContent><Asset Type=\"Graphics:FontDescription\"/></XnaContent>", "not 'TkFont'")]
    [InlineData("<TkFont></TkFont>", "no FontName")]
    [InlineData("<TkFont><FontName>   </FontName></TkFont>", "no FontName")]
    [InlineData("<TkFont><FontName>A</FontName><Size>big</Size></TkFont>", "Size 'big'")]
    [InlineData("<TkFont><FontName>A</FontName><Spacing>x</Spacing></TkFont>", "Spacing 'x'")]
    [InlineData("<TkFont><FontName>A</FontName><UseKerning>maybe</UseKerning></TkFont>", "UseKerning")]
    [InlineData("<TkFont><FontName>A</FontName><CharacterRegions><CharacterRegion><Start>126</Start><End>32</End></CharacterRegion></CharacterRegions></TkFont>", "CharacterRegion")]
    [InlineData("<TkFont><FontName>A</FontName><CharacterRegions><CharacterRegion><Start>1</Start><End>32</End></CharacterRegion></CharacterRegions></TkFont>", "CharacterRegion")]
    [InlineData("<TkFont><FontName>A</FontName><CharacterRegions><CharacterRegion><Start>55296</Start><End>55300</End></CharacterRegion></CharacterRegions></TkFont>", "CharacterRegion")]
    [InlineData("<TkFont><FontName>A</FontName><DefaultCharacter>70000</DefaultCharacter></TkFont>", "DefaultCharacter")]
    [InlineData("<TkFont><FontName>A</FontName><DefaultCharacter>5</DefaultCharacter></TkFont>", "DefaultCharacter")]
    [InlineData("<TkFont><FontName>A", "not valid XML")]
    [InlineData("", "not valid XML")]
    public void A_file_that_is_not_a_usable_toolkit_font_is_rejected_with_a_clear_message(string xml, string expected)
    {
        var result = TkFontConverter.Convert(xml);

        Assert.Null(result.SpriteFont);
        Assert.Contains(expected, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_doctype_is_refused_instead_of_resolved()
    {
        var result = TkFontConverter.Convert("<!DOCTYPE TkFont [<!ENTITY x SYSTEM \"file:///c:/secret\">]><TkFont><FontName>&x;</FontName></TkFont>");

        Assert.Null(result.SpriteFont);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void The_result_reads_back_as_a_spritefont_with_the_same_family()
    {
        var check = FontChecker.Check(TkFontConverter.Convert(_full).SpriteFont!);

        Assert.Equal("Arial", check.Family);
        Assert.Null(check.Problem);
    }
}
