// Part of XnaReborn.Compat, the XACT stand-in that XNA Reborn writes into a migrated copy.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Audio;

namespace XnaReborn.Compat.Xact;

/// <summary>Stand-in for an XACT sound bank. Cue and sound data come from the generated catalog.</summary>
public class SoundBank : IDisposable
{
    private readonly AudioEngine _engine;
    private readonly Dictionary<string, XactSoundData> _sounds;
    private readonly Dictionary<string, XactCueData> _cues;
    private readonly Dictionary<string, VariationState> _variations = new(StringComparer.Ordinal);

    /// <summary>Loads a sound bank. The file is looked up in the catalog by name; nothing is read from disk.</summary>
    /// <param name="audioEngine">The engine.</param>
    /// <param name="fileName">The path the game passes, such as <c>Content\Sound Bank.xsb</c>.</param>
    public SoundBank(AudioEngine audioEngine, string fileName)
        : this(audioEngine, XactNames.Find(fileName, "sound bank", XactCatalog.SoundBanks, b => b.Stem))
    {
    }

    internal SoundBank(AudioEngine audioEngine, XactSoundBankData data)
    {
        ArgumentNullException.ThrowIfNull(audioEngine);
        _engine = audioEngine;
        Data = data;
        _sounds = data.Sounds.GroupBy(s => s.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        _cues = data.Cues.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Raised when the bank is disposed.</summary>
    public event EventHandler<EventArgs>? Disposing;

    /// <summary>Gets a value that says whether a cue of this bank is playing.</summary>
    public bool IsInUse => _engine.Active.Any(c => ReferenceEquals(c.Bank, this));

    /// <summary>Gets a value that says whether the bank was disposed.</summary>
    public bool IsDisposed { get; private set; }

    internal XactSoundBankData Data { get; }

    internal AudioEngine Engine => _engine;

    /// <summary>Gets a new cue to control by hand.</summary>
    /// <param name="name">The cue name from the XACT project.</param>
    /// <returns>A prepared cue that is not playing yet.</returns>
    public Cue GetCue(string name)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(name);
        if (!_cues.TryGetValue(name, out var data))
        {
            throw new ArgumentException("XnaReborn.Compat: the sound bank '" + Data.Name + "' has no cue '" + name + "'. Cues: " + string.Join(", ", Data.Cues.Select(c => "'" + c.Name + "'")) + ".", nameof(name));
        }

        return new Cue(this, data);
    }

    /// <summary>Plays a cue once and cleans it up when it ends.</summary>
    /// <param name="name">The cue name.</param>
    public void PlayCue(string name)
    {
        var cue = GetCue(name);
        cue.AutoDispose = true;
        cue.Play();
    }

    /// <summary>Plays a cue once with 3D positioning and cleans it up when it ends.</summary>
    /// <param name="name">The cue name.</param>
    /// <param name="listener">The listener.</param>
    /// <param name="emitter">The emitter.</param>
    public void PlayCue(string name, AudioListener listener, AudioEmitter emitter)
    {
        var cue = GetCue(name);
        cue.AutoDispose = true;
        cue.Apply3D(listener, emitter);
        cue.Play();
    }

    /// <summary>Releases the bank.</summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        foreach (var cue in _engine.Active.Concat(_engine.Queue).Where(c => ReferenceEquals(c.Bank, this)).ToList())
        {
            cue.Stop(AudioStopOptions.Immediate);
        }

        IsDisposed = true;
        Disposing?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Picks the sound a new play of the cue uses, following the cue's variation type.</summary>
    internal XactSoundData? PickSound(XactCueData cue)
    {
        if (cue.Entries.Length == 0)
        {
            return null;
        }

        if (!_variations.TryGetValue(cue.Name, out var state))
        {
            state = new VariationState();
            _variations[cue.Name] = state;
        }

        var entry = cue.Entries[XactVariation.Pick(cue, state, _engine.Random)];
        return _sounds.TryGetValue(entry.Sound, out var sound) ? sound : null;
    }
}
