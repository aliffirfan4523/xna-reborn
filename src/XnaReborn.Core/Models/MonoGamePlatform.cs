using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models;

/// <summary>The platform an old MonoGame project was built for.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MonoGamePlatform
{
    /// <summary>Not stated, or not a value the analyzer knows.</summary>
    Unknown,

    /// <summary>Windows desktop with DirectX (<c>MonoGamePlatform</c> value <c>Windows</c>, package WindowsDX).</summary>
    WindowsDX,

    /// <summary>Windows desktop with OpenTK (<c>WindowsGL</c>).</summary>
    WindowsGL,

    /// <summary>Linux desktop (<c>Linux</c>).</summary>
    Linux,

    /// <summary>macOS desktop (<c>MacOSX</c>).</summary>
    MacOS,

    /// <summary>Cross-platform desktop with OpenGL (<c>DesktopGL</c>).</summary>
    DesktopGL,

    /// <summary>Universal Windows Platform or Windows Store app (<c>WindowsStoreApp</c>).</summary>
    Uwp,

    /// <summary>Windows Phone 8 (<c>WindowsPhone8</c>).</summary>
    WindowsPhone,

    /// <summary>OUYA (<c>Ouya</c>).</summary>
    Ouya,

    /// <summary>PlayStation Mobile (<c>PlayStationMobile</c>).</summary>
    PlayStationMobile,

    /// <summary>PlayStation Vita (<c>PSVita</c>).</summary>
    PsVita,

    /// <summary>Android with Xamarin (<c>Android</c>).</summary>
    Android,

    /// <summary>iOS with Xamarin (<c>iOS</c>).</summary>
    Ios,
}
