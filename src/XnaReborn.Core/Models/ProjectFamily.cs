using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models;

/// <summary>The kind of game project a .csproj is.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectFamily
{
    /// <summary>An XNA Game Studio project. This is the default, so existing callers keep their meaning.</summary>
    Xna,

    /// <summary>A MonoGame project that is not yet an SDK-style 3.8.6.1 desktop project.</summary>
    MonoGameLegacy,

    /// <summary>An SDK-style MonoGame project on the pinned 3.8.6.1 packages and net8.0.</summary>
    MonoGameCurrent,

    /// <summary>A project that is neither XNA nor MonoGame, or one that could not be read.</summary>
    Other,
}
