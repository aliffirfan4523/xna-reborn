using System.Text.RegularExpressions;

namespace XnaReborn.Analyzer.Rules.Matchers;

/// <summary>Regular expression test shared by the matchers: case-insensitive, culture-invariant, one-second timeout. A bad or slow pattern does not match.</summary>
internal static class TextPattern
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(1);

    /// <summary>Returns true when <paramref name="pattern"/> matches somewhere in <paramref name="text"/>.</summary>
    /// <param name="pattern">A .NET regular expression.</param>
    /// <param name="text">Text to test.</param>
    public static bool IsMatch(string pattern, string text)
    {
        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, _timeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
