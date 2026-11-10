using XnaReborn.Migrator.Content;

namespace XnaReborn.Tests.Migrator.Content;

/// <summary>The XNA-only font list.</summary>
[Trait("Category", "Migrator")]
public class XnaOnlyFontsTests
{
    private static readonly string[] _expected =
    [
        "Andy", "Jing Jing", "Kootenay", "Lindsey", "Miramonte", "Moire", "Motorwerk", "News Gothic",
        "Pericles", "Pescadero", "Quartz MS", "Segoe Keycaps", "Segoe UI Mono", "Wasco Sans",
    ];

    private static readonly string[] _windowsSubstitutes =
    [
        "Arial", "Segoe UI", "Consolas", "Georgia", "Trebuchet MS", "Courier New", "Comic Sans MS", "Segoe Print", "Segoe Script",
    ];

    [Fact]
    public void Default_lists_the_confirmed_families()
    {
        Assert.Equal(1, XnaOnlyFonts.Default.Version);
        Assert.Equal(_expected, XnaOnlyFonts.Default.Fonts.Select(f => f.Family).Order(StringComparer.Ordinal).ToArray());
        Assert.NotEmpty(XnaOnlyFonts.Default.Sources);
        Assert.All(XnaOnlyFonts.Default.Sources, s => Assert.StartsWith("https://", s));
    }

    [Fact]
    public void Default_does_not_list_fonts_that_windows_ships()
    {
        Assert.False(XnaOnlyFonts.Default.TryGet("OCR A Extended", out _));
        Assert.False(XnaOnlyFonts.Default.TryGet("Segoe Print", out _));
        Assert.False(XnaOnlyFonts.Default.TryGet("Arial", out _));
    }

    [Fact]
    public void Default_entries_have_windows_substitutes_and_notes()
    {
        foreach (var font in XnaOnlyFonts.Default.Fonts)
        {
            Assert.Contains(font.Substitute, _windowsSubstitutes);
            Assert.False(string.IsNullOrWhiteSpace(font.Note), font.Family);
            Assert.False(XnaOnlyFonts.Default.TryGet(font.Substitute, out _), font.Family);
        }
    }

    [Theory]
    [InlineData("Pescadero")]
    [InlineData("pescadero")]
    [InlineData("SEGOE UI MONO")]
    [InlineData("  Kootenay ")]
    public void TryGet_is_case_insensitive(string family)
    {
        Assert.True(XnaOnlyFonts.Default.TryGet(family, out var info));
        Assert.False(string.IsNullOrEmpty(info.Substitute));
    }

    [Fact]
    public void TryGet_unknown_family_returns_false()
    {
        Assert.False(XnaOnlyFonts.Default.TryGet("Arial", out _));
        Assert.False(XnaOnlyFonts.Default.TryGet(string.Empty, out _));
    }

    [Fact]
    public void Parse_reads_a_custom_list()
    {
        var list = XnaOnlyFonts.Parse("""{ "version": 2, "sources": ["x"], "fonts": [ { "family": "Foo", "substitute": "Arial" } ] }""");

        Assert.Equal(2, list.Version);
        Assert.True(list.TryGet("foo", out var info));
        Assert.Equal(new XnaFontInfo("Foo", "Arial", null), info);
    }
}
