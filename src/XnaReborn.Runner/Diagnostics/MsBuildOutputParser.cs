using System.Text.RegularExpressions;

namespace XnaReborn.Runner.Diagnostics;

/// <summary>
/// Reads the error and warning lines that MSBuild, the C# compiler, NuGet and the .NET SDK print, such as
/// <c>C:\x\Game1.cs(42,17): error CS0246: The type ... [C:\x\Game.csproj]</c>. Lines that do not look like a
/// diagnostic are ignored; the caller keeps the raw output.
/// </summary>
public static partial class MsBuildOutputParser
{
    /// <summary>Parses build output into diagnostics, in the order they appear. Repeats are kept; see <see cref="DiagnosticGrouper"/>.</summary>
    /// <param name="rawOutput">Standard output and standard error of the build, in any line ending style.</param>
    /// <returns>The error and warning diagnostics found.</returns>
    public static IReadOnlyList<BuildDiagnostic> Parse(string rawOutput)
    {
        ArgumentNullException.ThrowIfNull(rawOutput);

        var result = new List<BuildDiagnostic>();
        foreach (var line in SplitLines(rawOutput))
        {
            if (TryParseLine(line, out var diagnostic))
            {
                result.Add(diagnostic);
            }
        }

        return result;
    }

    /// <summary>Splits text into lines on LF, CRLF or CR.</summary>
    internal static string[] SplitLines(string text) => text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

    /// <summary>Parses one line of the canonical form <c>origin(line,col): error CODE: message [project]</c>.</summary>
    internal static bool TryParseLine(string line, out BuildDiagnostic diagnostic)
    {
        diagnostic = null!;
        var match = DiagnosticLine().Match(line);
        if (!match.Success)
        {
            return false;
        }

        var (message, project) = SplitProject(match.Groups["msg"].Value);
        var origin = match.Groups["file"].Success ? match.Groups["file"].Value.Trim() : null;
        diagnostic = new BuildDiagnostic(
            ParseSeverity(match.Groups["sev"].Value),
            match.Groups["code"].Value,
            message,
            LooksLikeFile(origin) ? origin : null,
            ParseNumber(match.Groups["line"]),
            ParseNumber(match.Groups["col"]),
            project,
            null);
        return true;
    }

    /// <summary>Separates the trailing <c>[project]</c> that MSBuild appends, if any.</summary>
    internal static (string Message, string? Project) SplitProject(string text)
    {
        var match = TrailingProject().Match(text);
        return match.Success ? (match.Groups["msg"].Value.TrimEnd(), match.Groups["proj"].Value) : (text.Trim(), null);
    }

    internal static BuildDiagnosticSeverity ParseSeverity(string text) =>
        text.Equals("warning", StringComparison.OrdinalIgnoreCase) ? BuildDiagnosticSeverity.Warning : BuildDiagnosticSeverity.Error;

    internal static int? ParseNumber(Group group) =>
        group.Success && int.TryParse(group.Value, out var number) ? number : null;

    // "MSBUILD" and "CSC" name a tool, not a file. A file has a folder separator or an extension.
    private static bool LooksLikeFile(string? origin) =>
        !string.IsNullOrWhiteSpace(origin)
        && (origin.Contains('\\', StringComparison.Ordinal) || origin.Contains('/', StringComparison.Ordinal) || origin.Contains('.', StringComparison.Ordinal));

    [GeneratedRegex(
        @"^\s*(?:\d+(?::\d+)?>)?\s*(?:(?<file>.+?)(?:\((?<line>\d+)(?:,(?<col>\d+))?(?:,\d+,\d+)?\))?\s*:\s*)?(?<sev>error|warning)\s+(?<code>[A-Za-z]{1,10}\d{1,6})\s*:\s*(?<msg>.*?)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex DiagnosticLine();

    // The project path ends the line in square brackets. A multi-targeting build adds "::TargetFramework=net8.0" inside them.
    [GeneratedRegex(
        @"^(?<msg>.*?)\s*\[(?<proj>[^\[\]]+?\.(?:[A-Za-z]*proj|slnf?|targets|props))(?:::[^\[\]]*)?\]\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex TrailingProject();
}
