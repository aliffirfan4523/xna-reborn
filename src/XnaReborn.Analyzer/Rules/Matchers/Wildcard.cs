namespace XnaReborn.Analyzer.Rules.Matchers;

/// <summary>Case-insensitive wildcard comparison. <c>*</c> matches any characters, <c>?</c> matches exactly one.</summary>
internal static class Wildcard
{
    /// <summary>Returns true when the pattern contains <c>*</c> or <c>?</c>.</summary>
    /// <param name="pattern">Pattern to inspect.</param>
    public static bool HasWildcard(string pattern) => pattern.AsSpan().IndexOfAny('*', '?') >= 0;

    /// <summary>Returns true when <paramref name="text"/> matches <paramref name="pattern"/>, ignoring case.</summary>
    /// <param name="pattern">Pattern that may contain <c>*</c> and <c>?</c>.</param>
    /// <param name="text">Text to test.</param>
    public static bool IsMatch(string pattern, string text)
    {
        var p = 0;
        var t = 0;
        var star = -1;
        var resume = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                resume = t;
            }
            else if (p < pattern.Length && (pattern[p] == '?' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(text[t])))
            {
                p++;
                t++;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++resume;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
