using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace XnaReborn.Migrator.Content;

/// <summary>The outcome of converting one SharpDX Toolkit font file.</summary>
/// <param name="SpriteFont">The text of the <c>.spritefont</c> file, or null when the file could not be converted.</param>
/// <param name="Warnings">Settings that have no MonoGame field and are not at their default, one sentence each, in a fixed order.</param>
/// <param name="Error">Why the file could not be converted, or null.</param>
public sealed record TkFontConversion(string? SpriteFont, IReadOnlyList<string> Warnings, string? Error);

/// <summary>
/// Converts a <c>TkFont</c> XML file (the source of the Toolkit <c>ToolkitFont</c> build action) to the <c>.spritefont</c> file that the
/// MonoGame content builder reads. A pure function: text in, text out.
/// </summary>
/// <remarks>
/// Fields copied: FontName, Size, Spacing, UseKerning, Style (Bold and Italic), CharacterRegions (numbers become characters) and
/// DefaultCharacter (a number becomes a character). Fields with no MonoGame counterpart: LineSpacing, Format, NoPremultiply, AntiAlias
/// and the Underline and Strikeout styles. A non-default value of one of them is returned as a warning.
/// Defaults are those of the Toolkit <c>FontDescription</c>: Size 23, Spacing 0, LineSpacing 0, UseKerning false, Format auto, Style regular.
/// </remarks>
public static class TkFontConverter
{
    private const string _defaultSize = "23";
    private const int _firstSupportedCharacter = 0x20;

    /// <summary>Converts one file.</summary>
    /// <param name="tkFontXml">The text of the <c>TkFont</c> file.</param>
    /// <returns>The converted file and the warnings, or an error with no file.</returns>
    public static TkFontConversion Convert(string tkFontXml)
    {
        ArgumentNullException.ThrowIfNull(tkFontXml);
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(tkFontXml), settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            return Fail("The file is not valid XML: " + ex.Message);
        }

        var root = document.Root;
        if (root is null || root.Name.LocalName != "TkFont")
        {
            return Fail($"The root element is '{root?.Name.LocalName ?? "(none)"}', not 'TkFont', so this is not a Toolkit font file.");
        }

        var fontName = Text(root, "FontName");
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return Fail("The file has no FontName element, or it is empty.");
        }

        var warnings = new List<string>();
        if (!TryFloat(Text(root, "Size") ?? _defaultSize, out var size, "Size", out var error)
            || !TryFloat(Text(root, "Spacing") ?? "0", out var spacing, "Spacing", out error))
        {
            return Fail(error!);
        }

        if (!TryBool(Text(root, "UseKerning"), false, out var kerning))
        {
            return Fail($"UseKerning '{Text(root, "UseKerning")}' is not true or false.");
        }

        var regions = new List<(int Start, int End)>();
        foreach (var region in root.Elements().Where(e => e.Name.LocalName == "CharacterRegions").Elements().Where(e => e.Name.LocalName == "CharacterRegion"))
        {
            if (!TryCharacter(Text(region, "Start"), out var start) || !TryCharacter(Text(region, "End"), out var end) || end < start)
            {
                return Fail("A CharacterRegion needs numeric Start and End values in order, between 32 and 65535 and outside the surrogate range.");
            }

            regions.Add((start, end));
        }

        var defaultCharacter = Text(root, "DefaultCharacter");
        int? fallback = null;
        if (!string.IsNullOrWhiteSpace(defaultCharacter))
        {
            if (!int.TryParse(defaultCharacter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) || code is < 0 or > 0xFFFF)
            {
                return Fail($"DefaultCharacter '{defaultCharacter}' is not a number from 0 to 65535.");
            }

            if (code != 0)
            {
                if (!IsXmlCharacter(code))
                {
                    return Fail($"DefaultCharacter {code} cannot be written in a .spritefont file.");
                }

                // The MonoGame font reader trims the text of this element, so a space cannot be written (checked with dotnet-mgcb 3.8.6.1: it fails with
                // "Not have enough entries in space-separated list!"). The element is left out and the author is told.
                if (char.IsWhiteSpace((char)code))
                {
                    warnings.Add($"DefaultCharacter is the whitespace character {code}, which a MonoGame font file cannot hold. It was left out. A character that is not in CharacterRegions now throws when it is drawn; set a visible DefaultCharacter if that matters.");
                }
                else
                {
                    fallback = code;
                }
            }
        }

        var (style, styleWarnings) = Style(Text(root, "Style"));
        warnings.AddRange(styleWarnings);
        AddWarning(warnings, "LineSpacing", Text(root, "LineSpacing"), "0", "MonoGame has no line spacing field. Text may be spaced differently.");
        AddWarning(warnings, "NoPremultiply", Text(root, "NoPremultiply"), "false", "MonoGame font textures are always premultiplied. Text edges may look different.");
        AddWarning(warnings, "Format", Text(root, "Format"), "Auto", "MonoGame chooses the font texture format itself.");
        AddWarning(warnings, "AntiAlias", Text(root, "AntiAlias"), "Default", "MonoGame has no anti-alias setting for fonts.");

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
        sb.Append("<XnaContent xmlns:Graphics=\"Microsoft.Xna.Framework.Content.Pipeline.Graphics\">\n");
        sb.Append("  <Asset Type=\"Graphics:FontDescription\">\n");
        sb.Append("    <FontName>").Append(Escape(fontName.Trim())).Append("</FontName>\n");
        sb.Append("    <Size>").Append(Format(size)).Append("</Size>\n");
        sb.Append("    <Spacing>").Append(Format(spacing)).Append("</Spacing>\n");
        sb.Append("    <UseKerning>").Append(kerning ? "true" : "false").Append("</UseKerning>\n");
        sb.Append("    <Style>").Append(style).Append("</Style>\n");
        if (fallback is { } code2)
        {
            sb.Append("    <DefaultCharacter>&#").Append(code2.ToString(CultureInfo.InvariantCulture)).Append(";</DefaultCharacter>\n");
        }

        if (regions.Count > 0)
        {
            sb.Append("    <CharacterRegions>\n");
            foreach (var (start, end) in regions)
            {
                sb.Append("      <CharacterRegion>\n");
                sb.Append("        <Start>&#").Append(start.ToString(CultureInfo.InvariantCulture)).Append(";</Start>\n");
                sb.Append("        <End>&#").Append(end.ToString(CultureInfo.InvariantCulture)).Append(";</End>\n");
                sb.Append("      </CharacterRegion>\n");
            }

            sb.Append("    </CharacterRegions>\n");
        }

        sb.Append("  </Asset>\n");
        sb.Append("</XnaContent>\n");
        return new TkFontConversion(sb.ToString(), warnings, null);
    }

    private static TkFontConversion Fail(string error) => new(null, [], error);

    private static string? Text(XElement parent, string name) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim();

    private static bool TryFloat(string text, out float value, string name, out string? error)
    {
        error = null;
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value))
        {
            return true;
        }

        error = $"{name} '{text}' is not a number.";
        return false;
    }

    private static bool TryBool(string? text, bool fallback, out bool value)
    {
        value = fallback;
        return string.IsNullOrWhiteSpace(text) || bool.TryParse(text, out value);
    }

    private static bool TryCharacter(string? text, out int code) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out code) && IsXmlCharacter(code);

    // A numeric character reference must name a character XML 1.0 allows. Control characters below 32 and the surrogate range are out.
    private static bool IsXmlCharacter(int code) =>
        code is >= _firstSupportedCharacter and <= 0xFFFF and not (>= 0xD800 and <= 0xDFFF) and not 0xFFFE and not 0xFFFF;

    private static (string Style, List<string> Warnings) Style(string? text)
    {
        var warnings = new List<string>();
        var bold = false;
        var italic = false;
        foreach (var token in (text ?? string.Empty).Split([' ', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (token.ToUpperInvariant())
            {
                case "BOLD":
                    bold = true;
                    break;
                case "ITALIC":
                    italic = true;
                    break;
                case "REGULAR":
                    break;
                default:
                    warnings.Add($"Style '{token}' has no MonoGame equivalent and was dropped.");
                    break;
            }
        }

        var style = bold && italic ? "Bold, Italic" : bold ? "Bold" : italic ? "Italic" : "Regular";
        return (style, warnings);
    }

    private static void AddWarning(List<string> warnings, string name, string? value, string defaultValue, string consequence)
    {
        if (!string.IsNullOrWhiteSpace(value) && !IsDefault(value, defaultValue))
        {
            warnings.Add($"{name} is '{value}' (default '{defaultValue}'). {consequence}");
        }
    }

    private static bool IsDefault(string value, string defaultValue) =>
        string.Equals(value, defaultValue, StringComparison.OrdinalIgnoreCase)
        || (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) && float.TryParse(defaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && a == b);

    private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}
