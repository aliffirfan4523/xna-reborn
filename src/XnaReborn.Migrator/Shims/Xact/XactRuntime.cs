// Part of XnaReborn.Compat, the XACT stand-in that XNA Reborn writes into a migrated copy.
// Voices, the voice factory (the seam that lets tests run without an audio device), name lookup and cue variations.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace XnaReborn.Compat.Xact;

/// <summary>One playing wave. The real one wraps a <c>SoundEffectInstance</c> or the <c>MediaPlayer</c>.</summary>
internal interface IXactVoice : IDisposable
{
    /// <summary>True once the wave has played to its end or was stopped.</summary>
    bool IsFinished { get; }

    bool IsLooped { set; }

    /// <summary>Linear volume, 0 to 1.</summary>
    float Volume { set; }

    /// <summary>XNA pitch, -1 to 1.</summary>
    float Pitch { set; }

    void Play();

    void Pause();

    void Resume();

    void Stop();

    void Apply3D(AudioListener listener, AudioEmitter emitter);
}

/// <summary>Loads wave assets and makes voices for them.</summary>
internal interface IXactVoiceFactory
{
    /// <summary>Loads the asset of a wave. Returns null when it cannot be loaded; the wave is then silent.</summary>
    object? Load(XactWaveData wave);

    /// <summary>Makes a voice for a loaded wave.</summary>
    IXactVoice? Create(XactWaveData wave, object loaded);
}

/// <summary>A service provider with no services: enough for <c>SoundEffect</c> and <c>Song</c> assets, which need no graphics device.</summary>
internal sealed class EmptyServiceProvider : IServiceProvider
{
    public object? GetService(Type serviceType) => null;
}

/// <summary>The factory the game uses: waves are content assets loaded by a private <c>ContentManager</c>.</summary>
internal sealed class XactVoiceFactory : IXactVoiceFactory
{
    private readonly ContentManager _content;

    public XactVoiceFactory(string contentRoot)
    {
        _content = new ContentManager(new EmptyServiceProvider(), contentRoot);
    }

    public object? Load(XactWaveData wave)
    {
        try
        {
            return wave.Kind == XactWaveKind.Song ? _content.Load<Song>(wave.Asset) : _content.Load<SoundEffect>(wave.Asset);
        }
        catch (Exception e) when (e is ContentLoadException or FileNotFoundException or DirectoryNotFoundException or NotSupportedException)
        {
            System.Diagnostics.Trace.TraceWarning("XnaReborn.Compat.Xact: wave '" + wave.Name + "' (" + wave.Asset + ") could not be loaded and will be silent: " + e.Message);
            return null;
        }
    }

    public IXactVoice? Create(XactWaveData wave, object loaded) =>
        loaded switch
        {
            SoundEffect effect => new EffectVoice(effect.CreateInstance()),
            Song song => new SongVoice(song),
            _ => null,
        };

    private sealed class EffectVoice(SoundEffectInstance instance) : IXactVoice
    {
        private bool _started;
        private bool _failed;

        public bool IsFinished => _failed || (_started && instance.State == SoundState.Stopped);

        public bool IsLooped
        {
            set => instance.IsLooped = value;
        }

        public float Volume
        {
            set => instance.Volume = Math.Clamp(value, 0f, 1f);
        }

        public float Pitch
        {
            set => instance.Pitch = Math.Clamp(value, -1f, 1f);
        }

        public void Play()
        {
            try
            {
                instance.Play();
                _started = true;
            }
            catch (InstancePlayLimitException)
            {
                _failed = true;
            }
        }

        public void Pause() => instance.Pause();

        public void Resume() => instance.Resume();

        public void Stop()
        {
            instance.Stop(true);
            _failed = true;
        }

        public void Apply3D(AudioListener listener, AudioEmitter emitter) => instance.Apply3D(listener, emitter);

        public void Dispose() => instance.Dispose();
    }

    // shortcut: MediaPlayer is one global player, so only one song plays at a time and pitch is ignored. Upgrade if a game streams two songs at once.
    private sealed class SongVoice(Song song) : IXactVoice
    {
        private bool _started;
        private bool _stopped;
        private bool _looped;

        public bool IsFinished => _stopped || (_started && (MediaPlayer.State == MediaState.Stopped || MediaPlayer.Queue.ActiveSong != song));

        public bool IsLooped
        {
            set => _looped = value;
        }

        public float Volume
        {
            set => MediaPlayer.Volume = Math.Clamp(value, 0f, 1f);
        }

        public float Pitch
        {
            set
            {
            }
        }

        public void Play()
        {
            MediaPlayer.IsRepeating = _looped;
            MediaPlayer.Play(song);
            _started = true;
        }

        public void Pause() => MediaPlayer.Pause();

        public void Resume() => MediaPlayer.Resume();

        public void Stop()
        {
            if (_started && MediaPlayer.Queue.ActiveSong == song)
            {
                MediaPlayer.Stop();
            }

            _stopped = true;
        }

        public void Apply3D(AudioListener listener, AudioEmitter emitter)
        {
        }

        public void Dispose()
        {
        }
    }
}

/// <summary>Finds catalog entries from the file name a game passes to a constructor.</summary>
internal static class XactNames
{
    /// <summary>The file name without folders and extension, which is how the catalog is keyed.</summary>
    public static string Stem(string path)
    {
        var name = path.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    /// <summary>Finds an entry by stem (case-insensitive) or throws a <see cref="FileNotFoundException"/> that lists what exists.</summary>
    public static T Find<T>(string path, string kind, IEnumerable<T> items, Func<T, string> stem)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(path);
        var wanted = Stem(path);
        var list = items.ToList();
        var found = list.FirstOrDefault(i => string.Equals(stem(i), wanted, StringComparison.OrdinalIgnoreCase));
        if (found is not null)
        {
            return found;
        }

        var known = list.Count == 0 ? "none" : string.Join(", ", list.Select(i => "'" + stem(i) + "'"));
        throw new FileNotFoundException(
            "XnaReborn.Compat: no converted " + kind + " matches '" + path + "'. XNA Reborn replaces XACT files with data built from the .xap, so a file that has no .xap behind it is not available. Converted " + kind + "s: " + known + ".",
            path);
    }
}

/// <summary>How a cue picks one of its sounds. State lives per cue so that repeated plays continue the sequence.</summary>
internal sealed class VariationState
{
    public int Last = -1;

    public List<int> Bag { get; } = [];
}

internal static class XactVariation
{
    /// <summary>Picks the index of the cue entry to play. Types: 0 ordered, 1 ordered from a random start, 2 random, 3 random without immediate repeat, 4 shuffle.</summary>
    public static int Pick(XactCueData cue, VariationState state, Random random)
    {
        var n = cue.Entries.Length;
        int index;
        switch (cue.VariationType)
        {
            case 0:
                index = (state.Last + 1) % n;
                break;
            case 1:
                index = state.Last < 0 ? random.Next(n) : (state.Last + 1) % n;
                break;
            case 2:
                index = Weighted(cue.Entries, random, -1);
                break;
            case 3:
                index = Weighted(cue.Entries, random, n > 1 ? state.Last : -1);
                break;
            case 4:
                if (state.Bag.Count == 0)
                {
                    for (var i = 0; i < n; i++)
                    {
                        state.Bag.Add(i);
                    }

                    for (var i = n - 1; i > 0; i--)
                    {
                        var j = random.Next(i + 1);
                        (state.Bag[i], state.Bag[j]) = (state.Bag[j], state.Bag[i]);
                    }

                    // The first of a new round must not repeat the last of the old one.
                    if (n > 1 && state.Bag[^1] == state.Last)
                    {
                        (state.Bag[^1], state.Bag[0]) = (state.Bag[0], state.Bag[^1]);
                    }
                }

                index = state.Bag[^1];
                state.Bag.RemoveAt(state.Bag.Count - 1);
                break;
            default:
                index = 0;
                break;
        }

        state.Last = index;
        return index;
    }

    private static int Weighted(XactCueEntryData[] entries, Random random, int exclude)
    {
        var weights = entries.Select((e, i) => i == exclude ? 0 : Math.Max(0, e.WeightMax - e.WeightMin)).ToArray();
        if (weights.Sum() == 0)
        {
            weights = entries.Select((_, i) => i == exclude ? 0 : 1).ToArray();
        }

        var roll = random.Next(weights.Sum());
        for (var i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>Picks one wave entry of a play-wave event by weight.</summary>
    public static XactWaveEntryData Entry(XactWaveEntryData[] entries, Random random)
    {
        if (entries.Length == 1)
        {
            return entries[0];
        }

        var total = entries.Sum(e => Math.Max(1, e.Weight));
        var roll = random.Next(total);
        foreach (var entry in entries)
        {
            roll -= Math.Max(1, entry.Weight);
            if (roll < 0)
            {
                return entry;
            }
        }

        return entries[0];
    }
}
