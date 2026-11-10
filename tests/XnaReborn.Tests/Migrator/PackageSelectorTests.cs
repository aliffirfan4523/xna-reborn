using XnaReborn.Migrator;
using XnaReborn.Migrator.Projects;

namespace XnaReborn.Tests.Migrator;

/// <summary>Package ids, versions and target frameworks per MonoGame target.</summary>
[Trait("Category", "Migrator")]
public class PackageSelectorTests
{
    [Fact]
    public void Pinned_versions_load_and_are_filled_in()
    {
        var versions = PackageVersions.Load();

        Assert.False(string.IsNullOrWhiteSpace(versions.MonoGame));
        Assert.False(string.IsNullOrWhiteSpace(versions.ContentBuilder));
        Assert.False(string.IsNullOrWhiteSpace(versions.DotnetMgcb));
        Assert.False(string.IsNullOrWhiteSpace(versions.TargetFramework));
        Assert.False(string.IsNullOrWhiteSpace(versions.WindowsTargetFramework));
        Assert.NotEmpty(versions.Sources);
        Assert.Equal("3.8.6.1", PackageVersions.Default.MonoGame);
    }

    [Fact]
    public void DesktopGL_uses_the_DesktopGL_package_and_net8()
    {
        var selection = new PackageSelector().Select(MonoGameTarget.DesktopGL);

        Assert.Equal("MonoGame.Framework.DesktopGL", selection.FrameworkPackage);
        Assert.Equal("net8.0", selection.TargetFramework);
        Assert.Equal("MonoGame.Content.Builder.Task", selection.ContentBuilderPackage);
        Assert.Equal(PackageVersions.Default.MonoGame, selection.FrameworkVersion);
        Assert.Equal(PackageVersions.Default.ContentBuilder, selection.ContentBuilderVersion);
    }

    [Fact]
    public void WindowsDX_uses_the_WindowsDX_package_and_a_windows_target_framework()
    {
        var selection = new PackageSelector().Select(MonoGameTarget.WindowsDX);

        Assert.Equal("MonoGame.Framework.WindowsDX", selection.FrameworkPackage);
        Assert.Equal("net8.0-windows", selection.TargetFramework);
        Assert.Equal("MonoGame.Content.Builder.Task", selection.ContentBuilderPackage);
    }

    [Fact]
    public void Custom_versions_are_used()
    {
        var versions = new PackageVersions { MonoGame = "1.2.3", ContentBuilder = "4.5.6", TargetFramework = "net9.0", WindowsTargetFramework = "net9.0-windows" };

        var selection = new PackageSelector(versions).Select(MonoGameTarget.DesktopGL);

        Assert.Equal("1.2.3", selection.FrameworkVersion);
        Assert.Equal("4.5.6", selection.ContentBuilderVersion);
        Assert.Equal("net9.0", selection.TargetFramework);
    }

    [Fact]
    public void Unknown_target_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PackageSelector().Select((MonoGameTarget)99));
    }
}
