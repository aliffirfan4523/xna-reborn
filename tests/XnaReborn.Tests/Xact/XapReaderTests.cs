using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Xact;
using XnaReborn.Core.Models.Xact;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Safety;

namespace XnaReborn.Tests.Xact;

/// <summary>Reading .xap files into <see cref="XapProject"/>.</summary>
[Trait("Category", "Xact")]
public sealed class XapReaderTests
{
    private static readonly string _fixtureXap = Path.Combine(FixturePaths.Of("xna40-xact"), "Content", "Sounds.xap");

    private static async Task<XapProject> ReadFixture()
    {
        var result = await XapReader.ReadAsync(_fixtureXap, new ReadOnlyFileSystem(), CancellationToken.None);
        Assert.Empty(result.Diagnostics);
        return Assert.IsType<XapProject>(result.Project);
    }

    [Fact]
    public async Task Header_and_engine_stem()
    {
        var project = await ReadFixture();

        Assert.Equal("XACT2", project.Signature);
        Assert.Equal(17, project.Version);
        Assert.Equal(46, project.ContentVersion);
        Assert.Equal("Sounds", project.EngineFileStem);
        Assert.Equal(_fixtureXap, project.Path);
    }

    [Fact]
    public async Task Categories()
    {
        var project = await ReadFixture();

        Assert.Equal(["Default", "Music"], project.Categories.Select(c => c.Name));
        var music = project.Categories[1];
        Assert.True(music.IsPublic);
        Assert.True(music.BackgroundMusic);
        Assert.Equal(-400, music.VolumeCentiDb);
        Assert.Equal(1, music.MaxInstances);
        Assert.Equal(2, music.Behavior);
        Assert.Equal(250, music.FadeInMs);
        Assert.Equal(500, music.FadeOutMs);
        Assert.Equal(1, music.CrossfadeType);
        Assert.Equal(["Global"], music.Parents);
        Assert.False(project.Categories[0].BackgroundMusic);
        Assert.Equal(255, project.Categories[0].MaxInstances);
    }

    [Fact]
    public async Task Variables()
    {
        var project = await ReadFixture();

        Assert.Equal(["Distance", "Intensity", "Speed"], project.Variables.Select(v => v.Name));
        Assert.True(project.Variables[1].IsGlobal);
        Assert.Equal(50f, project.Variables[1].InitialValue);
        Assert.Equal(100f, project.Variables[0].Max);
        Assert.True(project.Variables[2].ReadOnly);
        Assert.True(project.Variables[2].IsInternal);
        Assert.Equal(-1f, project.Variables[2].Min);
        Assert.False(project.Variables[2].IsPublic);
    }

    [Fact]
    public async Task Rpc_curve_and_points()
    {
        var project = await ReadFixture();

        var rpc = Assert.Single(project.Rpcs);
        Assert.Equal("DistanceFade", rpc.Name);
        var curve = Assert.Single(rpc.Curves);
        Assert.Equal("Distance", curve.VariableName);
        Assert.True(curve.IsSound);
        Assert.Equal(0, curve.Property);
        Assert.Equal([new XapRpcPoint(0f, 0f, 0), new XapRpcPoint(100f, -9600f, 0)], curve.Points);
    }

    [Fact]
    public async Task Wave_banks_waves_streaming_and_shared_file()
    {
        var project = await ReadFixture();

        Assert.Equal(["Wave Bank", "Music Bank", "Extra Bank"], project.WaveBanks.Select(b => b.Name));
        Assert.Equal(["Wave Bank", "Music Bank", "Extra Bank"], project.WaveBanks.Select(b => b.FileStem));
        Assert.Equal([false, true, false], project.WaveBanks.Select(b => b.Streaming));
        var normal = project.WaveBanks[0];
        Assert.Equal(["hit", "hit_alt", "coin"], normal.Waves.Select(w => w.Name));
        Assert.Equal(normal.Waves[0].File, normal.Waves[1].File);
        Assert.Equal("Audio\\coin.wav", normal.Waves[2].File);
        Assert.Equal("Voice Preset", normal.Waves[2].CompressionPreset);
        Assert.Null(normal.Waves[0].CompressionPreset);
        Assert.Equal(2, normal.Waves[2].Channels);
        Assert.Equal(44100, normal.Waves[2].SamplingRate);
        Assert.Equal(4410, normal.Waves[2].PlayRegionLength);
    }

    [Fact]
    public async Task Sounds_tracks_events_and_entries()
    {
        var project = await ReadFixture();

        var bank = Assert.Single(project.SoundBanks);
        Assert.Equal("Sound Bank", bank.FileStem);
        Assert.Equal(["hit", "step_a", "step_b", "theme", "layered", "echo", "ghost"], bank.Sounds.Select(s => s.Name));
        var theme = bank.Sounds[3];
        Assert.Equal(["Music"], theme.Categories);
        Assert.Equal(255, Assert.Single(Assert.Single(theme.Tracks).Events).LoopCount);
        var layered = bank.Sounds[4];
        Assert.Equal(-300, layered.VolumeCentiDb);
        Assert.Equal(600, layered.PitchCents);
        Assert.Equal(10, layered.Priority);
        Assert.Equal(["DistanceFade"], layered.RpcNames);
        Assert.Equal(2, layered.Tracks.Count);
        Assert.Equal(-200, layered.Tracks[1].VolumeCentiDb);
        var delayed = Assert.Single(layered.Tracks[1].Events);
        Assert.Equal(500, delayed.TimestampMs);
        Assert.True(delayed.Relative);
        Assert.Equal(new XapWaveEntry("Wave Bank", "coin", 255), Assert.Single(delayed.Entries));
        Assert.Equal(2, Assert.Single(layered.Tracks[0].Events).LoopCount);
    }

    [Fact]
    public async Task Cues_and_variation()
    {
        var project = await ReadFixture();

        var cues = project.SoundBanks[0].Cues;
        Assert.Equal(["Hit", "Step", "Theme", "Layered", "Echo", "Ghost"], cues.Select(c => c.Name));
        var step = cues[1];
        Assert.Equal(3, step.VariationType);
        Assert.Equal(1, step.VariationTableType);
        Assert.False(step.NewVariationOnLoop);
        Assert.Equal([new XapSoundEntry("step_a", 0, 128), new XapSoundEntry("step_b", 128, 255)], step.Entries);
    }

    [Fact]
    public async Task Dsp_and_reverb_blocks_are_listed_as_unsupported_with_line_and_path()
    {
        var project = await ReadFixture();

        Assert.Equal(2, project.Unsupported.Count);
        var dsp = project.Unsupported[0];
        Assert.Equal("Sound Bank[Sound Bank]/Sound[echo]/DSP Path Entry", dsp.Path);
        Assert.True(dsp.Line > 0);
        Assert.Contains("Effect chains", dsp.Reason, StringComparison.Ordinal);
        Assert.Equal("Sound Bank[Sound Bank]/Sound[echo]/Reverb Setting", project.Unsupported[1].Path);
    }

    [Fact]
    public async Task Unsupported_line_numbers_point_at_the_block()
    {
        var project = await ReadFixture();
        var lines = File.ReadAllLines(_fixtureXap);

        foreach (var item in project.Unsupported)
        {
            Assert.Equal(item.Path.Split('/')[^1], lines[item.Line - 1].Trim());
        }
    }

    [Fact]
    public void Unknown_keys_and_blocks_do_not_stop_reading()
    {
        const string text = "Signature = XACT2;\nMystery Header = 7;\nWave Bank\n{\n    Name = B;\n    Odd Key = 1;\n    Strange Block\n    {\n        X = 1;\n    }\n    Wave\n    {\n        Name = w;\n        File = w.wav;\n    }\n}\n";

        var result = XapReader.Read("a.xap", text);

        Assert.Empty(result.Diagnostics);
        var bank = Assert.Single(Assert.IsType<XapProject>(result.Project).WaveBanks);
        Assert.Equal("w", Assert.Single(bank.Waves).Name);
        Assert.Empty(result.Project.Unsupported);
    }

    [Fact]
    public void Byte_order_mark_crlf_tabs_and_same_line_braces_are_read()
    {
        const string text = "\uFEFFSignature = XACT2;\r\nVersion = 5;\r\nWave Bank {\r\n\tName = B;\r\n\tStreaming = 1;\r\n}\r\n";

        var result = XapReader.Read("a.xap", text);

        var project = Assert.IsType<XapProject>(result.Project);
        Assert.Equal("XACT2", project.Signature);
        Assert.Equal(5, project.Version);
        var bank = Assert.Single(project.WaveBanks);
        Assert.Equal("B", bank.Name);
        Assert.True(bank.Streaming);
    }

    [Fact]
    public void Fixture_text_with_crlf_reads_the_same_as_lf()
    {
        var lf = File.ReadAllText(_fixtureXap).Replace("\r\n", "\n");

        var a = XapReader.Read("x", lf).Project!;
        var b = XapReader.Read("x", lf.Replace("\n", "\r\n")).Project!;

        Assert.Equal(a.WaveBanks.Select(w => w.Waves.Count), b.WaveBanks.Select(w => w.Waves.Count));
        Assert.Equal(a.SoundBanks[0].Cues.Select(c => c.Name), b.SoundBanks[0].Cues.Select(c => c.Name));
        Assert.Equal(a.Unsupported.Count, b.Unsupported.Count);
    }

    [Theory]
    [InlineData("Wave Bank\n{\n    Name = B;\n", "ANA070", "never closed")]
    [InlineData("Wave Bank\n{\n    Name = B;\n}\n}\n", "ANA070", "closes nothing")]
    [InlineData("Wave Bank\n{\n}\nStray = 1;\n", "ANA071", "outside a block")]
    [InlineData("Wave Bank\nName = B;\n", "ANA071", "expected '{'")]
    [InlineData("{\n}\n", "ANA071", "without a block name")]
    public void Malformed_text_gives_a_diagnostic_and_no_project(string text, string code, string message)
    {
        var result = XapReader.Read("bad.xap", text);

        Assert.Null(result.Project);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.Contains(message, diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal("bad.xap", diagnostic.File);
    }

    [Fact]
    public async Task Missing_file_gives_a_diagnostic()
    {
        var result = await XapReader.ReadAsync(Path.Combine(FixturePaths.Root, "nope.xap"), new ReadOnlyFileSystem(), CancellationToken.None);

        Assert.Null(result.Project);
        Assert.Equal("ANA071", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task Cancelled_token_stops_the_read()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => XapReader.ReadAsync(_fixtureXap, new ReadOnlyFileSystem(), cts.Token));
    }

    /// <summary>A project without any .xap (negative case) has nothing to read.</summary>
    [Fact]
    public void Basic_fixture_has_no_xap_file() =>
        Assert.Empty(Directory.EnumerateFiles(FixturePaths.Of("xna40-basic"), "*.xap", SearchOption.AllDirectories));

    [Fact]
    public async Task Reading_leaves_fixture_files_unchanged()
    {
        var root = FixturePaths.Of("xna40-xact");
        var before = FolderState.Capture(root);

        await ReadFixture();

        Assert.Equal(before, FolderState.Capture(root));
    }
}
