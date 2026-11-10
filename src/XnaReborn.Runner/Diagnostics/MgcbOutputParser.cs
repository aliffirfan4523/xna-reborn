using System.Text.RegularExpressions;

namespace XnaReborn.Runner.Diagnostics;

/// <summary>
/// Reads content builder failures from the output of a MonoGame build (MonoGame.Content.Builder.Task 3.8.6.1 running
/// <c>dotnet mgcb</c>). Diagnostics get the code <c>MGCB</c> and the asset path. The real formats seen are
/// <c>path : error : Processor 'X' had unexpected failure!. System.Exception: text</c>,
/// <c>TextureImporter failed to load 'path': unknown image type</c> and <c>path(line,col): text</c>.
/// </summary>
/// <remarks>
/// The task also prints <c>error MSB3073: The command "dotnet" "mgcb" ... exited with code 1</c>. That line is
/// returned as one diagnostic with the code <c>MSB3073</c>, identical to what <see cref="MsBuildOutputParser"/> returns,
/// so running both parsers and passing the result to <see cref="DiagnosticGrouper"/> leaves a single entry.
/// </remarks>
public static partial class MgcbOutputParser
{
    /// <summary>The code given to content builder diagnostics.</summary>
    public const string Code = "MGCB";

    private const string _mgcbCommandCode = "MSB3073";

    private static readonly string[] _codeExtensions =
        [".cs", ".vb", ".fs", ".csproj", ".vbproj", ".fsproj", ".proj", ".sln", ".slnf", ".props", ".targets"];

    /// <summary>Parses build output and returns the content builder diagnostics, in the order they appear.</summary>
    /// <param name="rawOutput">Standard output and standard error of the build, in any line ending style.</param>
    /// <returns>The content builder diagnostics found.</returns>
    public static IReadOnlyList<BuildDiagnostic> Parse(string rawOutput)
    {
        ArgumentNullException.ThrowIfNull(rawOutput);

        var result = new List<BuildDiagnostic>();
        foreach (var line in MsBuildOutputParser.SplitLines(rawOutput))
        {
            if (TryParseLine(line, out var diagnostic))
            {
                result.Add(diagnostic);
            }
        }

        return result;
    }

    private static bool TryParseLine(string line, out BuildDiagnostic diagnostic)
    {
        // The task's own failure line: "error MSB3073: The command ""dotnet" "mgcb" ..." exited with code 1.
        if (MsBuildOutputParser.TryParseLine(line, out diagnostic))
        {
            return diagnostic.Code.Equals(_mgcbCommandCode, StringComparison.OrdinalIgnoreCase)
                && diagnostic.Message.Contains("mgcb", StringComparison.OrdinalIgnoreCase);
        }

        // "path : error : text" or "path(1,2): warning : text": the task rewrote an mgcb error into MSBuild form, without a code.
        var canonical = CodelessLine().Match(line);
        if (canonical.Success && !IsCodeFile(canonical.Groups["file"].Value))
        {
            var (message, project) = MsBuildOutputParser.SplitProject(canonical.Groups["msg"].Value);
            var path = canonical.Groups["file"].Value.Trim();
            diagnostic = new BuildDiagnostic(
                MsBuildOutputParser.ParseSeverity(canonical.Groups["sev"].Value),
                Code,
                message,
                path,
                MsBuildOutputParser.ParseNumber(canonical.Groups["line"]),
                MsBuildOutputParser.ParseNumber(canonical.Groups["col"]),
                project,
                path);
            return true;
        }

        // "TextureImporter failed to load 'path': unknown image type" is printed as is.
        var failed = ImporterFailure().Match(line);
        if (failed.Success)
        {
            var path = failed.Groups["path"].Value;
            diagnostic = new BuildDiagnostic(BuildDiagnosticSeverity.Error, Code, line.Trim(), path, null, null, null, path);
            return true;
        }

        // "path(1,41): Could not resolve type ..." is how mgcb reports a content file it cannot read.
        var positioned = PositionedLine().Match(line);
        if (positioned.Success
            && !IsCodeFile(positioned.Groups["file"].Value)
            && !StartsWithSeverity(positioned.Groups["msg"].Value))
        {
            var path = positioned.Groups["file"].Value.Trim();
            diagnostic = new BuildDiagnostic(
                BuildDiagnosticSeverity.Error,
                Code,
                positioned.Groups["msg"].Value.Trim(),
                path,
                MsBuildOutputParser.ParseNumber(positioned.Groups["line"]),
                MsBuildOutputParser.ParseNumber(positioned.Groups["col"]),
                null,
                path);
            return true;
        }

        diagnostic = null!;
        return false;
    }

    private static bool IsCodeFile(string path)
    {
        var trimmed = path.Trim();
        return _codeExtensions.Any(e => trimmed.EndsWith(e, StringComparison.OrdinalIgnoreCase));
    }

    private static bool StartsWithSeverity(string text) =>
        text.StartsWith("error", StringComparison.OrdinalIgnoreCase) || text.StartsWith("warning", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(
        @"^\s*(?:\d+(?::\d+)?>)?\s*(?<file>.+?)(?:\((?<line>\d+)(?:,(?<col>\d+))?\))?\s*:\s*(?<sev>error|warning)\s*:\s*(?<msg>.+?)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex CodelessLine();

    [GeneratedRegex(
        @"^\s*\w+ failed to \w+ '(?<path>[^']+)':\s*.+$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ImporterFailure();

    [GeneratedRegex(
        @"^\s*(?<file>\S.*?)\((?<line>\d+),(?<col>\d+)\):\s*(?<msg>\S.*)$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex PositionedLine();
}
