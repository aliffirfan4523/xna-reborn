using System.Text.RegularExpressions;
using XnaReborn.Runner.Processes;

namespace XnaReborn.Runner.Environment;

/// <summary>What <see cref="AudioToolCheck"/> found.</summary>
/// <param name="Needed">True when a <c>Content.mgcb</c> in the folder builds sound effects or songs, which need <c>ffmpeg</c> and <c>ffprobe</c>.</param>
/// <param name="FfmpegFound">True when <c>ffmpeg -version</c> ran. Only looked for when <paramref name="Needed"/> is true.</param>
/// <param name="FfprobeFound">True when <c>ffprobe -version</c> ran. Only looked for when <paramref name="Needed"/> is true.</param>
/// <param name="Warning">A plain sentence for the user when a tool is missing, otherwise null.</param>
public sealed record AudioToolCheckResult(bool Needed, bool FfmpegFound, bool FfprobeFound, string? Warning);

/// <summary>
/// The MonoGame audio importers call <c>ffmpeg</c> and <c>ffprobe</c> to read and convert audio, even WAV files. This check looks for them on the path
/// when the migrated copy builds audio, so a missing tool is a warning before the build and not a puzzling error in it.
/// </summary>
public static partial class AudioToolCheck
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    [GeneratedRegex(@"^\s*/processor:(SoundEffectProcessor|SongProcessor)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex AudioProcessor();

    /// <summary>Checks the folder and, if it builds audio, the two tools.</summary>
    /// <param name="migratedFolder">The migrated copy.</param>
    /// <param name="processRunner">Runs the tools. Tests pass a fake.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>What was found. Never throws for a missing tool.</returns>
    public static async Task<AudioToolCheckResult> CheckAsync(string migratedFolder, IProcessRunner processRunner, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(migratedFolder);
        ArgumentNullException.ThrowIfNull(processRunner);
        if (!BuildsAudio(migratedFolder))
        {
            return new AudioToolCheckResult(false, false, false, null);
        }

        var ffmpeg = await RunsAsync(processRunner, "ffmpeg", migratedFolder, cancellationToken).ConfigureAwait(false);
        var ffprobe = await RunsAsync(processRunner, "ffprobe", migratedFolder, cancellationToken).ConfigureAwait(false);
        if (ffmpeg && ffprobe)
        {
            return new AudioToolCheckResult(true, true, true, null);
        }

        var missing = string.Join(" and ", new[] { ffmpeg ? null : "ffmpeg", ffprobe ? null : "ffprobe" }.Where(n => n is not null));
        return new AudioToolCheckResult(
            true,
            ffmpeg,
            ffprobe,
            $"{missing} {(ffmpeg || ffprobe ? "was" : "were")} not found on the path. The audio in this project is built with ffmpeg and ffprobe, even WAV files, so the content build will fail until they are installed. Install ffmpeg, restart the terminal, and build again.");
    }

    private static bool BuildsAudio(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "*.mgcb", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(folder, file).Replace('\\', '/');
            if (relative.Split('/').Any(part => part is "bin" or "obj"))
            {
                continue;
            }

            try
            {
                if (AudioProcessor().IsMatch(File.ReadAllText(file)))
                {
                    return true;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A file that cannot be read is left to the build to report.
            }
        }

        return false;
    }

    private static async Task<bool> RunsAsync(IProcessRunner runner, string tool, string folder, CancellationToken ct)
    {
        var result = await runner.RunAsync(new ProcessRequest(tool, ["-version"], folder, _timeout), null, ct).ConfigureAwait(false);
        return result is { StartError: null, TimedOut: false, Cancelled: false, ExitCode: 0 };
    }
}
