using System.Diagnostics;
using System.Reflection;
using System.Text;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Migrator;

namespace XnaReborn.Xact.Tests;

/// <summary>
/// Checkpoint of audio-and-shaders task 10 and the real-asset test of task 8.4: migrates a copy of the xna40-xact fixture (waves written
/// from bytes), builds it with the .NET SDK and the real <c>dotnet-mgcb</c>, and plays the cues through the built stand-in.
/// Needs the network for NuGet and <c>ffmpeg</c> and <c>ffprobe</c> on the path, so it runs only when <c>XNAREBORN_INTEGRATION=1</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class XactCheckpointTests : IDisposable
{
    private static readonly TimeSpan _buildTimeout = TimeSpan.FromMinutes(10);

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-xact-int-" + Guid.NewGuid().ToString("N"));
    private readonly string _contentCopy = Path.Combine(AppContext.BaseDirectory, "Content");
    private readonly bool _hadContent = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Content"));

    public void Dispose()
    {
        TryDelete(_hadContent ? string.Empty : _contentCopy);
        TryDelete(_temp);
    }

    private static void TryDelete(string folder)
    {
        if (folder.Length == 0 || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A build server, the stand-in assembly or the song stream loaded by the test may still hold a file until the test host ends.
        }
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rules", "xna-to-monogame.json")))
            {
                return directory.FullName;
            }
        }

        throw new FileNotFoundException("The repository root was not found above " + AppContext.BaseDirectory);
    }

    /// <summary>A PCM WAV with 0.1 s of silence: 4410 16-bit mono samples at 44100 Hz. (libvorbis in the MonoGame ffmpeg refuses a tiny 8000 Hz clip as a Song.)</summary>
    private static byte[] Wav()
    {
        var data = new byte[8820];
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + data.Length);
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(44100);
        writer.Write(88200);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(data.Length);
        writer.Write(data);
        return stream.ToArray();
    }

    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var directory in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)), overwrite: true);
        }
    }

    private async Task<string> MigrateAsync(MonoGameTarget target)
    {
        var root = RepoRoot();
        var source = Path.Combine(_temp, "source");
        CopyFolder(Path.Combine(root, "tests", "fixtures", "xna40-xact"), source);
        Directory.CreateDirectory(Path.Combine(source, "Content", "Audio"));
        foreach (var name in new[] { "hit", "coin", "theme" })
        {
            File.WriteAllBytes(Path.Combine(source, "Content", "Audio", name + ".wav"), Wav());
        }

        var catalog = await RuleCatalogLoader.LoadAsync(Path.Combine(root, "rules", "xna-to-monogame.json"), new ReadOnlyFileSystem(), CancellationToken.None);
        var report = await new ProjectAnalyzer().AnalyzeAsync(source, catalog, null, CancellationToken.None);
        var output = Path.Combine(_temp, "migrated-" + target);
        var options = new MigrationOptions(output, target, false, false, new HashSet<string>(StringComparer.Ordinal));
        var result = await new ProjectMigrator().MigrateAsync(source, report, options, false, null, CancellationToken.None);
        Assert.True(result.Completed);
        return output;
    }

    private static async Task<(int ExitCode, string Output)> BuildAsync(string folder)
    {
        var start = new ProcessStartInfo("dotnet", "build --nologo -v minimal")
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        var log = new StringBuilder();
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => Append(log, e.Data);
        process.ErrorDataReceived += (_, e) => Append(log, e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var timeout = new CancellationTokenSource(_buildTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return (-1, "dotnet build did not finish in time." + Environment.NewLine + log);
        }

        process.WaitForExit();
        return (process.ExitCode, log.ToString());
    }

    private static void Append(StringBuilder log, string? line)
    {
        if (line is not null)
        {
            lock (log)
            {
                log.AppendLine(line);
            }
        }
    }

    [IntegrationFact]
    public async Task The_migrated_desktopgl_copy_builds_the_wave_assets_with_the_real_content_builder_and_plays_the_cues_through_the_stand_in()
    {
        var output = await MigrateAsync(MonoGameTarget.DesktopGL);

        var (exitCode, text) = await BuildAsync(output);

        Assert.True(exitCode == 0, $"dotnet build failed with exit code {exitCode}.{Environment.NewLine}{text}");
        var built = Path.Combine(output, "bin", "Debug", "net8.0");
        foreach (var asset in new[] { "Wave Bank/hit", "Wave Bank/coin", "Music Bank/theme" })
        {
            Assert.True(File.Exists(Path.Combine(built, "Content", "xact", "Sounds", asset + ".xnb")), asset + ".xnb is missing from " + built);
        }

        Assert.False(File.Exists(Path.Combine(built, "Content", "xact", "Extra Bank", "ghost.xnb")));
        Assert.True(File.Exists(Path.Combine(built, "XnaReborn.Compat.dll")));

        // The report says what was not converted.
        var manifest = await File.ReadAllTextAsync(Path.Combine(output, "xnareborn-manifest.md"));
        Assert.Contains("[XNA071]", manifest, StringComparison.Ordinal);
        Assert.Contains("[XNA072]", manifest, StringComparison.Ordinal);
        Assert.Contains("ghost.wav", manifest, StringComparison.Ordinal);

        PlayThroughTheBuiltStandIn(built);
    }

    [IntegrationFact]
    public async Task The_migrated_windowsdx_copy_builds_with_the_aliases_and_the_stand_in()
    {
        var output = await MigrateAsync(MonoGameTarget.WindowsDX);

        var (exitCode, text) = await BuildAsync(output);

        Assert.True(exitCode == 0, $"dotnet build failed with exit code {exitCode}.{Environment.NewLine}{text}");
        Assert.True(Directory.EnumerateFiles(output, "XnaReborn.Compat.dll", SearchOption.AllDirectories).Any(), "XnaReborn.Compat.dll is missing from the build output.");
        Assert.True(Directory.EnumerateFiles(output, "hit.xnb", SearchOption.AllDirectories).Any(), "hit.xnb is missing from the build output.");
    }

    /// <summary>
    /// Loads the built <c>XnaReborn.Compat.dll</c> (the real catalog, the real shim) and plays a cue from a real built <c>SoundEffect</c> asset
    /// loaded by a <c>ContentManager</c> with an empty service provider (R5.10). This is a test host, not the migrated game.
    /// </summary>
    private void PlayThroughTheBuiltStandIn(string built)
    {
        // The stand-in loads "Content/..." relative to the folder of the running program, which here is the test host.
        CopyFolder(Path.Combine(built, "Content"), _contentCopy);

        var compat = Assembly.LoadFrom(Path.Combine(built, "XnaReborn.Compat.dll"));
        Type T(string name) => compat.GetType("XnaReborn.Compat.Xact." + name, throwOnError: true)!;
        try
        {
            var engine = Activator.CreateInstance(T("AudioEngine"), "Content\\Sounds.xgs")!;
            var waves = Activator.CreateInstance(T("WaveBank"), engine, "Content\\Wave Bank.xwb")!;
            var music = Activator.CreateInstance(T("WaveBank"), engine, "Content\\Music Bank.xwb", 0, (short)16)!;
            var bank = Activator.CreateInstance(T("SoundBank"), engine, "Content\\Sound Bank.xsb")!;
            var cueType = T("Cue");

            var hit = T("SoundBank").GetMethod("GetCue")!.Invoke(bank, ["Hit"])!;
            cueType.GetMethod("Play")!.Invoke(hit, null);

            // A cue whose wave did not load ends at once and is not playing; this one has a real voice.
            Assert.True((bool)cueType.GetProperty("IsPlaying")!.GetValue(hit)!, "The Hit cue is not playing, so its built SoundEffect did not load.");
            Assert.True((bool)T("WaveBank").GetProperty("IsPrepared")!.GetValue(waves)!);
            Assert.True((bool)T("WaveBank").GetProperty("IsPrepared")!.GetValue(music)!);
            T("AudioEngine").GetMethod("Update")!.Invoke(engine, null);
            cueType.GetMethod("Dispose")!.Invoke(hit, null);
            Assert.True((bool)cueType.GetProperty("IsStopped")!.GetValue(hit)!);

            // The streaming bank was built as a Song and plays through MediaPlayer; the music cue loops until it is stopped.
            var theme = T("SoundBank").GetMethod("GetCue")!.Invoke(bank, ["Theme"])!;
            cueType.GetMethod("Play")!.Invoke(theme, null);
            Assert.True((bool)cueType.GetProperty("IsPlaying")!.GetValue(theme)!, "The Theme cue is not playing, so its built Song did not load.");
            T("AudioEngine").GetMethod("Update")!.Invoke(engine, null);
            Assert.True((bool)cueType.GetProperty("IsPlaying")!.GetValue(theme)!);
            cueType.GetMethod("Stop")!.Invoke(theme, [Microsoft.Xna.Framework.Audio.AudioStopOptions.Immediate]);
            Assert.True((bool)cueType.GetProperty("IsStopped")!.GetValue(theme)!);
        }
        catch (TargetInvocationException e) when (e.InnerException is Microsoft.Xna.Framework.Audio.NoAudioHardwareException)
        {
            Assert.Fail("This machine has no audio device, so the built SoundEffect assets could not be played: " + e.InnerException.Message);
        }
    }
}
