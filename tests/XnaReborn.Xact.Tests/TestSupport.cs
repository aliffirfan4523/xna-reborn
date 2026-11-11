using Microsoft.Xna.Framework.Audio;
using XnaReborn.Compat.Xact;

namespace XnaReborn.Xact.Tests;

/// <summary>A voice that records what the shim asks of it. <see cref="Finished"/> stands in for the end of the wave.</summary>
internal sealed class FakeVoice(string wave) : IXactVoice
{
    public string Wave { get; } = wave;

    public bool Finished { get; set; }

    public bool Looped { get; private set; }

    public float VolumeValue { get; private set; } = -1f;

    public float PitchValue { get; private set; }

    public bool Played { get; private set; }

    public bool Paused { get; private set; }

    public bool Stopped { get; private set; }

    public bool Disposed { get; private set; }

    public bool Applied3D { get; private set; }

    public bool IsFinished => Finished || Stopped;

    bool IXactVoice.IsLooped
    {
        set => Looped = value;
    }

    float IXactVoice.Volume
    {
        set => VolumeValue = value;
    }

    float IXactVoice.Pitch
    {
        set => PitchValue = value;
    }

    public void Play() => Played = true;

    public void Pause() => Paused = true;

    public void Resume() => Paused = false;

    public void Stop() => Stopped = true;

    public void Apply3D(AudioListener listener, AudioEmitter emitter) => Applied3D = true;

    public void Dispose() => Disposed = true;
}

/// <summary>A factory that makes <see cref="FakeVoice"/>s, so tests need no audio device.</summary>
internal sealed class FakeFactory : IXactVoiceFactory
{
    public List<FakeVoice> Voices { get; } = [];

    public HashSet<string> Unloadable { get; } = [];

    public object? Load(XactWaveData wave) => Unloadable.Contains(wave.Name) ? null : wave;

    public IXactVoice? Create(XactWaveData wave, object loaded)
    {
        var voice = new FakeVoice(wave.Name);
        Voices.Add(voice);
        return voice;
    }

    public IEnumerable<string> Played => Voices.Select(v => v.Wave);
}

/// <summary>Builders for small catalogs.</summary>
internal static class Data
{
    public const string Bank = "WB";

    public static XactSoundData Sound(string name, string? wave = null, float volume = 1f, float pitch = 0f, int priority = 0, string[]? categories = null, string[]? rpcs = null, int loop = 0, int timestampMs = 0, float trackVolume = 1f) =>
        new(name, volume, pitch, priority, categories ?? [], rpcs ?? [], [new XactTrackData(trackVolume, [new XactEventData(loop, timestampMs, false, [new XactWaveEntryData(Bank, wave ?? name, 255)])])]);

    public static XactCueData Cue(string name, int variation = 0, params string[] sounds) =>
        new(name, variation, false, sounds.Select(s => new XactCueEntryData(s, 0, 255)).ToArray(), []);

    public static XactCategoryData Category(string name, int max = 255, int behavior = 0, float volume = 1f, string[]? parents = null) =>
        new(name, volume, true, false, max, behavior, 0, 0, parents ?? []);

    public static XactVariableData Variable(string name, bool global = false, bool readOnly = false, float initial = 0f, float min = 0f, float max = 100f) =>
        new(name, global, true, readOnly, initial, min, max);

    /// <summary>An engine, wave bank and sound bank over the given sounds and cues, played through <see cref="FakeFactory"/>.</summary>
    public static (AudioEngine Engine, SoundBank Bank, FakeFactory Factory) Setup(
        XactSoundData[] sounds,
        XactCueData[] cues,
        XactEngineData? engine = null,
        int seed = 1,
        Action<FakeFactory>? configure = null)
    {
        var factory = new FakeFactory();
        configure?.Invoke(factory);
        var audioEngine = new AudioEngine(engine ?? new XactEngineData("Sounds", [], [], []), factory, new Random(seed));
        var waves = sounds.SelectMany(s => s.Tracks).SelectMany(t => t.Events).SelectMany(e => e.Entries).Select(e => e.Wave).Distinct().Select(w => new XactWaveData(w, "xact/" + w, XactWaveKind.SoundEffect, false, false)).ToArray();
        _ = new WaveBank(audioEngine, new XactWaveBankData("Wave Bank", Bank, false, waves));
        var soundBank = new SoundBank(audioEngine, new XactSoundBankData("Sound Bank", "Sound Bank", sounds, cues));
        return (audioEngine, soundBank, factory);
    }
}
