using XnaReborn.Analyzer.Discovery;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Discovery;

/// <summary>Task 4: project family, MonoGame version, platform, packages.config, shared items, and the no-project reasons.</summary>
public sealed class MonoGameDetectionTests : IDisposable
{
    private const string _ns = "http://schemas.microsoft.com/developer/msbuild/2003";

    private readonly string _work = Path.Combine(Path.GetTempPath(), "xnareborn-mgdetect-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    private static Task<DiscoveryResult> DiscoverAsync(string path) =>
        new ProjectDiscovery(new ReadOnlyFileSystem()).DiscoverAsync(path, CancellationToken.None);

    private static async Task<ProjectReadResult> ReadAsync(string fixture, string project)
    {
        var root = FixturePaths.Of(fixture);
        return await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(root, project.Replace('/', Path.DirectorySeparatorChar)), root, CancellationToken.None);
    }

    private string Project(string name, string body, string? packagesConfig = null, string attributes = "")
    {
        var folder = Path.Combine(_work, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name + ".csproj"), $"""<Project xmlns="{_ns}" {attributes}>{body}</Project>""");
        if (packagesConfig is not null)
        {
            File.WriteAllText(Path.Combine(folder, "packages.config"), packagesConfig);
        }

        return folder;
    }

    private static async Task<ProjectReadResult> ReadFolderAsync(string folder)
    {
        var project = Directory.GetFiles(folder, "*.csproj").Single();
        return await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(project, folder, CancellationToken.None);
    }

    [Theory]
    [InlineData("mg37-windowsdx-pkgconfig", "Mg37WindowsDx.csproj", "3.7.0.1708", MonoGameBand.V37, Confidence.High, MonoGamePlatform.WindowsDX)]
    [InlineData("mg37-desktopgl-installdir", "Mg37DesktopGl.csproj", null, MonoGameBand.V35Plus, Confidence.Medium, MonoGamePlatform.DesktopGL)]
    [InlineData("mg34-windowsgl", "Mg34WindowsGl.csproj", null, MonoGameBand.V32To36, Confidence.Medium, MonoGamePlatform.WindowsGL)]
    [InlineData("mg35-win8-wp8-ouya", "Phone/Mg35Phone.csproj", null, MonoGameBand.Unknown, Confidence.Unknown, MonoGamePlatform.WindowsPhone)]
    [InlineData("mg35-win8-wp8-ouya", "Ouya/Mg35Ouya.csproj", null, MonoGameBand.Unknown, Confidence.Unknown, MonoGamePlatform.Ouya)]
    [InlineData("mg37-multiplatform-sln", "Desktop/Mg37Desktop.csproj", null, MonoGameBand.Unknown, Confidence.Unknown, MonoGamePlatform.WindowsDX)]
    [InlineData("mg37-multiplatform-sln", "Android/Mg37Android.csproj", null, MonoGameBand.Unknown, Confidence.Unknown, MonoGamePlatform.Android)]
    [InlineData("mg37-multiplatform-sln", "Ios/Mg37Ios.csproj", null, MonoGameBand.Unknown, Confidence.Unknown, MonoGamePlatform.Ios)]
    [InlineData("mg37-multiplatform-sln", "Uwp/Mg37Uwp.csproj", null, MonoGameBand.V33To381, Confidence.Medium, MonoGamePlatform.Uwp)]
    [InlineData("mg38-old-pin", "Mg38OldPin.csproj", "3.8.0.1641", MonoGameBand.V38, Confidence.High, MonoGamePlatform.DesktopGL)]
    [InlineData("mg38-current", "Mg38Current.csproj", "3.8.6.1", MonoGameBand.V38, Confidence.High, MonoGamePlatform.DesktopGL)]
    [InlineData("mg37-source-apis", "Mg37SourceApis.csproj", null, MonoGameBand.Unknown, Confidence.Unknown, MonoGamePlatform.WindowsDX)]
    public async Task Fixture_version_and_platform(string fixture, string project, string? exact, MonoGameBand band, Confidence confidence, MonoGamePlatform platform)
    {
        var read = await ReadAsync(fixture, project);

        Assert.Equal(new MonoGameVersionInfo(exact, band, confidence), read.Info.MonoGameVersion);
        Assert.Equal(platform, read.Info.MonoGamePlatform);
        Assert.DoesNotContain(read.Diagnostics, d => d.Code == "ANA009");
    }

    [Theory]
    [InlineData("mg37-windowsdx-pkgconfig", ProjectFamily.MonoGameLegacy)]
    [InlineData("mg37-desktopgl-installdir", ProjectFamily.MonoGameLegacy)]
    [InlineData("mg34-windowsgl", ProjectFamily.MonoGameLegacy)]
    [InlineData("mg35-win8-wp8-ouya", ProjectFamily.MonoGameLegacy)]
    [InlineData("mg37-multiplatform-sln", ProjectFamily.MonoGameLegacy)]
    [InlineData("mg38-old-pin", ProjectFamily.MonoGameLegacy)]
    [InlineData("mg38-current", ProjectFamily.MonoGameCurrent)]
    [InlineData("mg37-source-apis", ProjectFamily.MonoGameLegacy)]
    public async Task Fixture_family_for_every_project(string fixture, ProjectFamily family)
    {
        var result = await DiscoverAsync(FixturePaths.Of(fixture));

        Assert.NotEmpty(result.Projects);
        Assert.All(result.Projects, p => Assert.Equal(family, p.Family));
        Assert.Equal(result.Projects.Count, result.MonoGameProjects.Count());
        Assert.Empty(result.XnaProjects);
    }

    [Theory]
    [InlineData("xna40-basic", ProjectFamily.Xna)]
    [InlineData("xna31-legacy", ProjectFamily.Xna)]
    [InlineData("not-xna", ProjectFamily.Other)]
    public async Task Xna_and_plain_fixtures_keep_their_family(string fixture, ProjectFamily family)
    {
        var result = await DiscoverAsync(FixturePaths.Of(fixture));

        Assert.All(result.Projects, p => Assert.Equal(family, p.Family));
        Assert.Empty(result.MonoGameProjects);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Xna_project_reads_with_family_xna_and_no_monogame_fields()
    {
        var read = await ReadAsync("xna40-basic", "Xna40Basic.csproj");

        Assert.Equal(ProjectFamily.Xna, read.Info.Family);
        Assert.True(read.Info.IsXna);
        Assert.Null(read.Info.MonoGameVersion);
        Assert.Equal(MonoGamePlatform.Unknown, read.Info.MonoGamePlatform);
    }

    [Fact]
    public async Task Non_xna_non_monogame_project_reads_as_other()
    {
        var read = await ReadAsync("not-xna", "NotXna.csproj");

        Assert.Equal(ProjectFamily.Other, read.Info.Family);
        Assert.False(read.Info.IsXna);
    }

    [Fact]
    public async Task Protobuild_only_folder_explains_what_to_do_and_runs_nothing()
    {
        var result = await DiscoverAsync(FixturePaths.Of("mg3x-protobuild"));

        Assert.Empty(result.Projects);
        Assert.StartsWith("Found Protobuild files (", result.NoProjectReason, StringComparison.Ordinal);
        Assert.Contains("Protobuild.exe", result.NoProjectReason, StringComparison.Ordinal);
        Assert.Contains("Build/Projects/Game.definition", result.NoProjectReason, StringComparison.Ordinal);
        Assert.Contains("outside XNA Reborn", result.NoProjectReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task All_current_projects_give_the_already_current_reason()
    {
        var result = await DiscoverAsync(FixturePaths.Of("mg38-current"));

        Assert.Equal("All MonoGame projects are already current. Nothing to migrate.", result.NoProjectReason);
    }

    [Fact]
    public async Task A_folder_with_xna_and_monogame_projects_leaves_no_reason()
    {
        var result = await DiscoverAsync(FixturePaths.Root);

        Assert.Null(result.NoProjectReason);
        Assert.Contains(result.Projects, p => p.Family == ProjectFamily.MonoGameCurrent);
        Assert.Contains(result.Projects, p => p.Family == ProjectFamily.Xna);
    }

    [Fact]
    public async Task Version_signals_that_disagree_report_the_higher_one_and_ANA009()
    {
        var folder = Project(
            "Mixed",
            """<ItemGroup><Reference Include="MonoGame.Framework, Version=3.7.0.213, Culture=neutral" /></ItemGroup>""",
            """<packages><package id="MonoGame.Framework.WindowsDX" version="3.8.1.303" targetFramework="net47" /></packages>""");

        var read = await ReadFolderAsync(folder);

        Assert.Equal(new MonoGameVersionInfo("3.8.1.303", MonoGameBand.V38, Confidence.High), read.Info.MonoGameVersion);
        var diagnostic = Assert.Single(read.Diagnostics, d => d.Code == "ANA009");
        Assert.Contains("3.7.0.213", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("3.8.1.303", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_exact_version_outside_a_band_signal_reports_ANA009_and_keeps_the_exact_version()
    {
        var folder = Project(
            "OutsideBand",
            """<ItemGroup><Reference Include="MonoGame.Framework, Version=3.8.1.303"><HintPath>$(MonoGameInstallDirectory)\MonoGame\v3.0\Assemblies\WindowsGL\MonoGame.Framework.dll</HintPath></Reference></ItemGroup>""");

        var read = await ReadFolderAsync(folder);

        Assert.Equal("3.8.1.303", read.Info.MonoGameVersion!.Exact);
        Assert.Contains(read.Diagnostics, d => d.Code == "ANA009" && d.Message.Contains("WindowsGL", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Signals_that_agree_do_not_report_a_conflict()
    {
        var folder = Project(
            "Agree",
            """<ItemGroup><Reference Include="MonoGame.Framework, Version=3.7.0.1708"><HintPath>..\packages\MonoGame.Framework.WindowsDX.3.7.0.1708\lib\net45\MonoGame.Framework.dll</HintPath></Reference></ItemGroup>""",
            """<packages><package id="MonoGame.Framework.WindowsDX" version="3.7.0.1708" /></packages>""");

        var read = await ReadFolderAsync(folder);

        Assert.Equal("3.7.0.1708", read.Info.MonoGameVersion!.Exact);
        Assert.Empty(read.Diagnostics);
    }

    [Fact]
    public async Task A_package_path_alone_gives_an_exact_version_and_platform()
    {
        var folder = Project(
            "PathOnly",
            """<ItemGroup><Reference Include="MonoGame.Framework"><HintPath>..\packages\MonoGame.Framework.DesktopGL.3.6.0.1625\lib\net40\MonoGame.Framework.dll</HintPath></Reference></ItemGroup>""");

        var read = await ReadFolderAsync(folder);

        Assert.Equal(new MonoGameVersionInfo("3.6.0.1625", MonoGameBand.V36, Confidence.High), read.Info.MonoGameVersion);
        Assert.Equal(MonoGamePlatform.DesktopGL, read.Info.MonoGamePlatform);
    }

    [Fact]
    public async Task A_net_core_3_1_package_project_is_the_3_8_0_era()
    {
        var folder = Project(
            "Era",
            """<PropertyGroup><TargetFramework>netcoreapp3.1</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include="MonoGame.Framework.DesktopGL" /></ItemGroup>""",
            attributes: """Sdk="Microsoft.NET.Sdk" """);

        var read = await ReadFolderAsync(folder);

        Assert.Equal(new MonoGameVersionInfo(null, MonoGameBand.V380Era, Confidence.Medium), read.Info.MonoGameVersion);
        Assert.Equal(ProjectFamily.MonoGameLegacy, read.Info.Family);
    }

    [Fact]
    public async Task No_signal_gives_Unknown_and_does_not_guess()
    {
        var folder = Project("Bare", """<PropertyGroup><MonoGamePlatform>Windows</MonoGamePlatform></PropertyGroup>""");

        var read = await ReadFolderAsync(folder);

        Assert.Equal(MonoGameVersionInfo.Unknown, read.Info.MonoGameVersion);
        Assert.Equal(MonoGamePlatform.WindowsDX, read.Info.MonoGamePlatform);
    }

    [Theory]
    [InlineData("""Sdk="Microsoft.NET.Sdk" """, "net8.0", "3.8.6.1", ProjectFamily.MonoGameCurrent)]
    [InlineData("""Sdk="Microsoft.NET.Sdk" """, "net8.0-windows", "3.8.6.1", ProjectFamily.MonoGameCurrent)]
    [InlineData("""Sdk="Microsoft.NET.Sdk" """, "net8.0", "3.8.1.303", ProjectFamily.MonoGameLegacy)]
    [InlineData("""Sdk="Microsoft.NET.Sdk" """, "net6.0", "3.8.6.1", ProjectFamily.MonoGameLegacy)]
    [InlineData("""Sdk="Microsoft.NET.Sdk" """, "net8.0", "", ProjectFamily.MonoGameLegacy)]
    [InlineData("", "net8.0", "3.8.6.1", ProjectFamily.MonoGameLegacy)]
    public async Task Current_needs_the_sdk_net8_and_the_pinned_version(string attributes, string framework, string version, ProjectFamily expected)
    {
        var versionAttribute = version.Length == 0 ? string.Empty : $"Version=\"{version}\"";
        var folder = Project(
            "Pin",
            $"""<PropertyGroup><TargetFramework>{framework}</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include="MonoGame.Framework.DesktopGL" {versionAttribute} /></ItemGroup>""",
            attributes: attributes);

        var result = await DiscoverAsync(folder);

        Assert.Equal(expected, Assert.Single(result.Projects).Family);
    }

    [Fact]
    public async Task One_package_below_the_pin_makes_the_project_legacy()
    {
        var folder = Project(
            "TwoPackages",
            """<PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.6.1" /><PackageReference Include="MonoGame.Framework.Content.Pipeline" Version="3.8.0.1641" /></ItemGroup>""",
            attributes: """Sdk="Microsoft.NET.Sdk" """);

        var result = await DiscoverAsync(folder);

        Assert.Equal(ProjectFamily.MonoGameLegacy, Assert.Single(result.Projects).Family);
    }

    [Theory]
    [InlineData("""<ItemGroup><Reference Include="MonoGame.Framework" /></ItemGroup>""")]
    [InlineData("""<Import Project="$(MSBuildExtensionsPath)\MonoGame\v3.0\MonoGame.Common.props" />""")]
    [InlineData("""<Import Project="$(MSBuildExtensionsPath)\MonoGame\v3.0\MonoGame.Content.Builder.targets" />""")]
    [InlineData("""<PropertyGroup><MonoGamePlatform>Windows</MonoGamePlatform></PropertyGroup>""")]
    [InlineData("""<ItemGroup><MonoGameContentReference Include="Content\Content.mgcb" /></ItemGroup>""")]
    public async Task Each_monogame_signal_alone_marks_the_project(string body)
    {
        var result = await DiscoverAsync(Project("Signal", body));

        Assert.Equal(ProjectFamily.MonoGameLegacy, Assert.Single(result.Projects).Family);
    }

    [Fact]
    public async Task A_packages_config_entry_alone_marks_the_project()
    {
        var folder = Project("PkgOnly", """<PropertyGroup><OutputType>Exe</OutputType></PropertyGroup>""", """<packages><package id="MonoGame.Framework.WindowsGL" version="3.6.0.1625" /></packages>""");

        var result = await DiscoverAsync(folder);

        Assert.Equal(ProjectFamily.MonoGameLegacy, Assert.Single(result.Projects).Family);
    }

    [Fact]
    public async Task Unrelated_packages_and_references_do_not_mark_the_project()
    {
        var folder = Project(
            "Plain",
            """<ItemGroup><Reference Include="System.Xml" /><PackageReference Include="Newtonsoft.Json" Version="13.0.1" /><PackageReference Include="MonoGame.Content.Builder.Task" Version="3.8.6.1" /></ItemGroup>""",
            """<packages><package id="Newtonsoft.Json" version="12.0.3" /></packages>""");

        var result = await DiscoverAsync(folder);

        Assert.Equal(ProjectFamily.Other, Assert.Single(result.Projects).Family);
    }

    [Fact]
    public async Task A_project_with_xna_and_monogame_references_stays_xna_and_reports_ANA008()
    {
        var folder = Project(
            "Both",
            """<ItemGroup><Reference Include="Microsoft.Xna.Framework, Version=4.0.0.0" /><Reference Include="MonoGame.Framework" /></ItemGroup>""");

        var result = await DiscoverAsync(folder);

        Assert.Equal(ProjectFamily.Xna, Assert.Single(result.Projects).Family);
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "ANA008");
        Assert.Contains("Microsoft.Xna.Framework", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("MonoGame.Framework", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal("Both.csproj", diagnostic.File);
    }

    [Fact]
    public async Task A_broken_packages_config_gives_ANA011_and_the_project_is_still_read()
    {
        var folder = Project("BadPkg", """<ItemGroup><Reference Include="MonoGame.Framework" /></ItemGroup>""", "<packages><package");

        var read = await ReadFolderAsync(folder);

        Assert.Equal(ProjectFamily.MonoGameLegacy, read.Info.Family);
        Assert.Contains(read.Diagnostics, d => d.Code == "ANA011" && d.File == "packages.config");
    }

    [Fact]
    public async Task Packages_config_entries_are_read_in_order()
    {
        var read = await ReadAsync("mg37-windowsdx-pkgconfig", "Mg37WindowsDx.csproj");

        Assert.Equal(["MonoGame.Framework.WindowsDX", "SharpDX", "Newtonsoft.Json"], read.Packages!.Select(p => p.Id));
        Assert.Equal("3.7.0.1708", read.Packages![0].Version);
        Assert.Equal("net47", read.Packages![0].TargetFramework);
    }

    [Fact]
    public async Task Shared_project_items_are_followed_with_paths_relative_to_the_project()
    {
        var read = await ReadAsync("mg37-multiplatform-sln", "Desktop/Mg37Desktop.csproj");

        Assert.Contains("../Shared/SharedGame.cs", read.Info.CompileFiles);
        Assert.Contains("Program.cs", read.Info.CompileFiles);
        Assert.Contains(read.Items, i => i.ItemType == "Compile" && i.Include == "../Shared/SharedGame.cs");
    }

    [Fact]
    public async Task A_missing_projitems_file_gives_ANA012_and_the_project_is_still_read()
    {
        var folder = Project("NoShared", """<ItemGroup><Reference Include="MonoGame.Framework" /></ItemGroup><Import Project="..\Missing\Missing.projitems" Label="Shared" />""");

        var read = await ReadFolderAsync(folder);

        Assert.Equal(ProjectFamily.MonoGameLegacy, read.Info.Family);
        Assert.Contains(read.Diagnostics, d => d.Code == "ANA012");
    }

    [Fact]
    public async Task The_discovery_reason_for_a_folder_with_no_projects_is_unchanged()
    {
        var result = await DiscoverAsync(FixturePaths.Of("empty-folder"));

        Assert.Equal("No .csproj files found.", result.NoProjectReason);
    }

    [Fact]
    public void The_pin_in_the_detector_equals_the_pin_in_packages_json()
    {
        var path = Path.Combine(FixturePaths.Root, "..", "..", "src", "XnaReborn.Migrator", "Projects", "packages.json");
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));

        Assert.Equal(MonoGameProjectDetector.CurrentVersion, document.RootElement.GetProperty("monoGame").GetString());
    }
}
