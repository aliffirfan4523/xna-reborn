using XnaReborn.Migrator.Output;
using XnaReborn.Runner;
using XnaReborn.Runner.Diagnostics;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Hints;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Run;

namespace XnaReborn.Tests.Runner;

/// <summary>The ffmpeg and ffprobe check before a build that has audio (audio-and-shaders task 6.3).</summary>
[Trait("Category", "Runner")]
public sealed class AudioToolCheckTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xr-audiotool-" + Guid.NewGuid().ToString("N"));

    public AudioToolCheckTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private string Mgcb(string relativePath, string processor)
    {
        var path = Path.Combine(_dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"/importer:WavImporter\n/processor:{processor}\n/processorParam:Quality=Best\n/build:a.wav;a.wav\n");
        return path;
    }

    private static FakeProcessRunner Tools(params string[] present) => new(request =>
        present.Contains(request.FileName, StringComparer.Ordinal)
            ? FakeProcessRunner.Ok(request.FileName + " version 6")
            : new ProcessResult(null, false, false, string.Empty, string.Empty, TimeSpan.Zero, "not found"));

    [Theory]
    [InlineData("SoundEffectProcessor")]
    [InlineData("SongProcessor")]
    public async Task Both_tools_present_gives_no_warning(string processor)
    {
        Mgcb("Content/Content.mgcb", processor);
        var fake = Tools("ffmpeg", "ffprobe");

        var result = await AudioToolCheck.CheckAsync(_dir, fake, CancellationToken.None);

        Assert.True(result.Needed);
        Assert.True(result.FfmpegFound && result.FfprobeFound);
        Assert.Null(result.Warning);
        Assert.Equal(["ffmpeg", "ffprobe"], fake.Requests.Select(r => r.FileName));
        Assert.All(fake.Requests, r => Assert.Equal(["-version"], r.Arguments));
    }

    [Theory]
    [InlineData(new string[0], "ffmpeg and ffprobe were not found")]
    [InlineData(new[] { "ffmpeg" }, "ffprobe was not found")]
    [InlineData(new[] { "ffprobe" }, "ffmpeg was not found")]
    public async Task A_missing_tool_gives_a_warning_that_names_it(string[] present, string expected)
    {
        Mgcb("Content/Content.mgcb", "SoundEffectProcessor");

        var result = await AudioToolCheck.CheckAsync(_dir, Tools(present), CancellationToken.None);

        Assert.True(result.Needed);
        Assert.Contains(expected, result.Warning, StringComparison.Ordinal);
        Assert.Contains("even WAV files", result.Warning, StringComparison.Ordinal);
        Assert.Equal(present.Contains("ffmpeg"), result.FfmpegFound);
        Assert.Equal(present.Contains("ffprobe"), result.FfprobeFound);
    }

    [Fact]
    public async Task A_copy_without_audio_entries_runs_no_process_and_gives_no_warning()
    {
        Mgcb("Content/Content.mgcb", "TextureProcessor");
        var fake = Tools();

        var result = await AudioToolCheck.CheckAsync(_dir, fake, CancellationToken.None);

        Assert.False(result.Needed);
        Assert.Null(result.Warning);
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task Content_files_under_bin_and_obj_do_not_count()
    {
        Mgcb("bin/Debug/Content.mgcb", "SoundEffectProcessor");
        Mgcb("obj/Content.mgcb", "SongProcessor");
        var fake = Tools();

        var result = await AudioToolCheck.CheckAsync(_dir, fake, CancellationToken.None);

        Assert.False(result.Needed);
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public void The_hint_catalog_has_advice_for_the_error_the_missing_tools_cause()
    {
        var hint = new HintMatcher().Match(new BuildDiagnostic(BuildDiagnosticSeverity.Error, "MGCB", "ffprobe was not found", null, null, null, null, null));

        Assert.Equal("H306", hint?.Id);
    }

    [Fact]
    public async Task The_runner_warns_before_the_build_and_still_builds()
    {
        Mgcb("Content/Content.mgcb", "SoundEffectProcessor");
        File.WriteAllText(Path.Combine(_dir, "xnareborn-manifest.json"), ManifestJsonWriter.Write(new Manifest(1, "0.1.0", "xna-to-monogame", true, [], null)));
        File.WriteAllText(Path.Combine(_dir, "xnareborn-manifest.md"), "# manifest\n");
        File.WriteAllText(Path.Combine(_dir, "Game.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var fake = new FakeProcessRunner(request => request.FileName switch
        {
            "ffmpeg" or "ffprobe" => new ProcessResult(null, false, false, string.Empty, string.Empty, TimeSpan.Zero, "not found"),
            _ => request.Arguments[0] switch
            {
                "--version" => FakeProcessRunner.Ok("8.0.100"),
                "--list-sdks" => FakeProcessRunner.Ok("8.0.100 [C:\\dotnet\\sdk]"),
                _ => FakeProcessRunner.Ok("  Game -> bin/Debug/net8.0/Game.dll"),
            },
        });
        var lines = new List<OutputLine>();

        var result = await new ProjectRunner(fake, new GameLauncher(TimeSpan.FromMilliseconds(100)), null, null).BuildAndRunAsync(
            _dir,
            new RunnerOptions(MonoGameTargetKind.DesktopGL, Launch: false),
            _ => Task.FromResult(true),
            null,
            new SyncProgress<OutputLine>(lines.Add),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var warning = Assert.Single(result.Warnings!);
        Assert.Contains("ffmpeg and ffprobe were not found", warning, StringComparison.Ordinal);
        Assert.Contains(lines, l => l.IsError && l.Text.StartsWith("warning: ffmpeg", StringComparison.Ordinal));
    }
}
