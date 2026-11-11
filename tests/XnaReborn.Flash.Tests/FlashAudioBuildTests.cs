using System.Diagnostics;
using XnaReborn.Flash.Converter;

namespace XnaReborn.Flash.Tests;

/// <summary>
/// Task 12.3: builds a converted copy of flx-platformer with a wav and an mp3 embed through the real <c>dotnet-mgcb</c> 3.8.6.1,
/// with every folder that holds ffmpeg removed from PATH. Needs the MonoGame packages in the local NuGet cache, so it only runs
/// with XNAREBORN_INTEGRATION=1.
/// Finding (2026-11-11, Windows x64): the build succeeds without a system ffmpeg or ffprobe. dotnet-mgcb 3.8.6.1 ships its own
/// ffmpeg and ffprobe for windows-x64, windows-arm64, linux-x64, linux-arm64 and osx. The mp3 becomes an .xnb and, for a Song, an .ogg.
/// A throwaway console app (not committed) loaded the wav and the mp3 as <c>SoundEffect</c> and the mp3 as <c>Song</c> on DesktopGL
/// 3.8.6.1 with no window; the mp3 effect instance reported Playing. Nothing was heard or measured on a speaker, and Linux/macOS were not run.
/// </summary>
public sealed class FlashAudioBuildTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xnareborn-flashaudio-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    /// <summary>Writes a silent MPEG-1 Layer III stream: 80 frames of 128 kbps, 44.1 kHz, with all-zero audio data (about 2 seconds).</summary>
    internal static byte[] SilentMp3()
    {
        const int frameLength = 417; // 144 * 128000 / 44100, no padding
        var bytes = new byte[frameLength * 80];
        for (var i = 0; i < bytes.Length; i += frameLength)
        {
            bytes[i] = 0xFF;
            bytes[i + 1] = 0xFB;
            bytes[i + 2] = 0x90;
        }

        return bytes;
    }

    [Fact]
    public void Silent_mp3_helper_writes_whole_frames_with_a_valid_header()
    {
        var bytes = SilentMp3();

        Assert.Equal(0, bytes.Length % 417);
        Assert.Equal([0xFF, 0xFB, 0x90, 0x00], bytes[..4]);
        Assert.Equal(0xFF, bytes[417]);
    }

    [SkippableIntegrationFact]
    public async Task Wav_and_mp3_build_without_a_system_ffmpeg()
    {
        var source = Path.Combine(_temp, "source");
        CopyDirectory(FlashFixtures.Of("flx-platformer"), source);
        File.WriteAllBytes(Path.Combine(source, "data", "tone.mp3"), SilentMp3());
        var player = Path.Combine(source, "src", "Player.as");
        var text = File.ReadAllText(player);
        const string jump = "[Embed(source=\"../data/jump.wav\")] private var SndJump:Class;";
        Assert.Contains(jump, text, StringComparison.Ordinal);
        File.WriteAllText(player, text.Replace(jump, jump + "\n\t\t[Embed(source=\"../data/tone.mp3\")] private var SndTone:Class;", StringComparison.Ordinal));

        var result = await FlashMigrator.MigrateAsync(source, Path.Combine(_temp, "out"));
        var mgcb = File.ReadAllText(Path.Combine(result.OutputFolder, "Content", "Content.mgcb"));
        Assert.Contains("/importer:Mp3Importer", mgcb, StringComparison.Ordinal);

        var psi = new ProcessStartInfo("dotnet", $"build \"{Path.Combine(result.OutputFolder, result.ProjectFile)}\" -nologo -v q --disable-build-servers")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = result.OutputFolder,
        };
        psi.Environment["PATH"] = WithoutFfmpeg(Environment.GetEnvironmentVariable("PATH") ?? string.Empty);
        psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, output);
        var built = Path.Combine(result.OutputFolder, "Content", "bin", "DesktopGL", "Content", "data");
        Assert.True(File.Exists(Path.Combine(built, "jump.xnb")), "jump.xnb is missing.");
        Assert.True(File.Exists(Path.Combine(built, "tone.xnb")), "tone.xnb is missing.");
    }

    private static string WithoutFfmpeg(string path) =>
        string.Join(
            Path.PathSeparator,
            path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(dir => !File.Exists(Path.Combine(dir, "ffmpeg.exe")) && !File.Exists(Path.Combine(dir, "ffmpeg")) && !File.Exists(Path.Combine(dir, "ffprobe.exe")) && !File.Exists(Path.Combine(dir, "ffprobe"))));

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from))
        {
            File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        }

        foreach (var d in Directory.GetDirectories(from))
        {
            CopyDirectory(d, Path.Combine(to, Path.GetFileName(d)));
        }
    }
}

/// <summary>A fact that is skipped unless XNAREBORN_INTEGRATION=1.</summary>
public sealed class SkippableIntegrationFactAttribute : FactAttribute
{
    public SkippableIntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("XNAREBORN_INTEGRATION") != "1")
        {
            Skip = "Set XNAREBORN_INTEGRATION=1 to build converted games.";
        }
    }
}
