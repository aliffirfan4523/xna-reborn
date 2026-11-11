using System.Reflection;
using Microsoft.Xna.Framework.Audio;
using XnaReborn.Compat.Xact;

namespace XnaReborn.Xact.Tests;

/// <summary>Loops, delayed events, category limits and operations, and parity with the MonoGame XACT types (audio-and-shaders tasks 9.1 to 9.6).</summary>
public sealed class ShimAdvancedTests
{
    // ---------- loops and delayed events ----------

    [Fact]
    public void Loop_count_255_loops_until_the_cue_is_stopped()
    {
        var (audio, bank, factory) = Data.Setup([Data.Sound("a", loop: 255)], [Data.Cue("Hit", 0, "a")]);
        var cue = bank.GetCue("Hit");

        cue.Play();
        audio.Advance(1f);

        Assert.True(factory.Voices[0].Looped);
        Assert.True(cue.IsPlaying);
        cue.Stop(AudioStopOptions.Immediate);
        Assert.True(cue.IsStopped);
    }

    [Fact]
    public void Loop_count_2_plays_three_times_in_total()
    {
        var (audio, bank, factory) = Data.Setup([Data.Sound("a", loop: 2)], [Data.Cue("Hit", 0, "a")]);
        var cue = bank.GetCue("Hit");
        cue.Play();

        for (var i = 0; i < 3; i++)
        {
            factory.Voices[^1].Finished = true;
            audio.Advance(0.1f);
        }

        Assert.Equal(3, factory.Voices.Count);
        Assert.All(factory.Voices, v => Assert.False(v.Looped));
        Assert.True(cue.IsStopped);
    }

    [Fact]
    public void Loop_count_0_plays_once()
    {
        var (audio, bank, factory) = Data.Setup([Data.Sound("a")], [Data.Cue("Hit", 0, "a")]);
        var cue = bank.GetCue("Hit");
        cue.Play();

        factory.Voices[0].Finished = true;
        audio.Advance(0.1f);

        Assert.Single(factory.Voices);
        Assert.True(cue.IsStopped);
    }

    [Fact]
    public void A_paused_cue_keeps_its_timers_and_loops()
    {
        var (audio, bank, factory) = Data.Setup([Data.Sound("a", timestampMs: 500)], [Data.Cue("Hit", 0, "a")]);
        var cue = bank.GetCue("Hit");
        cue.Play();
        cue.Pause();

        audio.Advance(2f);

        Assert.Empty(factory.Voices);
        cue.Resume();
        audio.Advance(0.6f);
        Assert.Single(factory.Voices);
    }

    [Fact]
    public void A_delayed_event_starts_after_its_timestamp()
    {
        var (audio, bank, factory) = Data.Setup([Data.Sound("a", timestampMs: 500)], [Data.Cue("Hit", 0, "a")]);
        var cue = bank.GetCue("Hit");

        cue.Play();
        Assert.Empty(factory.Voices);
        Assert.True(cue.IsPlaying);
        audio.Advance(0.4f);
        Assert.Empty(factory.Voices);
        audio.Advance(0.2f);

        Assert.Single(factory.Voices);
        Assert.True(factory.Voices[0].Played);
    }

    [Fact]
    public void Several_tracks_play_together()
    {
        var sound = new XactSoundData(
            "multi",
            1f,
            0f,
            0,
            [],
            [],
            [
                new XactTrackData(1f, [new XactEventData(0, 0, false, [new XactWaveEntryData(Data.Bank, "a", 255)])]),
                new XactTrackData(0.5f, [new XactEventData(0, 0, false, [new XactWaveEntryData(Data.Bank, "b", 255)])]),
            ]);
        var (_, bank, factory) = Data.Setup([sound], [Data.Cue("Hit", 0, "multi")]);

        bank.PlayCue("Hit");

        Assert.Equal(["a", "b"], factory.Played.Order(StringComparer.Ordinal));
        Assert.Equal(0.5f, factory.Voices.Single(v => v.Wave == "b").VolumeValue, 5);
    }

    [Fact]
    public void An_event_with_several_wave_entries_picks_by_weight()
    {
        var sound = new XactSoundData("s", 1f, 0f, 0, [], [], [new XactTrackData(1f, [new XactEventData(0, 0, false, [new XactWaveEntryData(Data.Bank, "a", 50), new XactWaveEntryData(Data.Bank, "b", 255)])])]);
        var (_, bank, factory) = Data.Setup([sound], [Data.Cue("Hit", 0, "s")]);

        for (var i = 0; i < 400; i++)
        {
            bank.PlayCue("Hit");
        }

        Assert.True(factory.Played.Count(w => w == "b") > factory.Played.Count(w => w == "a"));
        Assert.Contains("a", factory.Played);
    }

    // ---------- category limits ----------

    private static (AudioEngine Audio, SoundBank Bank, FakeFactory Factory) Limited(int behavior, params XactSoundData[] sounds)
    {
        var engine = new XactEngineData("Sounds", [Data.Category("Fx", max: 1, behavior: behavior)], [], []);
        return Data.Setup(sounds, sounds.Select(s => Data.Cue(s.Name, 0, s.Name)).ToArray(), engine);
    }

    [Fact]
    public void Behavior_0_fails_to_play_the_new_cue()
    {
        var (_, bank, factory) = Limited(0, Data.Sound("a", categories: ["Fx"], loop: 255), Data.Sound("b", categories: ["Fx"]));
        var first = bank.GetCue("a");
        var second = bank.GetCue("b");

        first.Play();
        second.Play();

        Assert.True(first.IsPlaying);
        Assert.True(second.IsStopped);
        Assert.Equal(["a"], factory.Played);
    }

    [Fact]
    public void Behavior_2_replaces_the_oldest_cue()
    {
        var (_, bank, factory) = Limited(2, Data.Sound("a", categories: ["Fx"], loop: 255), Data.Sound("b", categories: ["Fx"], loop: 255));
        var first = bank.GetCue("a");
        var second = bank.GetCue("b");

        first.Play();
        second.Play();

        Assert.True(first.IsStopped);
        Assert.True(second.IsPlaying);
        Assert.True(factory.Voices[0].Stopped);
    }

    [Fact]
    public void Behavior_1_queues_the_new_cue_until_there_is_room()
    {
        var (audio, bank, factory) = Limited(1, Data.Sound("a", categories: ["Fx"]), Data.Sound("b", categories: ["Fx"]));
        var first = bank.GetCue("a");
        var second = bank.GetCue("b");

        first.Play();
        second.Play();
        Assert.Equal(["a"], factory.Played);
        Assert.True(second.IsPlaying);
        factory.Voices[0].Finished = true;
        audio.Advance(0.1f);

        Assert.Equal(["a", "b"], factory.Played);
        Assert.True(first.IsStopped);
        Assert.Empty(audio.Queue);
    }

    [Fact]
    public void Behavior_3_replaces_the_quietest_cue()
    {
        var engine = new XactEngineData("Sounds", [Data.Category("Fx", max: 2, behavior: 3)], [], []);
        var sounds = new[]
        {
            Data.Sound("loud", volume: 1f, categories: ["Fx"], loop: 255),
            Data.Sound("quiet", volume: 0.1f, categories: ["Fx"], loop: 255),
            Data.Sound("new", volume: 0.5f, categories: ["Fx"], loop: 255),
        };
        var (_, bank, _) = Data.Setup(sounds, sounds.Select(s => Data.Cue(s.Name, 0, s.Name)).ToArray(), engine);
        var loud = bank.GetCue("loud");
        var quiet = bank.GetCue("quiet");
        var fresh = bank.GetCue("new");

        loud.Play();
        quiet.Play();
        fresh.Play();

        Assert.True(loud.IsPlaying);
        Assert.True(quiet.IsStopped);
        Assert.True(fresh.IsPlaying);
    }

    [Fact]
    public void Behavior_4_replaces_the_lowest_priority_cue()
    {
        var engine = new XactEngineData("Sounds", [Data.Category("Fx", max: 2, behavior: 4)], [], []);
        var sounds = new[]
        {
            Data.Sound("high", priority: 9, categories: ["Fx"], loop: 255),
            Data.Sound("low", priority: 1, categories: ["Fx"], loop: 255),
            Data.Sound("new", priority: 5, categories: ["Fx"], loop: 255),
        };
        var (_, bank, _) = Data.Setup(sounds, sounds.Select(s => Data.Cue(s.Name, 0, s.Name)).ToArray(), engine);
        var high = bank.GetCue("high");
        var low = bank.GetCue("low");
        var fresh = bank.GetCue("new");

        high.Play();
        low.Play();
        fresh.Play();

        Assert.True(high.IsPlaying);
        Assert.True(low.IsStopped);
        Assert.True(fresh.IsPlaying);
    }

    [Theory]
    [InlineData(255)]
    [InlineData(0)]
    public void Max_instances_of_255_or_0_means_no_limit(int max)
    {
        var engine = new XactEngineData("Sounds", [Data.Category("Fx", max: max)], [], []);
        var (_, bank, factory) = Data.Setup([Data.Sound("a", categories: ["Fx"], loop: 255)], [Data.Cue("a", 0, "a")], engine);

        for (var i = 0; i < 5; i++)
        {
            bank.PlayCue("a");
        }

        Assert.Equal(5, factory.Voices.Count);
    }

    [Fact]
    public void A_limit_applies_only_to_cues_of_that_category()
    {
        var (_, bank, factory) = Limited(0, Data.Sound("a", categories: ["Fx"], loop: 255), Data.Sound("other", loop: 255));

        bank.PlayCue("a");
        bank.PlayCue("other");

        Assert.Equal(2, factory.Voices.Count);
    }

    // ---------- category operations ----------

    [Fact]
    public void Category_stop_pause_and_resume_apply_to_its_cues_only()
    {
        var engine = new XactEngineData("Sounds", [Data.Category("Music"), Data.Category("Fx")], [], []);
        var (audio, bank, factory) = Data.Setup(
            [Data.Sound("m", categories: ["Music"], loop: 255), Data.Sound("f", categories: ["Fx"], loop: 255)],
            [Data.Cue("m", 0, "m"), Data.Cue("f", 0, "f")],
            engine);
        var music = bank.GetCue("m");
        var fx = bank.GetCue("f");
        music.Play();
        fx.Play();
        var category = audio.GetCategory("music");

        category.Pause();
        Assert.True(music.IsPaused);
        Assert.True(fx.IsPlaying);
        category.Resume();
        Assert.True(music.IsPlaying);
        category.Stop(AudioStopOptions.AsAuthored);

        Assert.True(music.IsStopped);
        Assert.True(fx.IsPlaying);
        Assert.True(factory.Voices[0].Stopped);
        Assert.False(factory.Voices[1].Stopped);
    }

    [Fact]
    public void Category_equality_name_and_unknown_names()
    {
        var engine = new XactEngineData("Sounds", [Data.Category("Music")], [], []);
        var (audio, _, _) = Data.Setup([Data.Sound("a")], [Data.Cue("a", 0, "a")], engine);

        var a = audio.GetCategory("Music");
        var b = audio.GetCategory("MUSIC");

        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal("Music", a.Name);
        Assert.Equal("Music", a.ToString());
        Assert.True(audio.GetCategory("Global") != a);
        Assert.Throws<InvalidOperationException>(() => audio.GetCategory("Nope"));
    }

    // ---------- parity with the MonoGame XACT types ----------

    private static string Signature(MemberInfo member)
    {
        static string Type(Type type) => type.Name;
        return member switch
        {
            MethodBase m => m.Name + "(" + string.Join(",", m.GetParameters().Select(p => Type(p.ParameterType))) + ")",
            PropertyInfo p => p.Name + ":" + Type(p.PropertyType) + "[" + (p.GetMethod?.IsPublic == true ? "get" : string.Empty) + (p.SetMethod?.IsPublic == true ? "set" : string.Empty) + "]",
            EventInfo e => e.Name + ":" + Type(e.EventHandlerType!),
            FieldInfo f => f.Name + ":" + Type(f.FieldType),
            _ => member.Name,
        };
    }

    [Theory]
    [InlineData("AudioEngine")]
    [InlineData("WaveBank")]
    [InlineData("SoundBank")]
    [InlineData("Cue")]
    [InlineData("AudioCategory")]
    public void Every_public_member_of_the_MonoGame_type_exists_on_the_shim_type(string name)
    {
        var monoGame = typeof(SoundEffect).Assembly.GetType("Microsoft.Xna.Framework.Audio." + name, throwOnError: true)!;
        var shim = typeof(XactCatalog).Assembly.GetType("XnaReborn.Compat.Xact." + name, throwOnError: true)!;
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var shimMembers = shim.GetMembers(flags).Select(Signature).ToHashSet(StringComparer.Ordinal);

        var missing = monoGame.GetMembers(flags)
            .Where(m => m.MemberType is MemberTypes.Method or MemberTypes.Property or MemberTypes.Event or MemberTypes.Field or MemberTypes.Constructor)
            .Where(m => m is not MethodInfo { IsSpecialName: true })
            .Select(Signature)
            .Where(s => !shimMembers.Contains(s))
            .ToList();

        Assert.True(missing.Count == 0, name + " is missing: " + string.Join("; ", missing));
        Assert.Equal(monoGame.IsValueType, shim.IsValueType);
        Assert.Equal(monoGame.GetInterfaces().Select(i => i.Name).Order(StringComparer.Ordinal), shim.GetInterfaces().Select(i => i.Name).Order(StringComparer.Ordinal));
    }
}
