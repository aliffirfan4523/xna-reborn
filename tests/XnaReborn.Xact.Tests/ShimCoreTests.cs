using Microsoft.Xna.Framework.Audio;
using XnaReborn.Compat.Xact;

namespace XnaReborn.Xact.Tests;

/// <summary>Lookup, variations, volume and pitch math, and cue states of the XACT shim (audio-and-shaders tasks 8.1 to 8.4).</summary>
public sealed class ShimCoreTests
{
    // ---------- lookup by file stem ----------

    [Theory]
    [InlineData("Sounds.xgs")]
    [InlineData(@"Content\Sounds.xgs")]
    [InlineData("Content/SOUNDS.XGS")]
    [InlineData("sounds")]
    public void Engine_is_found_by_stem_whatever_the_folder_case_or_extension(string path)
    {
        using var engine = new AudioEngine(path);

        Assert.False(engine.IsDisposed);
    }

    [Fact]
    public void Missing_engine_throws_with_the_name_and_the_converted_engines()
    {
        var error = Assert.Throws<FileNotFoundException>(() => new AudioEngine(@"Content\Other.xgs"));

        Assert.Contains(@"Content\Other.xgs", error.Message, StringComparison.Ordinal);
        Assert.Contains("'Sounds'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Wave_bank_and_sound_bank_are_found_by_stem_and_a_missing_one_names_the_converted_banks()
    {
        using var engine = new AudioEngine("Sounds.xgs");

        using var waves = new WaveBank(engine, @"Content\wave bank.xwb");
        using var streaming = new WaveBank(engine, "Wave Bank.xwb", 0, 16);
        using var sounds = new SoundBank(engine, "Content/Sound Bank.xsb");
        var noWaves = Assert.Throws<FileNotFoundException>(() => new WaveBank(engine, "Nope.xwb"));
        var noSounds = Assert.Throws<FileNotFoundException>(() => new SoundBank(engine, "Nope.xsb"));

        Assert.True(waves.IsPrepared);
        Assert.True(streaming.IsPrepared);
        Assert.Contains("'Wave Bank'", noWaves.Message, StringComparison.Ordinal);
        Assert.Contains("'Sound Bank'", noSounds.Message, StringComparison.Ordinal);
        Assert.Equal("Hit", sounds.GetCue("hit").Name);
    }

    [Fact]
    public void Unknown_cue_throws_and_lists_the_cues()
    {
        var (_, bank, _) = Data.Setup([Data.Sound("a")], [Data.Cue("Hit", 0, "a")]);

        var error = Assert.Throws<ArgumentException>(() => bank.GetCue("Nope"));

        Assert.Contains("'Hit'", error.Message, StringComparison.Ordinal);
    }

    // ---------- variations ----------

    private static (string[] Played, FakeFactory Factory) PlayCue(int variation, int times, int seed = 7, int[]? weights = null)
    {
        var sounds = new[] { "a", "b", "c" };
        var cue = new XactCueData("Cue", variation, false, sounds.Select((s, i) => new XactCueEntryData(s, 0, weights?[i] ?? 255)).ToArray(), []);
        var (_, bank, factory) = Data.Setup(sounds.Select(s => Data.Sound(s)).ToArray(), [cue], seed: seed);
        for (var i = 0; i < times; i++)
        {
            bank.PlayCue("Cue");
        }

        return (factory.Played.ToArray(), factory);
    }

    [Fact]
    public void Ordered_variation_plays_the_sounds_in_order_and_wraps()
    {
        Assert.Equal(["a", "b", "c", "a", "b"], PlayCue(0, 5).Played);
    }

    [Fact]
    public void Ordered_from_random_starts_anywhere_then_goes_in_order()
    {
        var played = PlayCue(1, 4, seed: 3).Played;

        var start = Array.IndexOf(new[] { "a", "b", "c" }, played[0]);
        Assert.Equal(Enumerable.Range(0, 4).Select(i => new[] { "a", "b", "c" }[(start + i) % 3]), played);
    }

    [Fact]
    public void Random_variation_follows_the_weights_and_never_picks_a_weight_of_zero()
    {
        var played = PlayCue(2, 200, weights: [255, 0, 0]).Played;

        Assert.All(played, p => Assert.Equal("a", p));
    }

    [Fact]
    public void Random_variation_without_weights_uses_every_sound()
    {
        var played = PlayCue(2, 100).Played;

        Assert.Equal(3, played.Distinct().Count());
    }

    [Fact]
    public void Random_without_immediate_repeat_never_repeats_the_last_sound()
    {
        var played = PlayCue(3, 100).Played;

        for (var i = 1; i < played.Length; i++)
        {
            Assert.NotEqual(played[i - 1], played[i]);
        }
    }

    [Fact]
    public void Shuffle_plays_every_sound_once_per_round_without_a_repeat_across_rounds()
    {
        var played = PlayCue(4, 30, seed: 11).Played;

        for (var round = 0; round < 10; round++)
        {
            Assert.Equal(["a", "b", "c"], played.Skip(round * 3).Take(3).Order(StringComparer.Ordinal));
        }

        for (var i = 1; i < played.Length; i++)
        {
            Assert.NotEqual(played[i - 1], played[i]);
        }
    }

    [Fact]
    public void Same_seed_gives_the_same_sequence_and_a_cue_without_entries_is_silent()
    {
        Assert.Equal(PlayCue(2, 20, seed: 5).Played, PlayCue(2, 20, seed: 5).Played);

        var (_, bank, factory) = Data.Setup([Data.Sound("a")], [new XactCueData("Empty", 0, false, [], [])]);
        bank.PlayCue("Empty");
        Assert.Empty(factory.Voices);
    }

    [Fact]
    public void A_cue_whose_sound_is_missing_from_the_bank_is_silent()
    {
        var (_, bank, factory) = Data.Setup([Data.Sound("a")], [Data.Cue("Hit", 0, "ghost")]);

        bank.PlayCue("Hit");

        Assert.Empty(factory.Voices);
    }

    // ---------- volume and pitch ----------

    [Fact]
    public void Volume_is_sound_times_track_times_category()
    {
        var engine = new XactEngineData("Sounds", [Data.Category("Music", volume: 0.5f)], [], []);
        var (audio, bank, factory) = Data.Setup([Data.Sound("a", volume: 0.5f, trackVolume: 0.5f, categories: ["Music"])], [Data.Cue("Hit", 0, "a")], engine);

        bank.PlayCue("Hit");

        Assert.Equal(0.125f, factory.Voices.Single().VolumeValue, 5);
        audio.GetCategory("Music").SetVolume(0.5f);
        audio.Advance(0.1f);
        Assert.Equal(0.0625f, factory.Voices.Single().VolumeValue, 5);
    }

    [Fact]
    public void Category_volume_includes_the_volume_of_its_parents()
    {
        var engine = new XactEngineData("Sounds", [Data.Category("Child", parents: ["Global"]), Data.Category("Global", volume: 0.5f)], [], []);
        var (_, bank, factory) = Data.Setup([Data.Sound("a", categories: ["Child"])], [Data.Cue("Hit", 0, "a")], engine);

        bank.PlayCue("Hit");

        Assert.Equal(0.5f, factory.Voices.Single().VolumeValue, 5);
    }

    [Fact]
    public void Pitch_is_passed_on_and_an_rpc_curve_changes_volume_and_pitch_from_a_cue_variable()
    {
        var rpc = new XactRpcData(
            "Fall",
            [
                new XactRpcCurveData("v", 0, true, "Distance", [new XactRpcPointData(0, 0, 0), new XactRpcPointData(10, -2000, 0)]),
                new XactRpcCurveData("p", 1, true, "Distance", [new XactRpcPointData(0, 0, 0), new XactRpcPointData(10, 600, 0)]),
            ]);
        var engine = new XactEngineData("Sounds", [], [Data.Variable("Distance", max: 10)], [rpc]);
        var (_, bank, factory) = Data.Setup([Data.Sound("a", pitch: 0.25f, rpcs: ["Fall"])], [Data.Cue("Hit", 0, "a")], engine);
        var cue = bank.GetCue("Hit");

        cue.SetVariable("Distance", 5);
        cue.Play();

        var voice = factory.Voices.Single();
        Assert.Equal(0.316228f, voice.VolumeValue, 4);
        Assert.Equal(0.5f, voice.PitchValue, 4);
        Assert.Equal(5f, cue.GetVariable("Distance"));
    }

    [Theory]
    [InlineData(-5f, 5f)]
    [InlineData(0f, 5f)]
    [InlineData(10f, -1f)]
    [InlineData(20f, -1f)]
    public void Interpolation_holds_the_end_values(float x, float expected)
    {
        Assert.Equal(expected, Cue.Interpolate([new XactRpcPointData(0, 5, 0), new XactRpcPointData(10, -1, 0)], x));
    }

    [Fact]
    public void Interpolation_midpoint()
    {
        Assert.Equal(2f, Cue.Interpolate([new XactRpcPointData(0, 5, 0), new XactRpcPointData(10, -1, 0)], 5f), 5);
    }

    [Fact]
    public void Variables_are_clamped_and_read_only_and_unknown_ones_throw()
    {
        var engine = new XactEngineData("Sounds", [], [Data.Variable("Pan", max: 1), Data.Variable("Fixed", readOnly: true), Data.Variable("Level", global: true, initial: 3, max: 10)], []);
        var (audio, bank, _) = Data.Setup([Data.Sound("a")], [Data.Cue("Hit", 0, "a")], engine);
        var cue = bank.GetCue("Hit");

        cue.SetVariable("Pan", 5);
        audio.SetGlobalVariable("Level", 99);

        Assert.Equal(1f, cue.GetVariable("Pan"));
        Assert.Equal(10f, audio.GetGlobalVariable("Level"));
        Assert.Throws<InvalidOperationException>(() => cue.SetVariable("Fixed", 1));
        Assert.Throws<InvalidOperationException>(() => cue.SetVariable("Nope", 1));
        Assert.Throws<InvalidOperationException>(() => cue.GetVariable("Level"));
        Assert.Throws<InvalidOperationException>(() => audio.GetGlobalVariable("Nope"));
        Assert.Throws<InvalidOperationException>(() => audio.SetGlobalVariable("Pan", 1));
    }

    // ---------- states ----------

    [Fact]
    public void Cue_goes_from_prepared_to_playing_paused_playing_and_stopped()
    {
        var (_, bank, factory) = Data.Setup([Data.Sound("a")], [Data.Cue("Hit", 0, "a")]);
        var cue = bank.GetCue("Hit");

        Assert.True(cue.IsPrepared);
        Assert.False(cue.IsPlaying || cue.IsPaused || cue.IsStopped || cue.IsStopping || cue.IsPreparing || cue.IsCreated);
        cue.Play();
        Assert.True(cue.IsPlaying);
        cue.Pause();
        Assert.True(cue.IsPaused);
        Assert.True(factory.Voices[0].Paused);
        Assert.False(cue.IsPlaying);
        cue.Play();
        Assert.True(cue.IsPlaying);
        Assert.False(factory.Voices[0].Paused);
        cue.Stop(AudioStopOptions.AsAuthored);
        Assert.True(cue.IsStopped);
        Assert.True(factory.Voices[0].Stopped);
        Assert.True(factory.Voices[0].Disposed);
        var error = Assert.Throws<InvalidOperationException>(cue.Play);
        Assert.Contains("GetCue", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Cue_stops_by_itself_when_the_wave_ends_and_PlayCue_cleans_up_the_engine()
    {
        var (audio, bank, factory) = Data.Setup([Data.Sound("a")], [Data.Cue("Hit", 0, "a")]);

        bank.PlayCue("Hit");
        Assert.Single(audio.Active);
        factory.Voices[0].Finished = true;
        audio.Advance(0.1f);

        Assert.Empty(audio.Active);
        Assert.True(factory.Voices[0].Disposed);
    }

    [Fact]
    public void Dispose_stops_the_cue_and_raises_Disposing_once()
    {
        var (_, bank, factory) = Data.Setup([Data.Sound("a")], [Data.Cue("Hit", 0, "a")]);
        var cue = bank.GetCue("Hit");
        var raised = 0;
        cue.Disposing += (_, _) => raised++;
        cue.Play();

        cue.Dispose();
        cue.Dispose();

        Assert.True(cue.IsDisposed);
        Assert.True(cue.IsStopped);
        Assert.True(factory.Voices[0].Stopped);
        Assert.Equal(1, raised);
        Assert.Throws<ObjectDisposedException>(cue.Play);
    }

    [Fact]
    public void Engine_dispose_stops_everything_and_later_calls_throw()
    {
        var (audio, bank, factory) = Data.Setup([Data.Sound("a", loop: 255)], [Data.Cue("Hit", 0, "a")]);
        bank.PlayCue("Hit");

        audio.Dispose();

        Assert.True(audio.IsDisposed);
        Assert.True(factory.Voices[0].Stopped);
        Assert.Throws<ObjectDisposedException>(audio.Update);
    }

    [Fact]
    public void Bank_use_flags_follow_the_playing_cues()
    {
        var (audio, bank, factory) = Data.Setup([Data.Sound("a")], [Data.Cue("Hit", 0, "a")]);
        var waves = audio.WaveBanks[Data.Bank];

        Assert.False(bank.IsInUse || waves.IsInUse);
        bank.PlayCue("Hit");
        Assert.True(bank.IsInUse && waves.IsInUse);
        factory.Voices[0].Finished = true;
        audio.Advance(0.1f);
        Assert.False(bank.IsInUse || waves.IsInUse);
    }

    // ---------- silent and unsupported ----------

    [Fact]
    public void A_wave_that_cannot_be_loaded_is_silent_and_the_cue_just_ends()
    {
        var (audio, bank, factory) = Data.Setup([Data.Sound("a")], [Data.Cue("Hit", 0, "a")], configure: f => f.Unloadable.Add("a"));
        var cue = bank.GetCue("Hit");

        cue.Play();

        Assert.Empty(factory.Voices);
        Assert.True(cue.IsStopped);
        Assert.Empty(audio.Active);
    }

    [Fact]
    public void A_missing_wave_is_silent()
    {
        var factory = new FakeFactory();
        var audio = new AudioEngine(new XactEngineData("Sounds", [], [], []), factory, new Random(1));
        _ = new WaveBank(audio, new XactWaveBankData("Wave Bank", Data.Bank, false, [new XactWaveData("a", "xact/a", XactWaveKind.SoundEffect, true, false)]));
        var bank = new SoundBank(audio, new XactSoundBankData("Sound Bank", "Sound Bank", [Data.Sound("a")], [Data.Cue("Hit", 0, "a")]));

        bank.PlayCue("Hit");

        Assert.Empty(factory.Voices);
        Assert.Empty(audio.Active);
    }

    [Fact]
    public void A_cue_whose_wave_bank_was_never_created_is_silent()
    {
        var factory = new FakeFactory();
        var audio = new AudioEngine(new XactEngineData("Sounds", [], [], []), factory, new Random(1));
        var bank = new SoundBank(audio, new XactSoundBankData("Sound Bank", "Sound Bank", [Data.Sound("a")], [Data.Cue("Hit", 0, "a")]));

        bank.PlayCue("Hit");

        Assert.Empty(factory.Voices);
    }

    [Fact]
    public void Unsupported_features_in_the_catalog_never_stop_the_cue()
    {
        var (_, bank, factory) = Data.Setup([Data.Sound("a")], [new XactCueData("Hit", 0, false, [new XactCueEntryData("a", 0, 255)], ["Reverb Setting (line 4)"])]);

        bank.PlayCue("Hit");

        Assert.Single(factory.Voices);
    }

    [Fact]
    public void Apply3D_reaches_the_voices_before_and_after_play()
    {
        var (_, bank, factory) = Data.Setup([Data.Sound("a")], [Data.Cue("Hit", 0, "a")]);
        var listener = new AudioListener();
        var emitter = new AudioEmitter();

        bank.PlayCue("Hit", listener, emitter);
        var late = bank.GetCue("Hit");
        late.Play();
        late.Apply3D(listener, emitter);

        Assert.All(factory.Voices, v => Assert.True(v.Applied3D));
        Assert.Throws<ArgumentNullException>(() => late.Apply3D(null!, emitter));
    }
}
