using XnaReborn.Core.Models;

namespace XnaReborn.Core.Mgcb;

/// <summary>
/// Turns the text of a Content.mgcb file into an <see cref="MgcbFile"/>. It touches no disk and never throws on bad input.
/// A line that cannot be read becomes an ANA010 diagnostic and parsing goes on.
/// </summary>
public static class MgcbParser
{
    /// <summary>Diagnostic code for a line that could not be read.</summary>
    public const string MalformedLineCode = "ANA010";

    private const string _beginMarker = "#begin";

    /// <summary>Parses mgcb text. Both LF and CRLF line endings are accepted.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="file">Relative path used as the diagnostic file, or null.</param>
    /// <returns>The parsed file.</returns>
    public static MgcbFile Parse(string text, string? file = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var settings = new List<MgcbSetting>();
        var references = new List<MgcbReference>();
        var entries = new List<MgcbEntry>();
        var diagnostics = new List<Diagnostic>();

        // State for the entry being described. A /build or /copy line closes it.
        string? importer = null;
        string? processor = null;
        var parameters = new List<MgcbParameter>();
        var beginLine = 0;

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith(_beginMarker, StringComparison.OrdinalIgnoreCase)
                && (line.Length == _beginMarker.Length || char.IsWhiteSpace(line[_beginMarker.Length])))
            {
                importer = null;
                processor = null;
                parameters = [];
                beginLine = lineNumber;
                continue;
            }

            if (line[0] == '#')
            {
                continue;
            }

            if (line[0] != '/' || line.Length == 1)
            {
                diagnostics.Add(Malformed(lineNumber, line, file));
                continue;
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            var name = (colon < 0 ? line[1..] : line[1..colon]).Trim();
            string? value = colon < 0 ? null : line[(colon + 1)..].Trim();
            if (name.Length == 0)
            {
                diagnostics.Add(Malformed(lineNumber, line, file));
                continue;
            }

            switch (name.ToLowerInvariant())
            {
                case "reference":
                    if (string.IsNullOrEmpty(value))
                    {
                        diagnostics.Add(Malformed(lineNumber, line, file));
                    }
                    else
                    {
                        references.Add(new MgcbReference(value, lineNumber));
                    }

                    break;
                case "importer":
                    importer = value;
                    break;
                case "processor":
                    processor = value;
                    break;
                case "processorparam":
                    if (TryParameter(value, lineNumber) is { } parameter)
                    {
                        parameters.Add(parameter);
                    }
                    else
                    {
                        diagnostics.Add(Malformed(lineNumber, line, file));
                    }

                    break;
                case "build":
                case "copy":
                    if (string.IsNullOrEmpty(value))
                    {
                        diagnostics.Add(Malformed(lineNumber, line, file));
                        break;
                    }

                    var semicolon = value.IndexOf(';', StringComparison.Ordinal);
                    entries.Add(new MgcbEntry(
                        semicolon < 0 ? value : value[..semicolon],
                        semicolon < 0 ? null : value[(semicolon + 1)..],
                        string.Equals(name, "copy", StringComparison.OrdinalIgnoreCase),
                        importer,
                        processor,
                        parameters,
                        beginLine > 0 ? beginLine : lineNumber));
                    importer = null;
                    processor = null;
                    parameters = [];
                    beginLine = 0;
                    break;
                default:
                    settings.Add(new MgcbSetting(name, value, lineNumber));
                    break;
            }
        }

        return new MgcbFile(settings, references, entries, diagnostics);
    }

    private static MgcbParameter? TryParameter(string? value, int line)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var equals = value.IndexOf('=', StringComparison.Ordinal);
        if (equals <= 0)
        {
            return null;
        }

        return new MgcbParameter(value[..equals].Trim(), value[(equals + 1)..].Trim(), line);
    }

    private static Diagnostic Malformed(int line, string text, string? file)
    {
        var shown = text.Length > 80 ? text[..80] + "..." : text;
        return new Diagnostic(MalformedLineCode, $"Content.mgcb line {line} could not be read: {shown}", file);
    }
}
