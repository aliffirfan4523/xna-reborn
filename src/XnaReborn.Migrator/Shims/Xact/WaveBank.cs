// Part of XnaReborn.Compat, the XACT stand-in that XNA Reborn writes into a migrated copy.
using System;
using System.Collections.Generic;
using System.Linq;

namespace XnaReborn.Compat.Xact;

/// <summary>Stand-in for an XACT wave bank. The waves are content assets that XNA Reborn built from the .xap.</summary>
public class WaveBank : IDisposable
{
    private readonly AudioEngine _engine;
    private readonly Dictionary<string, object?> _loaded = new(StringComparer.Ordinal);

    /// <summary>Loads a wave bank. The file is looked up in the catalog by name; nothing is read from disk.</summary>
    /// <param name="audioEngine">The engine.</param>
    /// <param name="nonStreamingWaveBankFilename">The path the game passes, such as <c>Content\Wave Bank.xwb</c>.</param>
    public WaveBank(AudioEngine audioEngine, string nonStreamingWaveBankFilename)
        : this(audioEngine, XactNames.Find(nonStreamingWaveBankFilename, "wave bank", XactCatalog.WaveBanks, b => b.Stem))
    {
    }

    /// <summary>Loads a streaming wave bank. It loads the same way as a normal bank; the offset and packet size are ignored.</summary>
    /// <param name="audioEngine">The engine.</param>
    /// <param name="streamingWaveBankFilename">The path the game passes.</param>
    /// <param name="offset">Ignored.</param>
    /// <param name="packetsize">Ignored.</param>
    public WaveBank(AudioEngine audioEngine, string streamingWaveBankFilename, int offset, short packetsize)
        : this(audioEngine, streamingWaveBankFilename)
    {
    }

    internal WaveBank(AudioEngine audioEngine, XactWaveBankData data)
    {
        ArgumentNullException.ThrowIfNull(audioEngine);
        _engine = audioEngine;
        Data = data;

        // Sound effects load now, as XNA loads a non-streaming bank. Songs load when first used.
        foreach (var wave in data.Waves.Where(w => !w.Missing && w.Kind == XactWaveKind.SoundEffect))
        {
            _loaded[wave.Name] = audioEngine.Factory.Load(wave);
        }

        audioEngine.WaveBanks[data.Name] = this;
        IsPrepared = true;
    }

    /// <summary>Raised when the bank is disposed.</summary>
    public event EventHandler<EventArgs>? Disposing;

    /// <summary>Gets a value that says whether a cue is playing a wave of this bank.</summary>
    public bool IsInUse => _engine.Active.Any(c => c.IsUsing(Data.Name));

    /// <summary>Gets a value that says whether the bank is ready.</summary>
    public bool IsPrepared { get; private set; }

    /// <summary>Gets a value that says whether the bank was disposed.</summary>
    public bool IsDisposed { get; private set; }

    internal XactWaveBankData Data { get; }

    /// <summary>Makes a voice for a wave, or null when the wave is missing or could not be loaded.</summary>
    internal IXactVoice? CreateVoice(string waveName)
    {
        var wave = Data.Waves.FirstOrDefault(w => string.Equals(w.Name, waveName, StringComparison.Ordinal));
        if (wave is null || wave.Missing || IsDisposed)
        {
            return null;
        }

        if (!_loaded.TryGetValue(wave.Name, out var asset))
        {
            asset = _engine.Factory.Load(wave);
            _loaded[wave.Name] = asset;
        }

        return asset is null ? null : _engine.Factory.Create(wave, asset);
    }

    /// <summary>Releases the bank. Cues that use it go silent.</summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        IsPrepared = false;
        _engine.WaveBanks.Remove(Data.Name);
        Disposing?.Invoke(this, EventArgs.Empty);
    }
}
