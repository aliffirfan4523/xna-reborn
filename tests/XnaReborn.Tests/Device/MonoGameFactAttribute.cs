using XnaReborn.Migrator.Projects;

namespace XnaReborn.Tests.Device;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that compile the generated device sources against the real MonoGame assembly.
/// The assembly is read from the local NuGet cache (it is put there by the integration builds); nothing is downloaded.
/// The test is skipped, with the reason, when the cache does not hold it.
/// </summary>
public sealed class MonoGameFactAttribute : FactAttribute
{
    /// <summary>Creates the attribute and sets <see cref="FactAttribute.Skip"/> when the MonoGame assembly is not cached.</summary>
    public MonoGameFactAttribute()
    {
        if (Path() is null)
        {
            Skip = "MonoGame.Framework.DesktopGL " + PackageVersions.Default.MonoGame + " is not in the local NuGet cache. Build any migrated fixture once (XNAREBORN_INTEGRATION=1) to cache it.";
        }
    }

    /// <summary>Finds MonoGame.Framework.dll of the pinned DesktopGL package in the NuGet cache, or null.</summary>
    internal static string? Path()
    {
        var cache = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (string.IsNullOrWhiteSpace(cache))
        {
            cache = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        }

        var dll = System.IO.Path.Combine(cache, "monogame.framework.desktopgl", PackageVersions.Default.MonoGame, "lib", "net8.0", "MonoGame.Framework.dll");
        return File.Exists(dll) ? dll : null;
    }
}
