using System.Xml;

namespace XnaReborn.Migrator.Content;

/// <summary>The result of checking one <c>.spritefont</c> file.</summary>
/// <param name="Family">The first <c>FontName</c> value, or null when it could not be read.</param>
/// <param name="IsXnaOnly">True when the family is on the XNA-only list.</param>
/// <param name="Substitute">The suggested Windows font when <paramref name="IsXnaOnly"/> is true.</param>
/// <param name="Problem">Why the file could not be read, or null.</param>
public sealed record FontCheckResult(string? Family, bool IsXnaOnly, string? Substitute, string? Problem);

/// <summary>Reads the font family of a <c>.spritefont</c> and checks it against the XNA-only list.</summary>
public static class FontChecker
{
    /// <summary>Checks the text of a <c>.spritefont</c> file against <see cref="XnaOnlyFonts.Default"/>.</summary>
    /// <param name="spriteFontText">The file text.</param>
    /// <returns>The family and whether it is XNA-only.</returns>
    public static FontCheckResult Check(string spriteFontText) => Check(spriteFontText, XnaOnlyFonts.Default);

    /// <summary>Checks the text of a <c>.spritefont</c> file against a given list.</summary>
    /// <param name="spriteFontText">The file text.</param>
    /// <param name="fonts">The XNA-only list.</param>
    /// <returns>The family and whether it is XNA-only.</returns>
    public static FontCheckResult Check(string spriteFontText, XnaOnlyFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(spriteFontText);
        ArgumentNullException.ThrowIfNull(fonts);

        string? family = null;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var text = new StringReader(spriteFontText);
            using var reader = XmlReader.Create(text, settings);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "FontName")
                {
                    family = reader.ReadElementContentAsString().Trim();
                    break;
                }
            }
        }
        catch (XmlException ex)
        {
            return new FontCheckResult(null, false, null, "The file is not valid XML: " + ex.Message);
        }

        if (string.IsNullOrEmpty(family))
        {
            return new FontCheckResult(null, false, null, family is null ? "The file has no FontName element." : "The FontName element is empty.");
        }

        return fonts.TryGet(family, out var info)
            ? new FontCheckResult(family, true, info.Substitute, null)
            : new FontCheckResult(family, false, null, null);
    }
}
