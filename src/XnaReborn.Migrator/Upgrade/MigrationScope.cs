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
}
