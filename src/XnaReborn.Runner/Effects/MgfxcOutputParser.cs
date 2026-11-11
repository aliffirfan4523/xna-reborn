using System.Text.RegularExpressions;
using XnaReborn.Runner.Diagnostics;

namespace XnaReborn.Runner.Effects;

/// <summary>
/// Reads the output of <c>mgfxc</c> (the MonoGame effect compiler, 3.8.6.1). Formats seen in real runs:
/// <c>path(line,col[,line,col]): error X4576: text</c> from the HLSL compiler,
/// <c>path,(line,col) : Unexpected token ...</c> from the MonoGame effect parser (no code, so it gets <c>MGFX0001</c>) and
/// <c>Invalid profile 'x'. Vertex shader 'F' must be SM 4.0 level 9.1 or higher!</c> (no code, so it gets <c>MGFX0002</c>).
/// The summary lines (<c>Failed to parse ...</c>, <c>Unexpected error compiling ...</c>, <c>Compiled ... to ...</c>) are not diagnostics.
/// </summary>
public static partial class MgfxcOutputParser
{
    /// <summary>The code given to errors from the effect parser.</summary>
    public const string ParseErrorCode = "MGFX0001";

    /// <summary>The code given to shader profile errors.</summary>
    public const string ProfileErrorCode = "MGFX0002";

    [GeneratedRegex(@"^(?<path>.+?)\((?<l>\d+),(?<c>\d+)(?:,\d+,\d+)?\): (?<sev>error|warning) (?<code>[A-Z]+\d+): (?<msg>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex HlslLine();

    [GeneratedRegex(@"^(?<path>.+?),\((?<l>\d+),(?<c>\d+)\) : (?<msg>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ParserLine();

    [GeneratedRegex(@"^Invalid profile '(?<m>.*)'\. (?<kind>Vertex|Pixel) shader '(?<fn>.+)' must be (?<req>.*)!$", RegexOptions.CultureInvariant)]
    private static partial Regex ProfileLine();

    /// <summary>Parses compiler output. The same error is often printed twice; repeats are removed.</summary>
    /// <param name="output">Standard output and standard error, in any line ending style.</param>
    /// <param name="effectPath">The effect that was compiled, used as the asset path of profile errors that name no file.</param>
    /// <returns>The diagnostics in the order they first appear.</returns>
    public static IReadOnlyList<BuildDiagnostic> Parse(string output, string? effectPath = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        var result = new List<BuildDiagnostic>();
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            BuildDiagnostic? diagnostic = null;
            if (HlslLine().Match(line) is { Success: true } hlsl)
            {
                var path = hlsl.Groups["path"].Value;
                diagnostic = new BuildDiagnostic(
                    hlsl.Groups["sev"].Value == "error" ? BuildDiagnosticSeverity.Error : BuildDiagnosticSeverity.Warning,
                    hlsl.Groups["code"].Value,
                    hlsl.Groups["msg"].Value,
                    path,
                    int.Parse(hlsl.Groups["l"].Value, System.Globalization.CultureInfo.InvariantCulture),
                    int.Parse(hlsl.Groups["c"].Value, System.Globalization.CultureInfo.InvariantCulture),
                    null,
                    path);
            }
            else if (ParserLine().Match(line) is { Success: true } parser)
            {
                var path = parser.Groups["path"].Value;
                diagnostic = new BuildDiagnostic(
                    BuildDiagnosticSeverity.Error,
                    ParseErrorCode,
                    parser.Groups["msg"].Value,
                    path,
                    int.Parse(parser.Groups["l"].Value, System.Globalization.CultureInfo.InvariantCulture),
                    int.Parse(parser.Groups["c"].Value, System.Globalization.CultureInfo.InvariantCulture),
                    null,
                    path);
            }
            else if (ProfileLine().IsMatch(line))
            {
                diagnostic = new BuildDiagnostic(BuildDiagnosticSeverity.Error, ProfileErrorCode, line, null, null, null, null, effectPath);
            }

            if (diagnostic is not null && !result.Contains(diagnostic))
            {
                result.Add(diagnostic);
            }
        }

        return result;
    }
}
