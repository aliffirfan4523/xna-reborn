using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Upgrade;

/// <summary>Tells which old MonoGame projects the migrator turns into a desktop project and which it leaves as manual work.</summary>
public static class MigrationScope
{
    /// <summary>
    /// True for a platform this version does not generate output for (UWP, Windows Phone, OUYA, PlayStation Mobile, PS Vita, Android, iOS).
    /// Such a project is copied unchanged and listed as manual work; it is not migrated.
    /// </summary>
    /// <param name="platform">The platform the analyzer detected.</param>
    public static bool IsPlatformProject(MonoGamePlatform platform) =>
        platform is MonoGamePlatform.Uwp or MonoGamePlatform.WindowsPhone or MonoGamePlatform.Ouya
            or MonoGamePlatform.PlayStationMobile or MonoGamePlatform.PsVita or MonoGamePlatform.Android or MonoGamePlatform.Ios;

    /// <summary>The platform in words, for reports and screens. Shared by the migrator manifest and the UI so the two never disagree.</summary>
    /// <param name="platform">The platform the analyzer detected.</param>
    public static string Describe(MonoGamePlatform platform) => platform switch
    {
        MonoGamePlatform.Unknown => "Platform unknown",
        MonoGamePlatform.WindowsDX => "Windows (DirectX)",
        MonoGamePlatform.WindowsGL => "WindowsGL",
        MonoGamePlatform.Linux => "Linux",
        MonoGamePlatform.MacOS => "macOS",
        MonoGamePlatform.DesktopGL => "DesktopGL",
        MonoGamePlatform.Uwp => "UWP",
        MonoGamePlatform.WindowsPhone => "Windows Phone",
        MonoGamePlatform.Ouya => "OUYA",
        MonoGamePlatform.PlayStationMobile => "PlayStation Mobile",
        MonoGamePlatform.PsVita => "PS Vita",
        MonoGamePlatform.Android => "Android",
        MonoGamePlatform.Ios => "iOS",
        _ => platform.ToString(),
    };
}
