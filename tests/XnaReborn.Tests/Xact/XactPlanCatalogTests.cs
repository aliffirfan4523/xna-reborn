using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;
using XnaReborn.Analyzer.Xact;
using XnaReborn.Core.Models.Xact;
using XnaReborn.Migrator.Audio;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Xact;

/// <summary>The XACT planner and the catalog writer, on the xna40-xact fixture.</summary>
[Trait("Category", "Xact")]
public sealed class XactPlanCatalogTests
{
    private static XapProject Fixture() =>
        XapReader.Read(Path.Combine(FixturePaths.Of("xna40-xact"), "Content", "Sounds.xap"), File.ReadAllText(Path.Combine(FixturePaths.Of("xna40-xact"), "Content", "Sounds.xap"))).Project!;

    private static XactPlan PlanFixture(bool streamingAsSong = true) =>
        XactPlanner.Plan(Fixture(), string.Empty, path => !path.EndsWith("ghost.wav", StringComparison.Ordinal), streamingAsSong);

    private static XapProject Minimal(string name, string? engine = null, string? waveStem = null, string? soundStem = null) =>
        new(name, null, null, null, engine, [], [], [],
            [new XapWaveBank("B", waveStem, false, [new XapWave("w", "w.wav", 1, 22050, 0, 0, 0, null)])],
            [new XapSoundBank("S", soundStem, [], [])],
            []);

    // ---------- planner ----------

    [Fact]
    public void Planner_names_assets_after_engine_bank_and_wave()
    {
        var plan = PlanFixture();

        Assert.Equal("Sounds", plan.EngineStem);
        Assert.Equal(
            ["xact/Sounds/Wave Bank/hit", "xact/Sounds/Wave Bank/coin", "xact/Sounds/Music Bank/theme", "xact/Sounds/Extra Bank/ghost"],
            plan.Assets.Select(a => a.AssetName));
        Assert.Equal(["Audio/hit.wav", "Audio/coin.wav", "Audio/theme.wav", "Audio/ghost.wav"], plan.Assets.Select(a => a.SourceRelativePath));
    }

    [Fact]
    public void Waves_that_share_a_file_share_one_asset()
    {
        var plan = PlanFixture();

        Assert.Equal(plan.AssetByWave["Wave Bank|hit"], plan.AssetByWave["Wave Bank|hit_alt"]);
        Assert.Equal(4, plan.Assets.Count);
        Assert.Equal(5, plan.AssetByWave.Count);
    }

    [Fact]
    public void Streaming_bank_becomes_songs_and_the_option_turns_that_off()
    {
        var on = PlanFixture(streamingAsSong: true);
        var off = PlanFixture(streamingAsSong: false);

        Assert.Equal(XactWaveKind.Song, on.Assets.Single(a => a.AssetName.EndsWith("theme", StringComparison.Ordinal)).Kind);
        Assert.Equal(XactWaveKind.SoundEffect, off.Assets.Single(a => a.AssetName.EndsWith("theme", StringComparison.Ordinal)).Kind);
        Assert.All(on.Assets.Where(a => !a.AssetName.EndsWith("theme", StringComparison.Ordinal)), a => Assert.Equal(XactWaveKind.SoundEffect, a.Kind));
        Assert.Contains(on.Findings, f => f.RuleId == XactPlanner.StreamingRuleId && f.Message.Contains("built as songs", StringComparison.Ordinal) && !f.NeedsManualWork);
        Assert.Contains(off.Findings, f => f.RuleId == XactPlanner.StreamingRuleId && f.Message.Contains("streaming-as-song is off", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_wave_is_marked_and_reported()
    {
        var plan = PlanFixture();

        Assert.False(plan.Assets.Single(a => a.AssetName.EndsWith("ghost", StringComparison.Ordinal)).Exists);
        Assert.All(plan.Assets.Where(a => !a.AssetName.EndsWith("ghost", StringComparison.Ordinal)), a => Assert.True(a.Exists));
        var finding = Assert.Single(plan.Findings, f => f.Message.Contains("does not exist", StringComparison.Ordinal));
        Assert.True(finding.NeedsManualWork);
        Assert.Contains("Audio/ghost.wav", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Preset_and_unsupported_features_become_notes()
    {
        var plan = PlanFixture();

        Assert.Contains(plan.Findings, f => f.Message.Contains("compression preset 'Voice Preset'", StringComparison.Ordinal));
        Assert.Equal(2, plan.Findings.Count(f => f.RuleId == XactPlanner.UnsupportedRuleId && f.NeedsManualWork));
    }

    [Fact]
    public void A_bank_that_does_not_stream_gets_no_streaming_note()
    {
        var plan = XactPlanner.Plan(Minimal("a.xap", engine: "E"), string.Empty, _ => true);

        Assert.DoesNotContain(plan.Findings, f => f.RuleId == XactPlanner.StreamingRuleId);
        Assert.Empty(plan.Findings);
    }

    [Fact]
    public void Xap_folder_prefix_is_applied_and_dots_are_collapsed()
    {
        var project = Minimal("a.xap", engine: "E") with
        {
            WaveBanks = [new XapWaveBank("B", null, false, [new XapWave("w", "..\\shared\\w.wav", 1, 1, 0, 0, 0, null)])],
        };

        var plan = XactPlanner.Plan(project, "Audio/Raw", _ => true);

        Assert.Equal("Audio/shared/w.wav", Assert.Single(plan.Assets).SourceRelativePath);
    }

    [Fact]
    public void Asset_names_drop_characters_that_are_not_safe_in_a_path()
    {
        var project = Minimal("a.xap", engine: "E") with
        {
            WaveBanks = [new XapWaveBank("B:1", null, false, [new XapWave("a;b", "x.wav", 1, 1, 0, 0, 0, null)])],
        };

        Assert.Equal("xact/E/B_1/a_b", Assert.Single(XactPlanner.Plan(project, string.Empty, _ => true).Assets).AssetName);
    }

    [Fact]
    public void Engine_stem_falls_back_to_the_xap_file_name()
    {
        var plan = XactPlanner.Plan(Minimal("C:/game/Content/Music.xap"), string.Empty, _ => true);

        Assert.Equal("Music", plan.EngineStem);
    }

    // ---------- units ----------

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(-1200, 0.2511886)]
    [InlineData(-2000, 0.1)]
    [InlineData(2000, 10.0)]
    public void Gain_converts_hundredths_of_a_decibel(int centiDb, double expected) =>
        Assert.Equal(expected, XactCatalogWriter.Gain(centiDb), precision: 5);

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(600, 0.5f)]
    [InlineData(-1200, -1f)]
    [InlineData(5000, 1f)]
    public void Pitch_maps_to_the_xna_range_and_clamps(int cents, float expected) =>
        Assert.Equal(expected, XactCatalogWriter.Pitch(cents));

    // ---------- catalog ----------

    private static XactCatalogResult WriteFixture() =>
        XactCatalogWriter.Write([(Fixture(), PlanFixture())], "Content");

    [Fact]
    public async Task Catalog_for_the_fixture_matches_snapshot() =>
        await Verifier.Verify(WriteFixture().Text, "txt");

    [Fact]
    public void Catalog_is_deterministic_and_uses_lf_only()
    {
        var first = WriteFixture().Text;
        var second = WriteFixture().Text;

        Assert.Equal(first, second);
        Assert.DoesNotContain("\r", first, StringComparison.Ordinal);
        Assert.EndsWith("    }\n}\n", first, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_writes_floats_with_a_dot_and_an_f_whatever_the_culture()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var text = WriteFixture().Text;

            Assert.Contains("0.63095737f", text, StringComparison.Ordinal);
            Assert.DoesNotContain("0,2", text, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void Catalog_lists_unsupported_features_per_cue_and_marks_the_missing_wave()
    {
        var text = WriteFixture().Text;

        Assert.Contains("new string[] { \"DSP Path Entry (line ", text, StringComparison.Ordinal);
        Assert.Contains("\"Reverb Setting (line ", text, StringComparison.Ordinal);
        Assert.Contains("new XactWaveData(\"ghost\", \"xact/Sounds/Extra Bank/ghost\", XactWaveKind.SoundEffect, true, false)", text, StringComparison.Ordinal);
        Assert.Contains("XactWaveKind.Song, false, false)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_without_cues_writes_empty_arrays()
    {
        var project = Minimal("a.xap", engine: "E") with { WaveBanks = [], SoundBanks = [] };

        var result = XactCatalogWriter.Write([(project, XactPlanner.Plan(project, string.Empty, _ => true))], "Content");

        Assert.Contains("XactWaveBankData[] WaveBanks = new XactWaveBankData[0];", result.Text, StringComparison.Ordinal);
        Assert.Contains("XactSoundBankData[] SoundBanks = new XactSoundBankData[0];", result.Text, StringComparison.Ordinal);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void No_projects_writes_an_empty_catalog()
    {
        var result = XactCatalogWriter.Write([], "Content");

        Assert.Contains("XactEngineData[] Engines = new XactEngineData[0];", result.Text, StringComparison.Ordinal);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void A_stem_used_twice_reports_a_conflict_and_keeps_the_first_owner()
    {
        var a = Minimal("a.xap", engine: "E", waveStem: "W1", soundStem: "S1");
        var b = Minimal("b.xap", engine: "E2", waveStem: "W1", soundStem: "S2");

        var result = XactCatalogWriter.Write(
            [(a, XactPlanner.Plan(a, string.Empty, _ => true)), (b, XactPlanner.Plan(b, string.Empty, _ => true))],
            "Content");

        var finding = Assert.Single(result.Findings);
        Assert.Contains("wave bank file name 'W1'", finding.Message, StringComparison.Ordinal);
        Assert.True(finding.NeedsManualWork);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(result.Text, "new XactWaveBankData\\(\"W1\""));
        Assert.Contains("new XactSoundBankData(\"S2\"", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_engine_stem_twice_leaves_the_second_project_out()
    {
        var a = Minimal("a.xap", engine: "E");
        var b = Minimal("b.xap", engine: "E");

        var result = XactCatalogWriter.Write(
            [(a, XactPlanner.Plan(a, string.Empty, _ => true)), (b, XactPlanner.Plan(b, string.Empty, _ => true))],
            "Content");

        Assert.Contains("engine file name 'E'", Assert.Single(result.Findings).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Strings_with_quotes_and_backslashes_are_escaped()
    {
        var project = Minimal("a.xap", engine: "E") with
        {
            SoundBanks = [new XapSoundBank("S\"1\\", null, [], [])],
        };

        var text = XactCatalogWriter.Write([(project, XactPlanner.Plan(project, string.Empty, _ => true))], "Content").Text;

        Assert.Contains("\"S\\\"1\\\\\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_compiles_against_the_contract_types()
    {
        var text = WriteFixture().Text;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "netstandard.dll")
            .Select(p => MetadataReference.CreateFromFile(p))
            .ToList();
        var compilation = CSharpCompilation.Create(
            "CatalogCheck",
            [CSharpSyntaxTree.ParseText(XactCatalogContract.Source), CSharpSyntaxTree.ParseText(text)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToList();

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void Writing_the_catalog_does_not_change_the_fixture()
    {
        var before = XnaReborn.Tests.Safety.FolderState.Capture(FixturePaths.Of("xna40-xact"));

        WriteFixture();

        Assert.Equal(before, XnaReborn.Tests.Safety.FolderState.Capture(FixturePaths.Of("xna40-xact")));
    }
}
