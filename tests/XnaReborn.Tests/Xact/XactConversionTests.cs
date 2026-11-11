using System.Text;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Audio;
using XnaReborn.Migrator.Content;
using XnaReborn.Migrator.Output;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Migrator;
using XnaReborn.Tests.Safety;

namespace XnaReborn.Tests.Xact;

/// <summary>
/// XACT conversion inside content conversion and in a whole migration (audio-and-shaders tasks 4.1, 4.3, 4.4, 6 and 7), on a temporary copy of the
/// xna40-xact fixture. The fixture has no audio files: the tests write small WAV files from bytes.
/// </summary>
[Trait("Category", "Xact")]
public sealed class XactConversionTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-xact-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    /// <summary>A 44-byte-header PCM WAV with 100 silent 16-bit mono samples.</summary>
    internal static byte[] Wav()
    {
        var data = new byte[200];
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + data.Length);
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(8000);
        writer.Write(16000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(data.Length);
        writer.Write(data);
        return stream.ToArray();
    }

    private string Copy(string fixture, bool withWaves = true)
    {
        var folder = Path.Combine(_temp, fixture);
        MigrationTestSupport.CopyFolder(FixturePaths.Of(fixture), folder);
        if (withWaves)
        {
            Directory.CreateDirectory(Path.Combine(folder, "Content", "Audio"));
            foreach (var name in new[] { "hit", "coin", "theme" })
            {
                File.WriteAllBytes(Path.Combine(folder, "Content", "Audio", name + ".wav"), Wav());
            }
        }

        return folder;
    }

    private static async Task<ContentConversionResult> ConvertAsync(string root, string project, XactMode mode = XactMode.Auto, bool streamingAsSong = true)
    {
        var fileSystem = new ReadOnlyFileSystem();
        var read = await new ProjectReader(fileSystem).ReadAsync(Path.Combine(root, project), root, CancellationToken.None);
        var options = new ContentOptions(MonoGameTarget.DesktopGL, SubstituteFonts: false, Xact: mode, StreamingWavesAsSong: streamingAsSong);
        return await new ContentConverter(fileSystem).ConvertAsync(read, root, string.Empty, options, CancellationToken.None);
    }

    private static FileChange Change(ContentConversionResult result, string path) =>
        Assert.Single(result.Changes, c => c.RelativePath == path);

    // ---------- Auto: a readable .xap is converted ----------

    [Fact]
    public async Task Auto_turns_the_waves_into_content_entries_and_copies_the_wave_files()
    {
        var root = Copy("xna40-xact");

        var result = await ConvertAsync(root, "Xna40Xact.csproj");

        var mgcb = Change(result, "Content/Content.mgcb").NewText!;
        Assert.Contains("/importer:WavImporter\n/processor:SoundEffectProcessor\n/processorParam:Quality=Best\n/build:Audio/hit.wav;xact/Sounds/Wave Bank/hit.wav", mgcb, StringComparison.Ordinal);
        Assert.Contains("/processor:SoundEffectProcessor\n/processorParam:Quality=Best\n/build:Audio/coin.wav;xact/Sounds/Wave Bank/coin.wav", mgcb, StringComparison.Ordinal);
        Assert.Contains("/processor:SongProcessor\n/processorParam:Quality=Best\n/build:Audio/theme.wav;xact/Sounds/Music Bank/theme.wav", mgcb, StringComparison.Ordinal);
        Assert.DoesNotContain("ghost", mgcb, StringComparison.Ordinal);
        Assert.DoesNotContain("Sounds.xap", mgcb, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(mgcb, "build:Audio/hit.wav"));
        foreach (var name in new[] { "hit", "coin", "theme" })
        {
            var copy = Change(result, $"Content/Audio/{name}.wav");
            Assert.Equal(ChangeKind.Copied, copy.Kind);
            Assert.Equal(Path.Combine(root, "Content", "Audio", name + ".wav"), copy.SourcePath);
        }

        Assert.Equal(ChangeKind.Copied, Change(result, "Content/Sounds.xap").Kind);
        Assert.DoesNotContain(result.Changes, c => c.RelativePath.EndsWith("ghost.wav", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Auto_reports_what_became_of_the_audio()
    {
        var root = Copy("xna40-xact");

        var result = await ConvertAsync(root, "Xna40Xact.csproj");

        var audio = result.Report.Audio!;
        Assert.Equal(3, audio.WaveBanks);
        Assert.Equal(2, audio.SoundEffects);
        Assert.Equal(1, audio.Songs);
        Assert.Equal(1, audio.MissingWaves);
        Assert.True(audio.CuesWithUnsupported >= 1);
        Assert.True(audio.CuesSupported >= 1);
        Assert.Contains(result.ManualNotes, n => n.StartsWith("Content/Sounds.xap: [XNA071]", StringComparison.Ordinal));
        Assert.Contains(result.ManualNotes, n => n.StartsWith("Content/Sounds.xap: [XNA072]", StringComparison.Ordinal));
        Assert.Contains(result.ManualNotes, n => n.Contains("ghost.wav", StringComparison.Ordinal) && n.Contains("does not exist", StringComparison.Ordinal));
        Assert.Contains(result.ManualNotes, n => n.Contains("XACT project converted", StringComparison.Ordinal));
        Assert.Equal(audio.Notes.Count, audio.Notes.Distinct().Count());
        var xact = Assert.Single(result.Xact!);
        Assert.Equal("Sounds", xact.Plan.EngineStem);
    }

    [Fact]
    public async Task Streaming_waves_become_sound_effects_when_the_song_option_is_off()
    {
        var root = Copy("xna40-xact");

        var result = await ConvertAsync(root, "Xna40Xact.csproj", streamingAsSong: false);

        var mgcb = Change(result, "Content/Content.mgcb").NewText!;
        Assert.DoesNotContain("SongProcessor", mgcb, StringComparison.Ordinal);
        Assert.Contains("/build:Audio/theme.wav;xact/Sounds/Music Bank/theme.wav", mgcb, StringComparison.Ordinal);
        Assert.Equal(0, result.Report.Audio!.Songs);
    }

    [Fact]
    public async Task A_wave_the_pipeline_cannot_read_is_reported_and_left_out_of_the_content_file()
    {
        var root = Copy("xna40-xact");
        var xap = Path.Combine(root, "Content", "Sounds.xap");
        File.WriteAllText(xap, File.ReadAllText(xap).Replace("hit.wav", "hit.aif", StringComparison.Ordinal));
        File.WriteAllBytes(Path.Combine(root, "Content", "Audio", "hit.aif"), Wav());

        var result = await ConvertAsync(root, "Xna40Xact.csproj");

        Assert.DoesNotContain("hit.aif", Change(result, "Content/Content.mgcb").NewText!, StringComparison.Ordinal);
        Assert.Contains(result.ManualNotes, n => n.Contains("'.aif'", StringComparison.Ordinal));
        Assert.True(result.Xact![0].Plan.Assets.Single(a => a.AssetName.EndsWith("hit", StringComparison.Ordinal)).Exists is false);
        Assert.Equal(2, result.Report.Audio!.MissingWaves);
    }

    [Fact]
    public async Task Conversion_leaves_the_original_folder_unchanged()
    {
        var root = Copy("xna40-xact");
        var before = FolderState.Capture(root);

        await ConvertAsync(root, "Xna40Xact.csproj");

        Assert.Equal(before, FolderState.Capture(root));
    }

    // ---------- CopyOnly and unreadable .xap ----------

    [Fact]
    public async Task CopyOnly_copies_the_xap_and_generates_nothing()
    {
        var root = Copy("xna40-xact");

        var result = await ConvertAsync(root, "Xna40Xact.csproj", XactMode.CopyOnly);

        Assert.Empty(result.Xact ?? []);
        Assert.Null(result.Report.Audio);
        Assert.Equal(ChangeKind.ManualWork, Change(result, "Content/Sounds.xap").Kind);
        Assert.DoesNotContain("xact/", Change(result, "Content/Content.mgcb").NewText!, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Changes, c => c.RelativePath.EndsWith(".wav", StringComparison.Ordinal));
        Assert.DoesNotContain(result.ManualNotes, n => n.Contains("[XNA07", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(XactMode.Auto, false)]
    [InlineData(XactMode.ConvertOnly, true)]
    public async Task An_xap_that_cannot_be_read_is_copied_and_reported(XactMode mode, bool isError)
    {
        var root = Copy("xna40-xact");
        File.WriteAllText(Path.Combine(root, "Content", "Sounds.xap"), "Wave Bank\n{\n    Name = Broken;\n");

        var result = await ConvertAsync(root, "Xna40Xact.csproj", mode);

        Assert.Empty(result.Xact ?? []);
        Assert.Equal(ChangeKind.ManualWork, Change(result, "Content/Sounds.xap").Kind);
        var note = Assert.Single(result.ManualNotes, n => n.Contains("could not be parsed", StringComparison.Ordinal));
        Assert.Equal(isError, note.Contains("ERROR", StringComparison.Ordinal));
        Assert.Contains("ANA070", note, StringComparison.Ordinal);
    }

    // ---------- compiled banks ----------

    [Fact]
    public async Task Compiled_banks_next_to_a_converted_xap_are_copied_and_said_to_be_ignored()
    {
        var root = Copy("xna40-xact");
        var project = Path.Combine(root, "Content", "Content.contentproj");
        File.WriteAllText(
            project,
            File.ReadAllText(project).Replace(
                "  <Import Project",
                "  <ItemGroup>\n    <Compile Include=\"Sound Bank.xsb\">\n      <Name>Sound Bank</Name>\n      <Importer>XactImporter</Importer>\n      <Processor>XactProcessor</Processor>\n    </Compile>\n  </ItemGroup>\n  <Import Project",
                StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(root, "Content", "Sound Bank.xsb"), "placeholder");

        var result = await ConvertAsync(root, "Xna40Xact.csproj");

        Assert.Equal(ChangeKind.ManualWork, Change(result, "Content/Sound Bank.xsb").Kind);
        Assert.Contains(result.ManualNotes, n => n.StartsWith("Content/Sound Bank.xsb:", StringComparison.Ordinal) && n.Contains("not used", StringComparison.Ordinal));
        Assert.DoesNotContain(result.ManualNotes, n => n.Contains("[XNA070]", StringComparison.Ordinal));
        Assert.Single(result.Xact!);
    }

    // ---------- a whole migration ----------

    private async Task<MigrationResult> MigrateAsync(string root, XactMode mode)
    {
        var output = Path.Combine(_temp, "out-" + mode);
        var options = MigrationTestSupport.Options(output) with { Xact = mode };
        return await MigrationTestSupport.MigrateAsync(root, options);
    }

    [Fact]
    public async Task Migration_writes_the_stand_in_the_catalog_and_the_aliases_and_references_the_library()
    {
        var root = Copy("xna40-xact");

        var result = await MigrateAsync(root, XactMode.Auto);

        Assert.True(result.Completed);
        var paths = result.Changes.Select(c => c.RelativePath).ToList();
        Assert.Contains("XnaReborn.Compat/Xact/AudioEngine.cs", paths);
        Assert.Contains("XnaReborn.Compat/Xact/Cue.cs", paths);
        var catalog = result.Changes.Single(c => c.RelativePath == "XnaReborn.Compat/Xact/XactCatalog.g.cs").NewText!;
        Assert.Contains("new XactEngineData(\"Sounds\"", catalog, StringComparison.Ordinal);
        Assert.Contains("internal const string ContentRoot = \"Content\";", catalog, StringComparison.Ordinal);

        var game = result.Changes.Single(c => c.RelativePath == "Game1.cs");
        Assert.Equal(ChangeKind.Modified, game.Kind);
        foreach (var type in new[] { "AudioEngine", "SoundBank", "WaveBank", "Cue" })
        {
            Assert.Contains($"using {type} = XnaReborn.Compat.Xact.{type};", game.NewText, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("using AudioCategory", game.NewText, StringComparison.Ordinal);
        var csproj = result.Changes.Single(c => c.RelativePath == "Xna40Xact.csproj").NewText!;
        Assert.Contains("XnaReborn.Compat\\XnaReborn.Compat.csproj", csproj, StringComparison.Ordinal);
        Assert.Contains(result.Manifest.Shims!.Files, f => f == "XnaReborn.Compat/Xact/XactCatalog.g.cs");
        Assert.Contains(result.Manifest.Shims.Uses, u => u.File == "Game1.cs" && u.Namespace == "XnaReborn.Compat.Xact" && u.Types.Contains("Cue"));
        Assert.Equal(ChangeKind.Copied, result.Changes.Single(c => c.RelativePath == "Content/Audio/hit.wav").Kind);
    }

    [Fact]
    public async Task Migration_in_CopyOnly_mode_changes_no_source_and_writes_no_stand_in()
    {
        var root = Copy("xna40-xact");

        var result = await MigrateAsync(root, XactMode.CopyOnly);

        Assert.DoesNotContain(result.Changes, c => c.RelativePath.StartsWith("XnaReborn.Compat/", StringComparison.Ordinal));
        Assert.DoesNotContain("XnaReborn.Compat.Xact", result.Changes.Single(c => c.RelativePath == "Game1.cs").NewText ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("XnaReborn.Compat", result.Changes.Single(c => c.RelativePath == "Xna40Xact.csproj").NewText!, StringComparison.Ordinal);
        Assert.Null(result.Manifest.Shims);
    }

    [Fact]
    public async Task Compiled_banks_without_an_xap_are_copied_with_a_warning_and_the_code_is_left_alone()
    {
        var root = Copy("xna40-xact-compiled", withWaves: false);

        var result = await MigrateAsync(root, XactMode.Auto);

        Assert.DoesNotContain(result.Changes, c => c.RelativePath.StartsWith("XnaReborn.Compat/", StringComparison.Ordinal));
        Assert.DoesNotContain("Compat", result.Changes.Single(c => c.RelativePath == "Game1.cs").NewText ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(ChangeKind.Copied, result.Changes.Single(c => c.RelativePath == "Content/Wave Bank.xwb").Kind);
        var notes = await File.ReadAllTextAsync(Path.Combine(_temp, "out-Auto", "xnareborn-manifest.md"));
        Assert.Contains("[XNA070] 4 compiled XACT file(s)", notes, StringComparison.Ordinal);
        Assert.Contains("PCM or ADPCM", notes, StringComparison.Ordinal);
        Assert.Contains("NotImplementedException", notes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compiled_banks_next_to_a_converted_xap_are_ignored_in_a_migration_too()
    {
        var root = Copy("xna40-xact");
        var project = Path.Combine(root, "Xna40Xact.csproj");
        File.WriteAllText(
            project,
            File.ReadAllText(project).Replace(
                "</Project>",
                "  <ItemGroup>\n    <Content Include=\"Content\\Sound Bank.xsb\">\n      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>\n    </Content>\n  </ItemGroup>\n</Project>",
                StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(root, "Content", "Sound Bank.xsb"), "placeholder");

        await MigrateAsync(root, XactMode.Auto);

        var notes = await File.ReadAllTextAsync(Path.Combine(_temp, "out-Auto", "xnareborn-manifest.md"));
        Assert.Contains("Content/Sound Bank.xsb: [XNA016] This compiled XACT bank is copied unchanged and is not used", notes, StringComparison.Ordinal);
        Assert.DoesNotContain("[XNA070]", notes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migration_does_not_touch_the_source_folder_and_is_repeatable()
    {
        var root = Copy("xna40-xact");
        var before = FolderState.Capture(root);

        var first = await MigrateAsync(root, XactMode.Auto);
        var second = await MigrationTestSupport.MigrateAsync(root, MigrationTestSupport.Options(Path.Combine(_temp, "out-second")));

        Assert.Equal(before, FolderState.Capture(root));
        Assert.Equal(first.Changes.Select(c => (c.RelativePath, c.NewText)), second.Changes.Select(c => (c.RelativePath, c.NewText)));
    }
}
