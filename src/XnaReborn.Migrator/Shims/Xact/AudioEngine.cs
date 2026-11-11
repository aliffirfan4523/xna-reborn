// Part of XnaReborn.Compat, the XACT stand-in that XNA Reborn writes into a migrated copy.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework.Audio;

namespace XnaReborn.Compat.Xact;

/// <summary>Stand-in for the XACT audio engine. It reads its settings from the generated catalog instead of an .xgs file.</summary>
public class AudioEngine : IDisposable
{
    /// <summary>The XACT content version MonoGame reports.</summary>
    public const int ContentVersion = 39;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<string, float> _globals;
    private readonly Dictionary<string, float> _categoryVolumes = new(StringComparer.OrdinalIgnoreCase);
    private double _last;
    private long _serial;

    /// <summary>Creates the engine for a settings file such as <c>Content\Sounds.xgs</c>. The file is looked up in the catalog by name; nothing is read from disk.</summary>
    /// <param name="settingsFile">The path the game passes. Only the file name without extension matters.</param>
    public AudioEngine(string settingsFile)
        : this(XactNames.Find(settingsFile, "audio engine", XactCatalog.Engines, e => e.Stem), new XactVoiceFactory(XactCatalog.ContentRoot), new Random())
    {
    }

    /// <summary>Creates the engine for a settings file. The look-ahead time and renderer are accepted and ignored.</summary>
    /// <param name="settingsFile">The path the game passes. Only the file name without extension matters.</param>
    /// <param name="lookAheadTime">Ignored.</param>
    /// <param name="rendererId">Ignored.</param>
    public AudioEngine(string settingsFile, TimeSpan lookAheadTime, string rendererId)
        : this(settingsFile)
    {
    }

    internal AudioEngine(XactEngineData data, IXactVoiceFactory factory, Random random)
    {
        Data = data;
        Factory = factory;
        Random = random;
        _globals = data.Variables.Where(v => v.IsGlobal).ToDictionary(v => v.Name, v => v.InitialValue, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Raised when the engine is disposed.</summary>
    public event EventHandler<EventArgs>? Disposing;

    /// <summary>Gets a value that says whether the engine was disposed.</summary>
    public bool IsDisposed { get; private set; }

    internal XactEngineData Data { get; }

    internal IXactVoiceFactory Factory { get; }

    internal Random Random { get; }

    /// <summary>Wave banks created for this engine, by bank name (the name the sound bank data uses).</summary>
    internal Dictionary<string, WaveBank> WaveBanks { get; } = new(StringComparer.Ordinal);

    /// <summary>Cues that are playing or paused.</summary>
    internal List<Cue> Active { get; } = [];

    /// <summary>Cues that wait for a free instance of a category.</summary>
    internal List<Cue> Queue { get; } = [];

    internal long NextSerial() => ++_serial;

    /// <summary>Moves cue timers forward by the time since the last call and cleans up finished cues.</summary>
    public void Update()
    {
        ThrowIfDisposed();
        var now = _clock.Elapsed.TotalSeconds;
        var dt = now - _last;
        _last = now;
        Advance((float)dt);
    }

    /// <summary>Gets a sound category by name.</summary>
    /// <param name="name">The category name from the XACT project.</param>
    /// <returns>The category.</returns>
    public AudioCategory GetCategory(string name)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(name);
        if (Data.Categories.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return new AudioCategory(this, Data.Categories.First(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)).Name);
        }

        // XACT always has Global, Default and Music even when the project does not list them.
        if (name is "Global" or "Default" or "Music")
        {
            return new AudioCategory(this, name);
        }

        throw new InvalidOperationException("XnaReborn.Compat: the XACT project has no category '" + name + "'. Categories: " + string.Join(", ", Data.Categories.Select(c => "'" + c.Name + "'")) + ".");
    }

    /// <summary>Gets the value of a global variable.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The value.</returns>
    public float GetGlobalVariable(string name)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(name);
        return _globals.TryGetValue(name, out var value)
            ? value
            : throw new InvalidOperationException("XnaReborn.Compat: the XACT project has no global variable '" + name + "'.");
    }

    /// <summary>Sets the value of a global variable. The value is kept inside the variable's minimum and maximum.</summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The new value.</param>
    public void SetGlobalVariable(string name, float value)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(name);
        var variable = Data.Variables.FirstOrDefault(v => v.IsGlobal && string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("XnaReborn.Compat: the XACT project has no global variable '" + name + "'.");
        if (variable.ReadOnly)
        {
            throw new InvalidOperationException("XnaReborn.Compat: the global variable '" + name + "' is read-only.");
        }

        _globals[variable.Name] = Math.Clamp(value, variable.Min, variable.Max);
    }

    /// <summary>Stops every cue and releases the engine.</summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        foreach (var cue in Active.Concat(Queue).Distinct().ToList())
        {
            cue.Stop(AudioStopOptions.Immediate);
        }

        IsDisposed = true;
        Disposing?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves time forward by hand. <see cref="Update"/> calls this with the measured time.</summary>
    internal void Advance(float seconds)
    {
        foreach (var cue in Active.ToList())
        {
            cue.Update(seconds);
        }

        foreach (var cue in Queue.ToList())
        {
            if (cue.TryDequeue())
            {
                Queue.Remove(cue);
            }
        }
    }

    internal float GlobalOrNull(string name, out bool found)
    {
        found = _globals.TryGetValue(name, out var value);
        return value;
    }

    /// <summary>Linear gain of a category: its own volume, the volume set by the game, and its parents.</summary>
    internal float CategoryGain(string name, int depth = 0)
    {
        var data = Data.Categories.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        var gain = _categoryVolumes.TryGetValue(name, out var set) ? set : 1f;
        if (data is null || depth > 8)
        {
            return gain;
        }

        gain *= data.Volume;
        foreach (var parent in data.Parents)
        {
            gain *= CategoryGain(parent, depth + 1);
        }

        return gain;
    }

    internal void SetCategoryVolume(string name, float volume) => _categoryVolumes[name] = Math.Max(0f, volume);

    internal XactCategoryData? FindCategory(string name) =>
        Data.Categories.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Applies the instance limits of the sound's categories before a cue starts. Returns false when the cue must not start now
    /// (it was refused, or it waits in the queue).
    /// </summary>
    internal bool Admit(Cue cue, out bool queued)
    {
        queued = false;
        var sound = cue.Sound;
        if (sound is null)
        {
            return false;
        }

        foreach (var name in sound.Categories)
        {
            var category = FindCategory(name);
            if (category is null || category.MaxInstances <= 0 || category.MaxInstances >= 255)
            {
                continue;
            }

            var live = Active.Where(c => c != cue && c.IsInCategory(name)).ToList();
            while (live.Count >= category.MaxInstances)
            {
                switch (category.Behavior)
                {
                    case 1:
                        queued = true;
                        return false;
                    case 2:
                        Evict(live, live.OrderBy(c => c.Serial).First());
                        break;
                    case 3:
                        Evict(live, live.OrderBy(c => c.CurrentGain).ThenBy(c => c.Serial).First());
                        break;
                    case 4:
                        Evict(live, live.OrderBy(c => c.Sound!.Priority).ThenBy(c => c.Serial).First());
                        break;
                    default:
                        // Behavior 0 (fail to play) and any value this shim does not know: the new cue does not play.
                        return false;
                }
            }
        }

        return true;
    }

    private static void Evict(List<Cue> live, Cue victim)
    {
        live.Remove(victim);
        victim.Stop(AudioStopOptions.Immediate);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);
}
