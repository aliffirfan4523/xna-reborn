using System.Text;

namespace XnaReborn.Migrator.Content;

/// <summary>Replaces the font family in the text of a <c>.spritefont</c> file without touching any other byte.</summary>
public static class FontSubstituter
{
    /// <summary>Replaces the inner text of the first <c>FontName</c> element.</summary>
    /// <param name="spriteFontText">The file text.</param>
    /// <param name="newFamily">The new family name.</param>
    /// <returns>The text with only that element's content changed.</returns>
    /// <exception cref="InvalidOperationException">The text has no <c>FontName</c> element with content.</exception>
    public static string Substitute(string spriteFontText, string newFamily)
    {
        ArgumentNullException.ThrowIfNull(spriteFontText);
        ArgumentException.ThrowIfNullOrWhiteSpace(newFamily);

        var text = spriteFontText;
        var i = 0;
        while (i < text.Length)
        {
            var lt = text.IndexOf('<', i);
            if (lt < 0)
            {
                break;
            }

            if (StartsWith(text, lt, "<!--"))
            {
                i = SkipPast(text, lt + 4, "-->");
            }
            else if (StartsWith(text, lt, "<![CDATA["))
            {
                i = SkipPast(text, lt + 9, "]]>");
            }
            else if (StartsWith(text, lt, "<?"))
            {
                i = SkipPast(text, lt + 2, "?>");
            }
            else if (StartsWith(text, lt, "<!"))
            {
                i = SkipPast(text, lt + 2, ">");
            }
            else
            {
                var gt = FindTagEnd(text, lt);
                if (gt < 0)
                {
                    break;
                }

                if (IsFontNameStart(text, lt, gt))
                {
                    if (text[gt - 1] == '/')
                    {
                        throw new InvalidOperationException("The FontName element is empty.");
                    }

                    var close = text.IndexOf('<', gt + 1);
                    if (close < 0)
                    {
                        break;
                    }

                    return string.Concat(text.AsSpan(0, gt + 1), Escape(newFamily.Trim()), text.AsSpan(close));
                }

                i = gt + 1;
            }
        }

        throw new InvalidOperationException("The file has no FontName element.");
    }

    private static bool StartsWith(string text, int index, string value) =>
        string.CompareOrdinal(text, index, value, 0, value.Length) == 0;

    private static int SkipPast(string text, int from, string end)
    {
        var at = text.IndexOf(end, from, StringComparison.Ordinal);
        return at < 0 ? text.Length : at + end.Length;
    }

    private static int FindTagEnd(string text, int lt)
    {
        char quote = '\0';
        for (var k = lt + 1; k < text.Length; k++)
        {
            var c = text[k];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                return k;
            }
        }

        return -1;
    }

    private static bool IsFontNameStart(string text, int lt, int gt)
    {
        var start = lt + 1;
        var end = start;
        while (end < gt && !char.IsWhiteSpace(text[end]) && text[end] != '/')
        {
            end++;
        }

        var name = text.AsSpan(start, end - start);
        var colon = name.LastIndexOf(':');
        if (colon >= 0)
        {
            name = name[(colon + 1)..];
        }

        return name.SequenceEqual("FontName");
    }

    private static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            sb.Append(c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                _ => c.ToString(),
            });
        }

        return sb.ToString();
    }
}
