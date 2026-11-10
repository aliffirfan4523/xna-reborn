namespace XnaReborn.Migrator.Projects;

/// <summary>The NuGet packages and target framework chosen for a migrated project.</summary>
/// <param name="FrameworkPackage">MonoGame framework package id.</param>
/// <param name="FrameworkVersion">Version of the framework package.</param>
/// <param name="ContentBuilderPackage">Content builder package id.</param>
/// <param name="ContentBuilderVersion">Version of the content builder package.</param>
/// <param name="TargetFramework">Target framework moniker.</param>
public sealed record PackageSelection(
    string FrameworkPackage,
    string FrameworkVersion,
    string ContentBuilderPackage,
    string ContentBuilderVersion,
    string TargetFramework);

/// <summary>Chooses the MonoGame packages for a <see cref="MonoGameTarget"/>.</summary>
public sealed class PackageSelector
{
    private readonly PackageVersions _versions;

    /// <summary>Creates a selector.</summary>
    /// <param name="versions">Pinned versions, or null for <see cref="PackageVersions.Default"/>.</param>
    public PackageSelector(PackageVersions? versions = null)
    {
        _versions = versions ?? PackageVersions.Default;
    }

    /// <summary>Picks the packages and target framework for a target.</summary>
    /// <param name="target">The MonoGame platform.</param>
    /// <returns>The package ids, versions and target framework.</returns>
    public PackageSelection Select(MonoGameTarget target) => target switch
    {
        MonoGameTarget.DesktopGL => new PackageSelection(
            "MonoGame.Framework.DesktopGL", _versions.MonoGame, "MonoGame.Content.Builder.Task", _versions.ContentBuilder, _versions.TargetFramework),
        MonoGameTarget.WindowsDX => new PackageSelection(
            "MonoGame.Framework.WindowsDX", _versions.MonoGame, "MonoGame.Content.Builder.Task", _versions.ContentBuilder, _versions.WindowsTargetFramework),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown MonoGame target."),
    };
}
