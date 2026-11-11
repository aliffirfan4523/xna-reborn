using System.Globalization;
using System.Xml.Linq;

namespace XnaReborn.App.Tests.Accessibility;

/// <summary>
/// WCAG 2.x contrast of every foreground and background token pair in Styles/Tokens.axaml, in the Light and Dark themes.
/// Text needs 4.5:1. Icons, input borders, focus rings and other parts of controls need 3:1. The pairs list where each
/// token is really drawn (see Styles/Controls.axaml and the views), so a new color that fails is fixed in the theme file.
/// </summary>
public sealed class ContrastTests
{
    private const double _text = 4.5;
    private const double _nonText = 3.0;

    private static readonly string[] _themes = ["Light", "Dark"];

    // Surfaces that normal text is drawn on.
    private static readonly string[] _pageSurfaces = ["BackgroundBrush", "SurfaceBrush", "SurfaceAltBrush", "RailBrush"];

    // Tinted backgrounds, each with the tokens that are drawn on it. Error banner is the shell's own tint.
    private static readonly (string Background, string[] Foregrounds)[] _tints =
    [
        ("AccentSoftBrush", ["TextBrush", "TextMutedBrush", "AccentTextBrush"]),
        ("SurfaceHoverBrush", ["TextBrush", "TextMutedBrush", "AccentTextBrush"]),
        ("SuccessSoftBrush", ["TextBrush", "TextMutedBrush", "SuccessTextBrush"]),
        ("InfoSoftBrush", ["TextBrush", "TextMutedBrush", "SeverityInfoBrush"]),
        ("WarningSoftBrush", ["TextBrush", "TextMutedBrush", "SeverityWarningBrush"]),
        ("ErrorSoftBrush", ["TextBrush", "TextMutedBrush", "SeverityErrorBrush", "SeverityBlockerBrush"]),
        ("ErrorBannerBackgroundBrush", ["TextBrush", "TextMutedBrush", "SeverityErrorBrush"]),
    ];

    /// <summary>Foreground, background, required ratio, what it is.</summary>
    public static IEnumerable<object[]> Pairs()
    {
        foreach (var theme in _themes)
        {
            foreach (var surface in _pageSurfaces)
            {
                foreach (var fg in new[] { "TextBrush", "TextMutedBrush", "AccentTextBrush", "SuccessTextBrush", "SeverityInfoBrush", "SeverityWarningBrush", "SeverityErrorBrush", "SeverityBlockerBrush" })
                {
                    yield return [theme, fg, surface, _text, "text"];
                }

                // Icons, input borders, focus ring, progress fill and the outline of a step circle.
                foreach (var fg in new[] { "BorderBrushStrong", "FocusRingBrush", "AccentBrush" })
                {
                    yield return [theme, fg, surface, _nonText, "non-text"];
                }
            }

            foreach (var (background, foregrounds) in _tints)
            {
                foreach (var fg in foregrounds)
                {
                    yield return [theme, fg, background, _text, "text"];
                }

                // The focus ring sits on the control, which can have any tint.
                yield return [theme, "FocusRingBrush", background, _nonText, "non-text"];
            }

            foreach (var background in new[] { "AccentBrush", "AccentHoverBrush", "AccentPressedBrush" })
            {
                yield return [theme, "OnAccentBrush", background, _text, "text"];
            }

            // The focus ring is two-tone: a 2 px FocusRingBrush line with a 1 px FocusRingInnerBrush line inside it, so one of
            // the two always stands out, whether the control is a pale card or a dark accent button.
            yield return [theme, "FocusRingBrush", "FocusRingInnerBrush", _nonText, "non-text"];
            yield return [theme, "FocusRingInnerBrush", "AccentBrush", _nonText, "non-text"];
            yield return [theme, "BorderBrushStrong", "SurfaceAltBrush", _nonText, "non-text"];
            yield return [theme, "AccentBrush", "AccentSoftBrush", _nonText, "non-text"];
            yield return [theme, "AccentBrush", "SurfaceAltBrush", _nonText, "non-text"];
            yield return [theme, "SuccessTextBrush", "SuccessSoftBrush", _nonText, "non-text"];
            yield return [theme, "ErrorBannerBorderBrush", "ErrorBannerBackgroundBrush", _nonText, "non-text"];
            yield return [theme, "ErrorBannerBorderBrush", "BackgroundBrush", _nonText, "non-text"];
        }
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Token_pair_meets_its_contrast_ratio(string theme, string foreground, string background, double required, string kind)
    {
        var colors = Load(theme);
        var ratio = Ratio(colors[foreground], colors[background]);
        Assert.True(ratio >= required, $"{theme}: {foreground} on {background} is {ratio:0.00}:1, needs {required}:1 ({kind}).");
    }

    [Fact]
    public void Every_token_color_is_opaque_so_the_ratios_above_are_exact()
    {
        foreach (var theme in _themes)
        {
            foreach (var (key, color) in Load(theme))
            {
                Assert.True(color.A == 255, $"{theme}: {key} has alpha {color.A}; contrast of a see-through color depends on what is behind it.");
            }
        }
    }

    [Fact]
    public void Both_themes_define_the_same_tokens()
    {
        Assert.Equal(Load("Light").Keys.Order(), Load("Dark").Keys.Order());
    }

    [Fact]
    public void The_build_log_terminal_colors_meet_their_ratios()
    {
        // BuildLogView paints a fixed dark panel in both themes (see Controls/BuildLogView.axaml).
        var xml = XDocument.Load(Path.Combine(AppDirectory, "Controls", "BuildLogView.axaml"));
        var brushes = xml.Descendants().Where(e => e.Name.LocalName == "SolidColorBrush")
            .ToDictionary(e => (string)e.Attribute(_x + "Key")!, e => Parse((string)e.Attribute("Color")!));
        var panel = brushes["TerminalBackground"];

        Assert.True(Ratio(brushes["TerminalText"], panel) >= _text);
        Assert.True(Ratio(brushes["TerminalMuted"], panel) >= _text);
        Assert.True(Ratio(brushes["TerminalError"], panel) >= _text);
    }

    [Fact]
    public void The_ratio_formula_matches_the_WCAG_reference_values()
    {
        Assert.Equal(21.0, Ratio(Parse("#000000"), Parse("#FFFFFF")), 2);
        Assert.Equal(1.0, Ratio(Parse("#777777"), Parse("#777777")), 2);
        Assert.Equal(4.48, Ratio(Parse("#777777"), Parse("#FFFFFF")), 2);
    }

    /// <summary>Prints every ratio, for the PR description: <c>dotnet test --filter Print_every_ratio</c> with XNAREBORN_CONTRAST_REPORT set to a file path.</summary>
    [Fact]
    public void Print_every_ratio_when_asked()
    {
        var path = Environment.GetEnvironmentVariable("XNAREBORN_CONTRAST_REPORT");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var lines = Pairs().Select(p => (string)p[0] + "\t" + (string)p[1] + "\t" + (string)p[2] + "\t" + (double)p[3] + "\t" +
            Ratio(Load((string)p[0])[(string)p[1]], Load((string)p[0])[(string)p[2]]).ToString("0.00", CultureInfo.InvariantCulture));
        File.WriteAllLines(path, lines);
    }

    private static readonly XNamespace _x = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string AppDirectory => Path.Combine(RepoPaths.Root, "src", "XnaReborn.App");

    private static Dictionary<string, (byte A, byte R, byte G, byte B)> Load(string theme)
    {
        var xml = XDocument.Load(Path.Combine(AppDirectory, "Styles", "Tokens.axaml"));
        var dictionary = xml.Descendants()
            .Where(e => e.Name.LocalName == "ResourceDictionary" && (string?)e.Attribute(_x + "Key") == theme)
            .Single();
        return dictionary.Elements()
            .Where(e => e.Name.LocalName == "SolidColorBrush")
            .ToDictionary(e => (string)e.Attribute(_x + "Key")!, e => Parse((string)e.Attribute("Color")!));
    }

    private static (byte A, byte R, byte G, byte B) Parse(string hex)
    {
        var h = hex.TrimStart('#');
        byte B(int i) => byte.Parse(h.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return h.Length == 8 ? (B(0), B(2), B(4), B(6)) : ((byte)255, B(0), B(2), B(4));
    }

    private static double Luminance((byte A, byte R, byte G, byte B) c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }

    private static double Ratio((byte A, byte R, byte G, byte B) a, (byte A, byte R, byte G, byte B) b)
    {
        var (hi, lo) = (Luminance(a), Luminance(b));
        if (hi < lo)
        {
            (hi, lo) = (lo, hi);
        }

        return (hi + 0.05) / (lo + 0.05);
    }
}
