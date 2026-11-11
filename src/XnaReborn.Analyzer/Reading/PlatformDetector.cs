using System.Text.RegularExpressions;
using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reading;

/// <summary>Finds the device platform an XNA project was built for. Pure: reads values the project reader already collected.</summary>
internal static partial class PlatformDetector
{
    /// <summary>Picks the platform from <c>XnaPlatform</c>, or guesses it when that property is missing.</summary>
    /// <param name="xnaPlatform">The <c>XnaPlatform</c> property value, or null.</param>
    /// <param name="references">Referenced assembly simple names.</param>
    /// <param name="defines">Preprocessor symbols defined by the project.</param>
    /// <param name="version">The detected XNA version.</param>
    /// <returns>The platform, and whether it was inferred instead of stated.</returns>
    internal static (XnaPlatform Platform, bool Inferred) Detect(
        string? xnaPlatform,
        IReadOnlyList<string> references,
        IReadOnlyList<string> defines,
        XnaVersion version)
    {
        if (!string.IsNullOrWhiteSpace(xnaPlatform))
        {
            return (Parse(xnaPlatform), false);
        }

        if (references.Any(r => r.Equals("Microsoft.Phone", StringComparison.OrdinalIgnoreCase)
            || r.Equals("Microsoft.Devices.Sensors", StringComparison.OrdinalIgnoreCase)))
        {
            return (XnaPlatform.WindowsPhone, true);
        }

        if (version is XnaVersion.V30 or XnaVersion.V31 && defines.Contains("ZUNE", StringComparer.Ordinal))
        {
            return (XnaPlatform.Zune, true);
        }

        return (XnaPlatform.Unknown, false);
    }

    [GeneratedRegex(@"^\s*Windows\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WindowsPattern();

    [GeneratedRegex(@"^\s*Xbox ?360\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex XboxPattern();

    [GeneratedRegex(@"^\s*Zune\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ZunePattern();

    [GeneratedRegex(@"^\s*Windows ?Phone\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PhonePattern();

    private static XnaPlatform Parse(string text)
    {
        if (WindowsPattern().IsMatch(text))
        {
            return XnaPlatform.Windows;
        }

        if (XboxPattern().IsMatch(text))
        {
            return XnaPlatform.Xbox360;
        }

        if (ZunePattern().IsMatch(text))
        {
            return XnaPlatform.Zune;
        }

        return PhonePattern().IsMatch(text) ? XnaPlatform.WindowsPhone : XnaPlatform.Unknown;
    }
}
