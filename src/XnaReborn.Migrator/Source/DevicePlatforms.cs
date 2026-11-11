using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Source;

/// <summary>Helpers about the device platforms.</summary>
internal static class DevicePlatforms
{
    /// <summary>Tells whether a platform is Xbox 360, Zune or Windows Phone.</summary>
    /// <param name="platform">The platform of the migrated project.</param>
    internal static bool IsDevice(XnaPlatform platform) =>
        platform is XnaPlatform.Xbox360 or XnaPlatform.Zune or XnaPlatform.WindowsPhone;
}
