using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using XnaReborn.Runner.Diagnostics;

namespace XnaReborn.Runner.Build;

/// <summary>
/// Writes the result of a build into the migration manifest of a migrated copy: the <c>build</c> object of
/// <c>xnareborn-manifest.json</c> and the <c>## Build</c> section of <c>xnareborn-manifest.md</c>. These are the only
/// two files it changes, and only inside the migrated folder. The Runner does not reference the Migrator, so it edits the
/// manifest as a JSON tree and leaves every other property as it was.
/// </summary>
public sealed class ManifestBuildRecorder
{
    private const string _jsonName = "xnareborn-manifest.json";
    private const string _markdownName = "xnareborn-manifest.md";
    private const string _heading = "## Build";

    // The same options as the migrator's ManifestJsonWriter, so the file looks as if the migrator had written it.
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Records a build in the manifest files.</summary>
    /// <param name="migratedFolder">The migrated copy.</param>
    /// <param name="build">The build result.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns>A task that completes when the files are written.</returns>
    /// <exception cref="FileNotFoundException">The JSON manifest does not exist.</exception>
    /// <exception cref="InvalidDataException">The JSON manifest is not a JSON object.</exception>
    /// <exception cref="JsonException">The JSON manifest is not valid JSON.</exception>
    public async Task RecordAsync(string migratedFolder, BuildResult build, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(migratedFolder);
        ArgumentNullException.ThrowIfNull(build);

        var root = Path.GetFullPath(migratedFolder);
        var errors = ErrorLines(root, build);

        var jsonPath = Path.Combine(root, _jsonName);
        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException($"The manifest {jsonPath} does not exist.", jsonPath);
        }

        var json = await File.ReadAllTextAsync(jsonPath, cancellationToken).ConfigureAwait(false);
        var manifest = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidDataException($"{_jsonName} is not a JSON object.");
        manifest["build"] = new JsonObject
        {
            ["attempted"] = true,
            ["succeeded"] = build.Succeeded,
            ["errors"] = new JsonArray([.. errors.Select(e => (JsonNode?)JsonValue.Create(e))]),
        };
        var newJson = manifest.ToJsonString(_options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

        // Read the markdown before writing anything, so a failure to read it leaves both files as they were.
        var markdownPath = Path.Combine(root, _markdownName);
        string? newMarkdown = null;
        if (File.Exists(markdownPath))
        {
            var markdown = await File.ReadAllTextAsync(markdownPath, cancellationToken).ConfigureAwait(false);
            newMarkdown = ReplaceSection(markdown, Section(build, errors));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await WriteAtomicAsync(jsonPath, newJson).ConfigureAwait(false);
        if (newMarkdown is not null)
        {
            await WriteAtomicAsync(markdownPath, newMarkdown).ConfigureAwait(false);
        }
    }

    private static List<string> ErrorLines(string root, BuildResult build) =>
        build.Groups
            .SelectMany(g => g.Items)
            .Where(d => d.Severity == BuildDiagnosticSeverity.Error)
            .Select(d => Describe(root, d))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static string Describe(string root, BuildDiagnostic d)
    {
        var text = $"{d.Code}: {d.Message}";
        var place = d.File ?? d.AssetPath;
        if (string.IsNullOrWhiteSpace(place))
        {
            return text;
        }

        var shown = Relative(root, place);
        return d.File is not null && d.Line is { } line ? $"{text} ({shown}:{line})" : $"{text} ({shown})";
    }

    // Paths under the migrated folder are shown relative to it, so the manifest does not depend on where the copy lives.
    private static string Relative(string root, string path)
    {
        try
        {
            if (Path.IsPathRooted(path))
            {
                var relative = Path.GetRelativePath(root, path);
                if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
                {
                    return relative.Replace('\\', '/');
                }
            }
        }
        catch (ArgumentException)
        {
            // Not a usable path: show it as printed.
        }

        return path;
    }

    private static string Section(BuildResult build, List<string> errors)
    {
        var status = build.Succeeded ? "Succeeded." : errors.Count == 0 ? "Failed." : $"Failed with {errors.Count} error(s).";
        return errors.Count == 0
            ? $"{_heading}\n\n{status}"
            : $"{_heading}\n\n{status}\n\n" + string.Join('\n', errors.Select(e => "- " + e));
    }

    private static string ReplaceSection(string markdown, string section)
    {
        var crlf = markdown.Contains("\r\n", StringComparison.Ordinal);
        var text = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = text.Split('\n');
        var start = Array.FindIndex(lines, l => l.TrimEnd() == _heading);

        string result;
        if (start < 0)
        {
            var body = text.TrimEnd('\n');
            result = (body.Length == 0 ? string.Empty : body + "\n\n") + section + "\n";
        }
        else
        {
            var end = start + 1;
            while (end < lines.Length && !lines[end].StartsWith("## ", StringComparison.Ordinal))
            {
                end++;
            }

            var before = start == 0 ? string.Empty : string.Join('\n', lines[..start]) + "\n";
            var after = end < lines.Length ? "\n" + string.Join('\n', lines[end..]) : string.Empty;
            result = before + section + "\n" + after;
        }

        return crlf ? result.Replace("\n", "\r\n", StringComparison.Ordinal) : result;
    }

    // Writes a temporary file next to the target, then moves it over the target, so a failure never leaves half a manifest.
    private static async Task WriteAtomicAsync(string path, string content)
    {
        var temp = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, content, new UTF8Encoding(false)).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
