// Part of XnaReborn.Compat, the XACT stand-in that XNA Reborn writes into a migrated copy.
using System;
using System.Linq;
using Microsoft.Xna.Framework.Audio;

namespace XnaReborn.Compat.Xact;

/// <summary>A sound category. Operations apply to the cues that are playing now in the category.</summary>
public struct AudioCategory : IEquatable<AudioCategory>
{
    private readonly AudioEngine? _engine;
    private readonly string? _name;

    internal AudioCategory(AudioEngine engine, string name)
    {
        _engine = engine;
        _name = name;
    }

    /// <summary>Gets the category name.</summary>
    public readonly string Name => _name ?? string.Empty;

    /// <summary>Compares two categories.</summary>
    public static bool operator ==(AudioCategory first, AudioCategory second) => first.Equals(second);

    /// <summary>Compares two categories.</summary>
    public static bool operator !=(AudioCategory first, AudioCategory second) => !first.Equals(second);

    /// <summary>Pauses the cues of the category.</summary>
    public readonly void Pause() => ForEach(c => c.Pause());

    /// <summary>Resumes the cues of the category.</summary>
    public readonly void Resume() => ForEach(c => c.Resume());

    /// <summary>Stops the cues of the category. A fade is not applied: <see cref="AudioStopOptions.AsAuthored"/> stops at once, like Immediate.</summary>
    /// <param name="options">How to stop.</param>
    public readonly void Stop(AudioStopOptions options) => ForEach(c => c.Stop(options));

    /// <summary>Sets the volume of the category as a linear factor, 1 being unchanged.</summary>
    /// <param name="volume">The factor. Negative values count as 0.</param>
    public readonly void SetVolume(float volume) => _engine?.SetCategoryVolume(Name, volume);

    /// <inheritdoc />
    public readonly bool Equals(AudioCategory other) =>
        ReferenceEquals(_engine, other._engine) && string.Equals(_name, other._name, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is AudioCategory other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    /// <inheritdoc />
    public override readonly string ToString() => Name;

    private readonly void ForEach(Action<Cue> action)
    {
        if (_engine is null)
        {
            return;
        }

        var name = Name;
        foreach (var cue in _engine.Active.Where(c => c.IsInCategory(name)).ToList())
        {
            action(cue);
        }
    }
}
