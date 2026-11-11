// Part of XnaReborn.Compat, the XACT stand-in that XNA Reborn writes into a migrated copy.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Audio;

namespace XnaReborn.Compat.Xact;

/// <summary>
/// Stand-in for an XACT cue. It picks a sound by the cue's variation type, plays the sound's waves with volume, pitch,
/// loops and RPC curves applied, and tracks its own state. Fades, filters, reverb and effects are not applied.
/// </summary>
public class Cue : IDisposable
{
    private readonly AudioEngine _engine;
    private readonly List<Running> _running = [];
    private readonly List<Delayed> _delayed = [];
    private readonly Dictionary<string, float> _variables;
    private CueState _state = CueState.Prepared;
    private bool _queued;
    private AudioListener? _listener;
    private AudioEmitter? _emitter;

    internal Cue(SoundBank bank, XactCueData data)
    {
        Bank = bank;
        Data = data;
        _engine = bank.Engine;
        _variables = _engine.Data.Variables.Where(v => !v.IsGlobal).ToDictionary(v => v.Name, v => v.InitialValue, StringComparer.OrdinalIgnoreCase);
    }

    private enum CueState
    {
        Prepared,
        Playing,
        Paused,
        Stopped,
    }

    /// <summary>Raised when the cue is disposed.</summary>
    public event EventHandler<EventArgs>? Disposing;

    /// <summary>Gets a value that says whether the cue is paused.</summary>
    public bool IsPaused => _state == CueState.Paused;

    /// <summary>Gets a value that says whether the cue is playing (or waiting for a free instance of its category).</summary>
    public bool IsPlaying => _state == CueState.Playing;

    /// <summary>Gets a value that says whether the cue has stopped or finished.</summary>
    public bool IsStopped => _state == CueState.Stopped;

    /// <summary>Always false: a stop takes effect at once.</summary>
    public bool IsStopping => false;

    /// <summary>Always false.</summary>
    public bool IsPreparing => false;

    /// <summary>Gets a value that says whether the cue is ready to play.</summary>
    public bool IsPrepared { get; internal set; } = true;

    /// <summary>Always false, as in MonoGame.</summary>
    public bool IsCreated { get; internal set; }

    /// <summary>Gets the cue name.</summary>
    public string Name => Data.Name;

    /// <summary>Gets a value that says whether the cue was disposed.</summary>
    public bool IsDisposed { get; internal set; }

    internal SoundBank Bank { get; }

    internal XactCueData Data { get; }

    internal XactSoundData? Sound { get; private set; }

    internal bool AutoDispose { get; set; }

    internal long Serial { get; private set; }

    /// <summary>The last gain applied to the first voice, used to find the quietest cue.</summary>
    internal float CurrentGain { get; private set; }

    /// <summary>Starts the cue, or resumes it when it is paused.</summary>
    public void Play()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        switch (_state)
        {
            case CueState.Paused:
                Resume();
                return;
            case CueState.Playing:
                return;
            case CueState.Stopped:
                throw new InvalidOperationException("XnaReborn.Compat: a cue that has stopped cannot play again. Get a new one with SoundBank.GetCue.");
        }

        Sound = Bank.PickSound(Data);
        _state = CueState.Playing;
        if (!_engine.Admit(this, out var queued))
        {
            if (queued)
            {
                _queued = true;
                _engine.Queue.Add(this);
            }
            else
            {
                Finish();
            }

            return;
        }

        Begin();
    }

    /// <summary>Pauses the cue.</summary>
    public void Pause()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_state != CueState.Playing)
        {
            return;
        }

        _state = CueState.Paused;
        foreach (var run in _running)
        {
            run.Voice.Pause();
        }
    }

    /// <summary>Resumes a paused cue.</summary>
    public void Resume()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_state != CueState.Paused)
        {
            return;
        }

        _state = CueState.Playing;
        foreach (var run in _running)
        {
            run.Voice.Resume();
        }
    }

    /// <summary>Stops the cue at once. A fade is not applied, so AsAuthored and Immediate do the same.</summary>
    /// <param name="options">How to stop.</param>
    public void Stop(AudioStopOptions options)
    {
        if (_state == CueState.Stopped || IsDisposed)
        {
            return;
        }

        Finish();
    }

    /// <summary>Sets a cue variable. The value is kept inside the variable's minimum and maximum.</summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The new value.</param>
    public void SetVariable(string name, float value)
    {
        ArgumentNullException.ThrowIfNull(name);
        var variable = _engine.Data.Variables.FirstOrDefault(v => !v.IsGlobal && string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("XnaReborn.Compat: the XACT project has no cue variable '" + name + "'.");
        if (variable.ReadOnly)
        {
            throw new InvalidOperationException("XnaReborn.Compat: the cue variable '" + name + "' is read-only.");
        }

        _variables[variable.Name] = Math.Clamp(value, variable.Min, variable.Max);
    }

    /// <summary>Gets a cue variable.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The value.</returns>
    public float GetVariable(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _variables.TryGetValue(name, out var value)
            ? value
            : throw new InvalidOperationException("XnaReborn.Compat: the XACT project has no cue variable '" + name + "'.");
    }

    /// <summary>Positions the cue in 3D. Applies to waves that play as sound effects.</summary>
    /// <param name="listener">The listener.</param>
    /// <param name="emitter">The emitter.</param>
    public void Apply3D(AudioListener listener, AudioEmitter emitter)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(emitter);
        _listener = listener;
        _emitter = emitter;
        foreach (var run in _running)
        {
            run.Voice.Apply3D(listener, emitter);
        }
    }

    /// <summary>Stops the cue and releases it.</summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        Stop(AudioStopOptions.Immediate);
        IsDisposed = true;
        Disposing?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>True while the cue holds a slot in its categories.</summary>
    internal bool IsInCategory(string name) =>
        Sound is not null && Sound.Categories.Any(c => InCategory(c, name, 0));

    internal bool IsUsing(string bankName) => _running.Any(r => string.Equals(r.BankName, bankName, StringComparison.Ordinal));

    /// <summary>Starts a queued cue if its categories have room now. Returns true when it left the queue.</summary>
    internal bool TryDequeue()
    {
        if (_state != CueState.Playing || !_queued)
        {
            return true;
        }

        if (!_engine.Admit(this, out var queued) && queued)
        {
            return false;
        }

        _queued = false;
        Begin();
        return true;
    }

    /// <summary>Moves the cue forward: starts delayed events, restarts finished waves that still have loops left, ends the cue when nothing is left.</summary>
    internal void Update(float seconds)
    {
        if (_state != CueState.Playing)
        {
            return;
        }

        foreach (var delayed in _delayed.ToList())
        {
            delayed.Remaining -= seconds;
            if (delayed.Remaining <= 0)
            {
                _delayed.Remove(delayed);
                StartEvent(delayed.Event, Loops(delayed.Event.Event));
            }
        }

        foreach (var run in _running.ToList())
        {
            if (!run.Voice.IsFinished)
            {
                continue;
            }

            _running.Remove(run);
            run.Voice.Dispose();
            if (run.LoopsLeft > 0)
            {
                StartEvent(run.Pending, run.LoopsLeft - 1, run.Entry);
            }
        }

        if (_running.Count == 0 && _delayed.Count == 0)
        {
            Finish();
            return;
        }

        ApplyLevels();
    }

    private bool InCategory(string category, string wanted, int depth)
    {
        if (string.Equals(category, wanted, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return depth <= 8 && _engine.FindCategory(category) is { } data && data.Parents.Any(p => InCategory(p, wanted, depth + 1));
    }

    private void Begin()
    {
        Serial = _engine.NextSerial();
        _engine.Active.Add(this);
        if (Sound is null)
        {
            Finish();
            return;
        }

        foreach (var track in Sound.Tracks)
        {
            foreach (var xactEvent in track.Events)
            {
                var item = new Pending(track, xactEvent);
                if (xactEvent.TimestampMs <= 0)
                {
                    StartEvent(item, Loops(xactEvent));
                }
                else
                {
                    _delayed.Add(new Delayed(item, xactEvent.TimestampMs / 1000.0));
                }
            }
        }

        if (_running.Count == 0 && _delayed.Count == 0)
        {
            Finish();
            return;
        }

        ApplyLevels();
    }

    /// <summary>Starts one play-wave event: 255 loops forever, N plays N more times, 0 plays once.</summary>
    private void StartEvent(Pending pending, int loops, XactWaveEntryData? entry = null)
    {
        var xactEvent = pending.Event;
        if (xactEvent.Entries.Length == 0)
        {
            return;
        }

        entry ??= XactVariation.Entry(xactEvent.Entries, _engine.Random);
        if (!_engine.WaveBanks.TryGetValue(entry.Bank, out var bank))
        {
            return;
        }

        var voice = bank.CreateVoice(entry.Wave);
        if (voice is null)
        {
            return;
        }

        var forever = xactEvent.LoopCount >= 255;
        voice.IsLooped = forever;
        if (_listener is not null && _emitter is not null)
        {
            voice.Apply3D(_listener, _emitter);
        }

        var run = new Running(voice, pending.Track, xactEvent, pending, entry, entry.Bank, forever ? 0 : loops);
        _running.Add(run);
        ApplyLevel(run);
        voice.Play();
    }

    private static int Loops(XactEventData xactEvent) => xactEvent.LoopCount >= 255 ? 0 : Math.Max(0, xactEvent.LoopCount);

    private void ApplyLevels()
    {
        foreach (var run in _running)
        {
            ApplyLevel(run);
        }
    }

    private void ApplyLevel(Running run)
    {
        var sound = Sound!;
        var gain = sound.Volume * run.Track.Volume;
        foreach (var category in sound.Categories)
        {
            gain *= _engine.CategoryGain(category);
        }

        var pitch = sound.Pitch;
        foreach (var rpcName in sound.Rpcs)
        {
            var rpc = _engine.Data.Rpcs.FirstOrDefault(r => string.Equals(r.Name, rpcName, StringComparison.OrdinalIgnoreCase));
            if (rpc is null)
            {
                continue;
            }

            foreach (var curve in rpc.Curves)
            {
                var value = Variable(curve.Variable);
                if (value is null || curve.Points.Length == 0)
                {
                    continue;
                }

                var y = Interpolate(curve.Points, value.Value);
                if (curve.Property == 0)
                {
                    gain *= (float)Math.Pow(10, y / 2000.0);
                }
                else if (curve.Property == 1)
                {
                    pitch += y / 1200f;
                }
            }
        }

        CurrentGain = gain;
        run.Voice.Volume = gain;
        run.Voice.Pitch = Math.Clamp(pitch, -1f, 1f);
    }

    private float? Variable(string name)
    {
        if (_variables.TryGetValue(name, out var cue))
        {
            return cue;
        }

        var global = _engine.GlobalOrNull(name, out var found);
        return found ? global : null;
    }

    /// <summary>Linear interpolation between the curve points; outside the first and last point the end value holds. The curve shape is ignored.</summary>
    internal static float Interpolate(XactRpcPointData[] points, float x)
    {
        var sorted = points.OrderBy(p => p.X).ToArray();
        if (x <= sorted[0].X)
        {
            return sorted[0].Y;
        }

        for (var i = 1; i < sorted.Length; i++)
        {
            if (x <= sorted[i].X)
            {
                var a = sorted[i - 1];
                var b = sorted[i];
                return b.X == a.X ? b.Y : a.Y + ((b.Y - a.Y) * (x - a.X) / (b.X - a.X));
            }
        }

        return sorted[^1].Y;
    }

    private void Finish()
    {
        _state = CueState.Stopped;
        _queued = false;
        foreach (var run in _running)
        {
            run.Voice.Stop();
            run.Voice.Dispose();
        }

        _running.Clear();
        _delayed.Clear();
        _engine.Active.Remove(this);
        _engine.Queue.Remove(this);
        if (AutoDispose)
        {
            IsDisposed = true;
            Disposing?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed record Pending(XactTrackData Track, XactEventData Event);

    private sealed record Running(IXactVoice Voice, XactTrackData Track, XactEventData Event, Pending Pending, XactWaveEntryData Entry, string BankName, int LoopsLeft);

    private sealed class Delayed(Pending item, double remaining)
    {
        public Pending Event { get; } = item;

        public double Remaining { get; set; } = remaining;
    }
}
