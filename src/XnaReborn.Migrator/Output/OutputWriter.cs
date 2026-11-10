using System.Text;

namespace XnaReborn.Migrator.Output;

/// <summary>What <see cref="OutputWriter"/> did.</summary>
/// <param name="FilesWritten">Text files written (Created, Modified, and ManualWork with new text).</param>
/// <param name="FilesCopied">Files copied byte for byte from the source.</param>
/// <param name="FilesSkipped">Changes that wrote nothing.</param>
/// <param name="BytesWritten">Total bytes written to the staging folder by text writes and copies.</param>
public sealed record WriteSummary(int FilesWritten, int FilesCopied, int FilesSkipped, long BytesWritten);

/// <summary>
/// Writes planned changes into the staging folder of an <see cref="OutputSession"/>.
/// It never writes outside the staging folder and only ever opens source files for reading.
/// </summary>
public sealed class OutputWriter
{
    private const char _bom = '\uFEFF';

    private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Writes every change into <see cref="OutputSession.StagingFolder"/>.</summary>
    /// <param name="session">The open output session.</param>
    /// <param name="sourceRoot">The original project folder. It is only read.</param>
    /// <param name="changes">The planned changes.</param>
    /// <param name="cancellationToken">Checked between files.</param>
    /// <returns>Counts of what was written.</returns>
    /// <exception cref="ArgumentException">A relative path is empty, rooted, escapes the staging folder, or collides with another. Nothing is written.</exception>
    /// <exception cref="InvalidOperationException">A change has no usable content or its source path is missing or outside <paramref name="sourceRoot"/>. Nothing is written.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested. Files already written stay in staging.</exception>
    public Task<WriteSummary> WriteAsync(
        OutputSession session,
        string sourceRoot,
        IReadOnlyList<FileChange> changes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);

        var staging = Path.TrimEndingDirectorySeparator(Path.GetFullPath(session.StagingFolder));
        var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceRoot));
        var plan = Validate(staging, source, changes);
        return RunAsync(plan, cancellationToken);
    }

    private static async Task<WriteSummary> RunAsync(List<Planned> plan, CancellationToken cancellationToken)
    {
        int written = 0, copied = 0, skipped = 0;
        long bytes = 0;

        foreach (var item in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item.Change.Kind == ChangeKind.Skipped)
            {
                skipped++;
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(item.Target)!);

            if (item.Change.NewText is { } text)
            {
                var content = ToBytes(item.Change, text);
                await File.WriteAllBytesAsync(item.Target, content, cancellationToken).ConfigureAwait(false);
                written++;
                bytes += content.Length;
            }
            else
            {
                await using var input = new FileStream(item.SourceFull!, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using var output = new FileStream(item.Target, FileMode.Create, FileAccess.Write, FileShare.None);
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                copied++;
                bytes += output.Length;
            }
        }

        return new WriteSummary(written, copied, skipped, bytes);
    }

    private static byte[] ToBytes(FileChange change, string text)
    {
        var keepBom = change.Kind != ChangeKind.Created && change.OriginalText is { Length: > 0 } original && original[0] == _bom;
        var body = _utf8.GetBytes(text.TrimStart(_bom));
        if (!keepBom)
        {
            return body;
        }

        var preamble = Encoding._utf8.GetPreamble();
        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    private static List<Planned> Validate(string staging, string source, IReadOnlyList<FileChange> changes)
    {
        var plan = new List<Planned>(changes.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stagingPrefix = staging + Path.DirectorySeparatorChar;

        // Pass 1: every path. Nothing is written if any path is bad.
        foreach (var change in changes)
        {
            ArgumentNullException.ThrowIfNull(change);
            var rel = change.RelativePath;
            if (string.IsNullOrWhiteSpace(rel))
            {
                throw new ArgumentException("A change has an empty relative path.", nameof(changes));
            }

            if (Path.IsPathRooted(rel) || rel[0] is '/' or '\\')
            {
                throw new ArgumentException($"The relative path '{rel}' is rooted. Use a path inside the output folder.", nameof(changes));
            }

            var segments = rel.Split(['/', '\\']);
            foreach (var segment in segments)
            {
                if (segment.Length == 0 || segment == "." || segment == ".." ||
                    segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    segment.TrimEnd(' ', '.').Length == 0)
                {
                    throw new ArgumentException($"The relative path '{rel}' has an empty, '.', '..' or invalid segment.", nameof(changes));
                }
            }

            var target = Path.GetFullPath(Path.Combine(staging, string.Join(Path.DirectorySeparatorChar, segments)));
            if (!target.StartsWith(stagingPrefix, PathComparison))
            {
                throw new ArgumentException($"The relative path '{rel}' resolves outside the staging folder.", nameof(changes));
            }

            var normalized = string.Join('/', segments);
            if (!seen.Add(normalized))
            {
                throw new ArgumentException($"The relative path '{rel}' appears more than once.", nameof(changes));
            }

            plan.Add(new Planned(change, normalized, target, null));
        }

        // A path that is both a file and a parent folder of another path cannot be written.
        foreach (var item in plan)
        {
            var parts = item.Normalized.Split('/');
            for (var i = 1; i < parts.Length; i++)
            {
                prefixes.Add(string.Join('/', parts, 0, i));
            }
        }

        foreach (var item in plan)
        {
            if (prefixes.Contains(item.Normalized))
            {
                throw new ArgumentException($"The relative path '{item.Change.RelativePath}' is used as both a file and a folder.", nameof(changes));
            }
        }

        plan.Sort((a, b) => string.CompareOrdinal(a.Normalized, b.Normalized));

        // Pass 2: content and source files.
        var sourcePrefix = source + Path.DirectorySeparatorChar;
        for (var i = 0; i < plan.Count; i++)
        {
            var change = plan[i].Change;
            switch (change.Kind)
            {
                case ChangeKind.Skipped:
                    break;
                case ChangeKind.Created or ChangeKind.Modified when change.NewText is null:
                    throw new InvalidOperationException($"The {change.Kind} change for '{change.RelativePath}' has no new text.");
                case ChangeKind.Created or ChangeKind.Modified:
                    break;
                default:
                    if (change.NewText is not null)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(change.SourcePath))
                    {
                        throw new InvalidOperationException($"The {change.Kind} change for '{change.RelativePath}' has no source path to copy.");
                    }

                    string full;
                    try
                    {
                        full = Path.GetFullPath(change.SourcePath);
                    }
                    catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
                    {
                        throw new InvalidOperationException($"The source path for '{change.RelativePath}' is not a valid path.", e);
                    }

                    if (!full.StartsWith(sourcePrefix, PathComparison))
                    {
                        throw new InvalidOperationException($"The source path '{change.SourcePath}' for '{change.RelativePath}' is outside the source folder.");
                    }

                    if (!File.Exists(full))
                    {
                        throw new InvalidOperationException($"The source file '{change.SourcePath}' for '{change.RelativePath}' does not exist.");
                    }

                    plan[i] = plan[i] with { SourceFull = full };
                    break;
            }
        }

        return plan;
    }

    private sealed record Planned(FileChange Change, string Normalized, string Target, string? SourceFull);
}
