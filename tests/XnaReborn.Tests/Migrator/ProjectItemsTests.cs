using System.Text;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Content;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Projects;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator;

/// <summary>
/// The xna40-audio and xna40-prebuilt fixtures: what the analyzer reports, how the audio content converts, and that a game project's
/// own Content and None items with CopyToOutputDirectory survive the migration.
/// </summary>
[Trait("Category", "Migrator")]
public sealed class ProjectItemsTests : IDisposable
{
    private static readonly ContentOptions _plain = new(MonoGameTarget.DesktopGL, SubstituteFonts: false);

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-items-" + Guid.NewGuid().ToString("N"));

    public ProjectItemsTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private static string Utf8(string path) => File.ReadAllText(path, new UTF8Encoding(false));

    private static async Task<ContentConversionResult> ConvertAsync(string fixture, string project)
    {
        var root = FixturePaths.Of(fixture);
        var fileSystem = new ReadOnlyFileSystem();
        var read = await new ProjectReader(fileSystem).ReadAsync(Path.Combine(root, project), root, CancellationToken.None);
        return await new ContentConverter(fileSystem).ConvertAsync(read, root, string.Empty, _plain, CancellationToken.None);
    }

    private static FileChange Change(IEnumerable<FileChange> changes, string path) =>
        Assert.Single(changes, c => c.RelativePath == path);

    private async Task<MigrationResult> MigrateFolderAsync(string source, string name = "out") =>
        await MigrationTestSupport.MigrateAsync(source, MigrationTestSupport.Options(Path.Combine(_temp, name)));

    private string NewSource(string name, string itemGroup, params string[] files)
    {
        var root = Path.Combine(_temp, name);
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, "Game.csproj"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="4.0">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <AssemblyName>Game</AssemblyName>
                <XnaFrameworkVersion>v4.0</XnaFrameworkVersion>
                <XnaPlatform>Windows</XnaPlatform>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="Microsoft.Xna.Framework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=842cf8be1de50553" />
              </ItemGroup>
              <ItemGroup>
                <Compile Include="Program.cs" />
              </ItemGroup>
              <ItemGroup>
                {itemGroup}
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { static void Main() { } }\n");
        foreach (var file in files)
        {
            var full = Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "placeholder\n");
        }

        return root;
    }

    // ----- analyzer findings with the real catalog -----

    [Fact]
    public async Task Audio_fixture_findings_are_the_project_ones_plus_xna005_and_xna019()
    {
        var report = await MigrationTestSupport.AnalyzeAsync(FixturePaths.Of("xna40-audio"));

        Assert.Equal(["XNA001", "XNA002", "XNA003", "XNA004", "XNA005", "XNA019", "XNA046"], report.Findings.Select(f => f.RuleId).Distinct().Order(StringComparer.Ordinal));
        var wma = Assert.Single(report.Findings, f => f.RuleId == "XNA019");
        Assert.Equal("Audio/legacy.wma", wma.Snippet);
        Assert.DoesNotContain(report.Findings, f => f.Location is not null);
        Assert.Equal(XnaVersion.V40, Assert.Single(report.Projects).XnaVersion);
    }

    [Fact]
    public async Task Prebuilt_fixture_reports_xna018_for_both_xnb_files_and_has_no_content_project()
    {
        var report = await MigrationTestSupport.AnalyzeAsync(FixturePaths.Of("xna40-prebuilt"));

        var prebuilt = Assert.Single(report.Findings, f => f.RuleId == "XNA018");
        Assert.Equal(Classification.Manual, prebuilt.Classification);
        Assert.Equal("Content/hero.xnb, Content/sheet.xnb", prebuilt.Snippet);
        Assert.DoesNotContain(report.Findings, f => f.RuleId is "XNA005" or "XNA049" or "XNA019");
        Assert.DoesNotContain(report.Findings, f => f.Location is not null);
        Assert.Empty(Assert.Single(report.Projects).ContentProjects);
    }

    // ----- audio conversion (unit-tested only: the fixture assets are text placeholders, so mgcb cannot build them) -----

    [Fact]
    public async Task Audio_fixture_converts_every_asset_with_the_right_importer_and_processor()
    {
        var result = await ConvertAsync("xna40-audio", "Xna40Audio.csproj");

        var mgcb = Change(result.Changes, "Content/Content.mgcb").NewText!;
        Assert.Contains("/importer:WavImporter\n/processor:SoundEffectProcessor\n/build:Audio/beep.wav\n", mgcb, StringComparison.Ordinal);
        Assert.Contains("/importer:Mp3Importer\n/processor:SongProcessor\n/build:Audio/theme.mp3\n", mgcb, StringComparison.Ordinal);
        Assert.Contains("/importer:Mp3Importer\n/processor:SoundEffectProcessor\n/build:Audio/jingle.mp3\n", mgcb, StringComparison.Ordinal);
        Assert.Contains("/importer:WmaImporter\n/processor:SongProcessor\n/build:Audio/legacy.wma\n", mgcb, StringComparison.Ordinal);
        Assert.Equal(5, result.Report.CountsByStatus[AssetStatus.Converted]);
        Assert.Empty(result.ManualNotes);
    }

    [Fact]
    public async Task Audio_fixture_keeps_the_quality_parameter_on_the_wav_only()
    {
        var result = await ConvertAsync("xna40-audio", "Xna40Audio.csproj");

        var mgcb = Change(result.Changes, "Content/Content.mgcb").NewText!;
        Assert.Contains(
            "/importer:WavImporter\n/processor:SoundEffectProcessor\n/processorParam:Quality=Low\n/build:Audio/quiet.wav\n",
            mgcb,
            StringComparison.Ordinal);
        Assert.Equal(1, mgcb.Split("/processorParam:Quality=", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task Audio_fixture_copies_the_five_source_files_unchanged()
    {
        var result = await ConvertAsync("xna40-audio", "Xna40Audio.csproj");

        var copies = result.Changes.Where(c => c.Kind == ChangeKind.Copied).Select(c => c.RelativePath).Order(StringComparer.Ordinal);
        Assert.Equal(
            ["Content/Audio/beep.wav", "Content/Audio/jingle.mp3", "Content/Audio/legacy.wma", "Content/Audio/quiet.wav", "Content/Audio/theme.mp3"],
            copies);
        Assert.All(result.Changes.Where(c => c.Kind == ChangeKind.Copied), c => Assert.True(File.Exists(c.SourcePath), c.SourcePath));
    }

    [Fact]
    public async Task Audio_fixture_asset_names_and_types_are_reported()
    {
        var result = await ConvertAsync("xna40-audio", "Xna40Audio.csproj");

        Assert.Equal(
            ["Audio/beep", "Audio/jingle", "Audio/legacy", "Audio/quiet", "Audio/theme"],
            result.Report.Assets.Select(a => a.AssetName).Order(StringComparer.Ordinal));
        Assert.Equal(3, result.Report.CountsByType["SoundEffect"]);
        Assert.Equal(2, result.Report.CountsByType["Song"]);
    }

    // ----- prebuilt assets survive the migration -----

    [Fact]
    public async Task Prebuilt_fixture_copies_every_content_and_none_item_with_copy_to_output()
    {
        var source = FixturePaths.Of("xna40-prebuilt");
        var before = MigrationTestSupport.Tree(source);

        var result = await MigrateFolderAsync(source);

        Assert.True(result.Completed);
        var hero = Change(result.Changes, "Content/hero.xnb");
        Assert.Equal(ChangeKind.ManualWork, hero.Kind);
        Assert.Equal(Path.Combine(source, "Content", "hero.xnb"), hero.SourcePath);
        Assert.Contains("cannot be rebuilt", hero.Reason, StringComparison.Ordinal);
        Assert.Equal(ChangeKind.ManualWork, Change(result.Changes, "Content/sheet.xnb").Kind);
        var levels = Change(result.Changes, "Data/levels.txt");
        Assert.Equal(ChangeKind.Copied, levels.Kind);
        Assert.Equal(Path.Combine(source, "Data", "levels.txt"), levels.SourcePath);

        var written = Path.Combine(_temp, "out");
        Assert.Equal(Utf8(Path.Combine(source, "Content", "hero.xnb")), Utf8(Path.Combine(written, "Content", "hero.xnb")));
        Assert.Equal(Utf8(Path.Combine(source, "Data", "levels.txt")), Utf8(Path.Combine(written, "Data", "levels.txt")));
        Assert.True(File.Exists(Path.Combine(written, "Content", "sheet.xnb")));
        Assert.Equal(before, MigrationTestSupport.Tree(source));
    }

    [Fact]
    public async Task Prebuilt_fixture_csproj_lists_the_items_in_document_order_with_their_original_values()
    {
        var result = await MigrateFolderAsync(FixturePaths.Of("xna40-prebuilt"));

        var csproj = Change(result.Changes, "Xna40Prebuilt.csproj").NewText!;
        var expected =
            "  <ItemGroup>\n"
            + "    <None Include=\"Content\\hero.xnb\" CopyToOutputDirectory=\"PreserveNewest\" />\n"
            + "    <None Include=\"Content\\sheet.xnb\" CopyToOutputDirectory=\"Always\" />\n"
            + "    <None Include=\"Data\\levels.txt\" CopyToOutputDirectory=\"PreserveNewest\" />\n"
            + "  </ItemGroup>\n"
            + "</Project>\n";
        Assert.EndsWith(expected, csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("MonoGameContentReference", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prebuilt_fixture_adds_a_manual_note_for_the_xnb_files_only()
    {
        var result = await MigrateFolderAsync(FixturePaths.Of("xna40-prebuilt"));

        var written = Path.Combine(_temp, "out");
        var markdown = Utf8(Path.Combine(written, ProjectMigrator.ManifestMarkdownName));
        Assert.Contains("Content/hero.xnb: Prebuilt .xnb content cannot be rebuilt for MonoGame", markdown, StringComparison.Ordinal);
        Assert.Contains("Content/sheet.xnb: Prebuilt .xnb content cannot be rebuilt for MonoGame", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Data/levels.txt:", markdown, StringComparison.Ordinal);
        Assert.Equal(2, result.Manifest.Changes.Count(c => c.Kind == ChangeKind.ManualWork));
    }

    [Fact]
    public async Task Items_without_copy_to_output_or_with_never_are_not_copied()
    {
        var source = NewSource(
            "plain",
            """
            <Content Include="Data\a.txt" />
            <Content Include="Data\b.txt"><CopyToOutputDirectory>Never</CopyToOutputDirectory></Content>
            <None Include="Data\c.txt"><CopyToOutputDirectory>Always</CopyToOutputDirectory></None>
            <EmbeddedResource Include="Data\d.txt"><CopyToOutputDirectory>Always</CopyToOutputDirectory></EmbeddedResource>
            """,
            "Data/a.txt",
            "Data/b.txt",
            "Data/c.txt",
            "Data/d.txt");

        var result = await MigrateFolderAsync(source);

        Assert.Equal(["Data/c.txt"], result.Changes.Where(c => c.RelativePath.StartsWith("Data/", StringComparison.Ordinal)).Select(c => c.RelativePath));
        var csproj = Change(result.Changes, "Game.csproj").NewText!;
        Assert.Contains("<None Include=\"Data\\c.txt\" CopyToOutputDirectory=\"Always\" />", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("a.txt", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("b.txt", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("d.txt", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Item_outside_the_source_folder_is_skipped_with_a_note_and_left_out_of_the_csproj()
    {
        var parent = Path.Combine(_temp, "outer");
        Directory.CreateDirectory(parent);
        File.WriteAllText(Path.Combine(parent, "shared.txt"), "outside\n");
        var source = NewSource(
            "outer/game",
            """
            <Content Include="..\shared.txt"><CopyToOutputDirectory>Always</CopyToOutputDirectory></Content>
            <Content Include="Data\ok.txt"><CopyToOutputDirectory>Always</CopyToOutputDirectory></Content>
            """,
            "Data/ok.txt");

        var result = await MigrateFolderAsync(source);

        Assert.DoesNotContain(result.Changes, c => c.RelativePath.Contains("shared.txt", StringComparison.Ordinal));
        Assert.Contains(result.Changes, c => c.RelativePath == "Data/ok.txt");
        var csproj = Change(result.Changes, "Game.csproj").NewText!;
        Assert.DoesNotContain("shared.txt", csproj, StringComparison.Ordinal);
        Assert.Contains("Data\\ok.txt", csproj, StringComparison.Ordinal);
        var markdown = Utf8(Path.Combine(_temp, "out", ProjectMigrator.ManifestMarkdownName));
        Assert.Contains("../shared.txt: this item is copied to the output folder but lies outside the source folder", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_file_and_wildcard_items_get_a_note_and_no_csproj_entry()
    {
        var source = NewSource(
            "gaps",
            """
            <Content Include="Data\gone.txt"><CopyToOutputDirectory>Always</CopyToOutputDirectory></Content>
            <Content Include="Data\*.dat"><CopyToOutputDirectory>Always</CopyToOutputDirectory></Content>
            """,
            "Data/x.dat");

        var result = await MigrateFolderAsync(source);

        Assert.DoesNotContain(result.Changes, c => c.RelativePath.StartsWith("Data/", StringComparison.Ordinal));
        var markdown = Utf8(Path.Combine(_temp, "out", ProjectMigrator.ManifestMarkdownName));
        Assert.Contains("Data/gone.txt: this item is copied to the output folder but the file does not exist", markdown, StringComparison.Ordinal);
        Assert.Contains("Data/*.dat: wildcard items", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("<None", Change(result.Changes, "Game.csproj").NewText!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Repeated_item_is_copied_and_listed_once()
    {
        var source = NewSource(
            "dup",
            """
            <Content Include="Data\a.txt"><CopyToOutputDirectory>Always</CopyToOutputDirectory></Content>
            <None Include="Data\a.txt"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>
            """,
            "Data/a.txt");

        var result = await MigrateFolderAsync(source);

        Assert.Single(result.Changes, c => c.RelativePath == "Data/a.txt");
        var csproj = Change(result.Changes, "Game.csproj").NewText!;
        Assert.Equal(1, csproj.Split("<None Include=", StringSplitOptions.None).Length - 1);
        Assert.Contains("CopyToOutputDirectory=\"Always\"", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Projects_without_such_items_keep_a_csproj_without_none_items()
    {
        var result = await MigrateFolderAsync(FixturePaths.Of("xna40-basic"));

        Assert.DoesNotContain("<None", Change(result.Changes, "Xna40Basic.csproj").NewText!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generator_lists_the_same_items_the_migrator_copies()
    {
        var root = FixturePaths.Of("xna40-prebuilt");
        var read = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(root, "Xna40Prebuilt.csproj"), root, CancellationToken.None);

        var items = SdkCsprojGenerator.CopiedItems(read);

        Assert.Equal(
            [("Content\\hero.xnb", "PreserveNewest"), ("Content\\sheet.xnb", "Always"), ("Data\\levels.txt", "PreserveNewest")],
            items.Select(i => (i.Include, i.CopyToOutputDirectory)));
        Assert.All(items, i => Assert.Equal(CopiedItemProblem.None, i.Problem));
        Assert.Equal(["Content/hero.xnb", "Content/sheet.xnb", "Data/levels.txt"], items.Select(i => i.RootRelativePath));
    }
}
