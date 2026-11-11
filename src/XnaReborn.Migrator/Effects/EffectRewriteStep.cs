using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Content;

namespace XnaReborn.Migrator.Effects;

/// <summary>The files an effect pulls in with <c>#include</c>.</summary>
/// <param name="Files">Absolute paths of the files that exist, in discovery order, without duplicates.</param>
/// <param name="Missing">One sentence per include that was not found.</param>
public sealed record FxIncludes(IReadOnlyList<string> Files, IReadOnlyList<string> Missing);

/// <summary>Runs the effect rewriters in a fixed order and finds the files an effect includes.</summary>
public static class EffectRewriteStep
{
    /// <summary>Rewrites one effect: profile macros, then sampler fixes, then markers. Each step is idempotent, so the whole is.</summary>
    /// <param name="text">The effect text.</param>
    /// <param name="profiles">The profile data. Defaults to <see cref="EffectProfiles.Default"/>.</param>
    public static FxRewriteResult Rewrite(string text, EffectProfiles? profiles = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        profiles ??= EffectProfiles.Default;
        var profile = FxProfileRewriter.Rewrite(text, profiles);
        var sampler = FxSamplerRewriter.Rewrite(profile.Text, profiles);
        var markers = FxMarkers.Mark(sampler.Text, profiles);
        return new FxRewriteResult(markers.Text, [.. profile.Notes, .. sampler.Notes, .. markers.Notes]);
    }

    /// <summary>
    /// Finds the files an effect includes, directly and through other includes. A path resolves against the folder of the file that names it.
    /// A cycle is cut by a visited set. Reads through <see cref="IReadOnlyFileSystem"/> only.
    /// </summary>
    /// <param name="effectPath">Absolute path of the effect.</param>
    /// <param name="fileSystem">Read-only file system.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    public static FxIncludes CollectIncludes(string effectPath, IReadOnlyFileSystem fileSystem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(effectPath);
        ArgumentNullException.ThrowIfNull(fileSystem);
        var files = new List<string>();
        var missing = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(effectPath) };
        var queue = new Queue<string>();
        queue.Enqueue(Path.GetFullPath(effectPath));
        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = queue.Dequeue();
            string text;
            try
            {
                text = fileSystem.ReadAllText(current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                missing.Add($"{Path.GetFileName(current)} could not be read: {ex.Message}");
                continue;
            }

            foreach (var include in FxScanner.Scan(text).Includes)
            {
                var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(current) ?? string.Empty, include.Path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
                if (!fileSystem.FileExists(resolved))
                {
                    missing.Add($"{Path.GetFileName(current)} line {include.Line} includes '{include.Path}', which was not found.");
                }
                else if (visited.Add(resolved))
                {
                    files.Add(resolved);
                    queue.Enqueue(resolved);
                }
            }
        }

        return new FxIncludes(files, missing);
    }
}
