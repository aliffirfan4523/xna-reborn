using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Projects;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator;

/// <summary>Monogame-upgrade tasks 11.1 to 11.3: the legacy project generator, the packages.config mapper and the 3.8.x bump.</summary>
[Trait("Category", "Migrator")]
public class LegacyMonoGameProjectTests
{
    private static readonly LegacyMonoGameProjectGenerator _generator = new();

    private static async Task<ProjectReadResult> ReadAsync(string fixture, string? relativeProject = null)
    {
        var root = FixturePaths.Of(fixture);
        var csproj = relativeProject is null
            ? Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories).Single()
            : Path.Combine(root, relativeProject);
        return await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(csproj, root, CancellationToken.None);
    }

    private static string[] ContentRefs(ProjectReadResult source) =>
        [.. (source.MgcbFiles ?? []).Select(m => m.Path.Replace('/', '\\'))];

    [Theory]
    [InlineData("mg37-windowsdx-pkgconfig", MonoGameTarget.WindowsDX)]
    [InlineData("mg37-desktopgl-installdir", MonoGameTarget.DesktopGL)]
    [InlineData("mg34-windowsgl", MonoGameTarget.DesktopGL)]
    public async Task Generated_project_for_a_legacy_fixture_matches_snapshot(string fixture, MonoGameTarget target)
    {
        var source = await ReadAsync(fixture);
        var mapping = PackagesConfigMapper.Map(source.Packages);

        var generated = _generator.Generate(source, target, ContentRefs(source), mapping.Mapped);

        Assert.Equal(source.Info.Path, generated.Change.RelativePath);
        Assert.Equal(ChangeKind.Created, generated.Change.Kind);
        await Verifier.Verify(generated.Change.NewText!, "csproj").UseParameters(fixture);
    }

    [Fact]
    public async Task Installer_layout_is_dropped_from_the_generated_project()
    {
        var source = await ReadAsync("mg37-desktopgl-installdir");

        var text = _generator.Generate(source, MonoGameTarget.DesktopGL, ContentRefs(source), []).Change.NewText!;

        Assert.Contains("<PackageReference Include=\"MonoGame.Framework.DesktopGL\" Version=\"3.8.6.1\" />", text);
        Assert.Contains("<PackageReference Include=\"MonoGame.Content.Builder.Task\" Version=\"3.8.6.1\" />", text);
        Assert.Contains("<TargetFramework>net8.0</TargetFramework>", text);
        Assert.Contains("<MonoGameContentReference Include=\"Content\\Content.mgcb\" />", text);
        foreach (var gone in new[] { "MonoGame.Common.props", "MonoGame.Content.Builder.targets", "MonoGamePlatform", "PlatformTarget", "TargetFrameworkVersion", "OpenTK", "NVorbis", "HintPath" })
        {
            Assert.DoesNotContain(gone, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Old_windows_project_keeps_packages_other_than_MonoGame_and_SharpDX()
    {
        var source = await ReadAsync("mg37-windowsdx-pkgconfig");

        var generated = _generator.Generate(source, MonoGameTarget.WindowsDX, ContentRefs(source), PackagesConfigMapper.Map(source.Packages).Mapped);

        Assert.Contains("<TargetFramework>net8.0-windows</TargetFramework>", generated.Change.NewText);
        Assert.Contains("<PackageReference Include=\"Newtonsoft.Json\" Version=\"12.0.3\" />", generated.Change.NewText);
        Assert.DoesNotContain("SharpDX", generated.Change.NewText, StringComparison.Ordinal);
        Assert.DoesNotContain("MonoGame.Framework.WindowsDX\" Version=\"3.7", generated.Change.NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Project_without_content_gets_no_content_builder()
    {
        var source = await ReadAsync("mg34-windowsgl");

        var text = _generator.Generate(source, MonoGameTarget.DesktopGL, [], []).Change.NewText!;

        Assert.DoesNotContain("MonoGame.Content.Builder.Task", text, StringComparison.Ordinal);
        Assert.DoesNotContain("MonoGameContentReference", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dropped_references_are_reported_in_notes()
    {
        var source = await ReadAsync("mg34-windowsgl");

        var notes = _generator.Generate(source, MonoGameTarget.DesktopGL, [], []).Notes;

        Assert.Contains(notes, n => n.Contains("MonoGame.Framework.Net", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.Contains("Lidgren.Network", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Shared_project_files_are_linked_into_the_desktop_project()
    {
        var source = await ReadAsync("mg37-multiplatform-sln", "Desktop/Mg37Desktop.csproj");

        var text = _generator.Generate(source, MonoGameTarget.WindowsDX, [], []).Change.NewText!;

        Assert.Contains("<Compile Include=\"..\\Shared\\SharedGame.cs\" Link=\"SharedGame.cs\" />", text);
    }

    [Fact]
    public async Task Generation_is_deterministic()
    {
        var source = await ReadAsync("mg37-desktopgl-installdir");

        var first = _generator.Generate(source, MonoGameTarget.DesktopGL, ContentRefs(source), []).Change.NewText;
        var second = _generator.Generate(source, MonoGameTarget.DesktopGL, ContentRefs(source), []).Change.NewText;

        Assert.Equal(first, second);
    }

    [Fact]
    public void Packages_config_mapper_sorts_entries_into_mapped_dropped_and_manual()
    {
        var mapping = PackagesConfigMapper.Map(
        [
            new PackageEntry("MonoGame.Framework.WindowsDX", "3.7.0.1708", "net47"),
            new PackageEntry("SharpDX.XAudio2", "4.0.1", "net47"),
            new PackageEntry("Newtonsoft.Json", "12.0.3", "net47"),
            new PackageEntry("StyleCop.Analyzers", "1.1.118", "net47", DevelopmentDependency: true),
            new PackageEntry("Microsoft.Bcl", "1.1.10", "win8"),
            new PackageEntry("Some.Phone.Lib", "2.0.0", "wp8"),
            new PackageEntry("Some.Portable.Lib", "2.0.0", "portable-net45+win8"),
        ]);

        Assert.Equal(["Newtonsoft.Json", "StyleCop.Analyzers"], mapping.Mapped.Select(p => p.Id));
        Assert.Equal("12.0.3", mapping.Mapped[0].Version);
        Assert.Equal(["MonoGame.Framework.WindowsDX", "SharpDX.XAudio2"], mapping.Dropped.Select(p => p.Id));
        Assert.Equal(["Microsoft.Bcl", "Some.Phone.Lib", "Some.Portable.Lib"], mapping.Manual.Select(p => p.Id));
    }

    [Fact]
    public void Packages_config_mapper_keeps_a_net_framework_package_and_accepts_no_entries()
    {
        Assert.Single(PackagesConfigMapper.Map([new PackageEntry("Lib", "1.0.0", "net452")]).Mapped);
        Assert.Empty(PackagesConfigMapper.Map(null).Mapped);
    }

    [Fact]
    public async Task Development_dependency_gets_private_assets_and_others_do_not()
    {
        var source = await ReadAsync("mg34-windowsgl");

        var text = _generator.Generate(
            source,
            MonoGameTarget.DesktopGL,
            [],
            [new PackageEntry("StyleCop.Analyzers", "1.1.118", "net47", DevelopmentDependency: true), new PackageEntry("Lib", "1.0.0", "net47")]).Change.NewText!;

        Assert.Contains("<PackageReference Include=\"StyleCop.Analyzers\" Version=\"1.1.118\" PrivateAssets=\"all\" />", text);
        Assert.Contains("<PackageReference Include=\"Lib\" Version=\"1.0.0\" />", text);
    }

    [Fact]
    public async Task Bump_moves_the_old_pin_fixture_to_the_pinned_versions()
    {
        var path = Path.Combine(FixturePaths.Of("mg38-old-pin"), "Mg38OldPin.csproj");
        var text = await File.ReadAllTextAsync(path);

        var bump = SdkProjectBumper.Bump(text, PackageVersions.Default);

        Assert.Contains("<TargetFramework>net8.0</TargetFramework>", bump.Text);
        Assert.Contains("<PackageReference Include=\"MonoGame.Framework.DesktopGL\" Version=\"3.8.6.1\" />", bump.Text);
        Assert.Contains("<PackageReference Include=\"MonoGame.Content.Builder.Task\" Version=\"3.8.6.1\" />", bump.Text);
        Assert.Empty(bump.Notes);
        await Verifier.Verify(bump.Text, "csproj");
    }

    [Fact]
    public void Bump_leaves_a_current_project_byte_for_byte()
    {
        var text = File.ReadAllText(Path.Combine(FixturePaths.Of("mg38-current"), Directory.GetFiles(FixturePaths.Of("mg38-current"), "*.csproj").Single()));

        Assert.Equal(text, SdkProjectBumper.Bump(text, PackageVersions.Default).Text);
    }

    [Fact]
    public void Bump_keeps_other_packages_and_properties_and_moves_a_windows_target()
    {
        const string text = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net6.0-windows</TargetFramework>
                <UseWindowsForms>true</UseWindowsForms>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Version="3.8.1.303" Include="MonoGame.Framework.WindowsDX" />
                <PackageReference Include="MonoGame.Extended" Version="3.8.0" />
              </ItemGroup>
            </Project>
            """;

        var bump = SdkProjectBumper.Bump(text, PackageVersions.Default).Text;

        Assert.Contains("<TargetFramework>net8.0-windows</TargetFramework>", bump);
        Assert.Contains("Version=\"3.8.6.1\" Include=\"MonoGame.Framework.WindowsDX\"", bump);
        Assert.Contains("<PackageReference Include=\"MonoGame.Extended\" Version=\"3.8.0\" />", bump);
        Assert.Contains("<UseWindowsForms>true</UseWindowsForms>", bump);
    }

    [Fact]
    public void Bump_reports_a_version_it_cannot_change()
    {
        const string text = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="MonoGame.Framework.DesktopGL" />
              </ItemGroup>
            </Project>
            """;

        var bump = SdkProjectBumper.Bump(text, PackageVersions.Default);

        Assert.Equal(text, bump.Text);
        Assert.Single(bump.Notes);
    }

    [Fact]
    public void Tool_manifest_bump_changes_only_the_mgcb_version()
    {
        const string text = "{\n  \"version\": 1,\n  \"tools\": {\n    \"dotnet-ef\": { \"version\": \"7.0.0\", \"commands\": [\"dotnet-ef\"] },\n    \"dotnet-mgcb\": { \"version\": \"3.8.0.1641\", \"commands\": [\"mgcb\"] }\n  }\n}\n";

        var bumped = SdkProjectBumper.BumpToolManifest(text, "3.8.6.1");

        Assert.Contains("\"dotnet-ef\": { \"version\": \"7.0.0\"", bumped);
        Assert.Contains("\"dotnet-mgcb\": { \"version\": \"3.8.6.1\"", bumped);
        var without = text.Replace("dotnet-mgcb", "other", StringComparison.Ordinal);
        Assert.Equal(without, SdkProjectBumper.BumpToolManifest(without, "3.8.6.1"));
    }
}
