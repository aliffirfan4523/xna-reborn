using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models;

/// <summary>
/// The best statement of a MonoGame version. When an exact version is known the band is its major.minor line
/// (<see cref="V3Early"/> to <see cref="V38"/>). When only a signal is known the band is the range that signal allows.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MonoGameBand
{
    /// <summary>No signal was found.</summary>
    Unknown,

    /// <summary>An exact version from 3.0 to 3.4.</summary>
    V3Early,

    /// <summary>An exact 3.5.x version.</summary>
    V35,

    /// <summary>An exact 3.6.x version.</summary>
    V36,

    /// <summary>An exact 3.7.x version.</summary>
    V37,

    /// <summary>An exact 3.8.x version.</summary>
    V38,

    /// <summary>Any version from 3.2 to 3.6 (a WindowsGL assembly folder).</summary>
    V32To36,

    /// <summary>Any version from 3.3 to 3.8.1 (a UWP template).</summary>
    V33To381,

    /// <summary>Any version from 3.5 on (a DesktopGL assembly folder or an import of <c>MonoGame.Common.props</c>).</summary>
    V35Plus,

    /// <summary>The 3.8.0 era: an SDK-style project on <c>netcoreapp3.1</c> or <c>net5.0</c>.</summary>
    V380Era,
}
