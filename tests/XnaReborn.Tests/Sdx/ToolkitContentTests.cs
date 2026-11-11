using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Content;
using XnaReborn.Migrator.Output;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Migrator;

namespace XnaReborn.Tests.Sdx;

/// <summary>Task 9.1 and 9.3: the Toolkit content reader and the content conversion.</summary>
[Trait("Category", "Sdx")]
public sealed class ToolkitContentTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-sdxc-" + Guid.NewGuid().ToString("N"));

    public ToolkitContentTests()
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

    private static readonly ContentOptions _windows = new(MonoGameTarget.WindowsDX, SubstituteFonts: false);

    private static async Task<ProjectReadResult> ReadAsync(string folder, string project) =>
        await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(folder, project), folder, CancellationToken.None);

    // Writes a project whose items are given as `Action|Include|Link` and whose files hold the given text.
    private async Task<(ProjectReadResult Read, string Folder)> ProjectAsync(string[] items, params (string Path, string Text)[] files)
    {
        var folder = Path.Combine(_temp, "p" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var xml = string.Concat(items.Select(i =>
        {
            var parts = i.Split('|');
            var link = parts.Length > 2 ? $"<Link>{parts[2]}</Link>" : string.Empty;
            return $"<ItemGroup><{parts[0]} Include=\"{parts[1]}\">{link}</{parts[0]}></ItemGroup>";
        }));
        File.WriteAllText(Path.Combine(folder, "Game.csproj"), $"<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"><ItemGroup><Reference Include=\"SharpDX.Toolkit\" /></ItemGroup>{xml}</Project>");
        foreach (var (path, text) in files)
        {
            var full = Path.Combine(folder, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }

        return (await ReadAsync(folder, "Game.csproj"), folder);
    }

    private static Task<ContentConversionResult> ConvertAsync(ProjectReadResult read, string folder, ContentOptions? options = null, string gameDir = "") =>
        new ToolkitContentConverter(new ReadOnlyFileSystem()).ConvertAsync(read, folder, gameDir, options ?? _windows, CancellationToken.None);

    private const string _font = "<TkFont><FontName>Arial</FontName><Size>12</Size></TkFont>";

    // ---------- 9.1 reader ----------
    [Fact]
    public async Task The_reader_lists_the_five_build_actions_with_link_based_asset_names()
    {
        var read = await ReadAsync(Fixture("sdx-effects"), "SdxEffects.csproj");

        var items = ToolkitContentReader.Read(read);

        Assert.Equal(["ToolkitFont|Content/Arial16.xml|Arial16", "ToolkitFxc|Content/Tint.fx|Tint"], items.Select(i => $"{i.BuildAction}|{i.Item.SourcePath}|{i.AssetName}"));
        Assert.All(items, i => Assert.Null(i.Item.Importer));
        Assert.All(items, i => Assert.Null(i.Item.Processor));
        Assert.Equal("Arial16.xml", items[0].Link);
    }

    [Fact]
    public async Task Without_a_link_the_asset_name_is_the_project_relative_path_without_extension()
    {
        var (read, _) = await ProjectAsync(
            ["ToolkitTexture|Content\\Sprites\\hero.png", "ToolkitTexture|.\\Other\\a.b.dds|Textures\\a.b.dds", "ToolkitModel|Models\\ship"]);

        var items = ToolkitContentReader.Read(read);

        Assert.Equal(["Content/Sprites/hero", "Textures/a.b", "Models/ship"], items.Select(i => i.AssetName));
        Assert.Equal("Other/a.b.dds", items[1].Item.SourcePath);
    }

    [Fact]
    public async Task The_reader_ignores_other_items_and_repeats()
    {
        var (read, _) = await ProjectAsync(["Compile|a.cs", "None|b.png", "ToolkitAudio|s.wav", "ToolkitAudio|s.wav", "Content|c.png"]);

        var items = ToolkitContentReader.Read(read);

        Assert.Equal("s", Assert.Single(items).AssetName);
    }

    [Fact]
    public async Task An_xna_project_has_no_toolkit_content()
    {
        var read = await ReadAsync(Fixture("xna40-basic"), "Xna40Basic.csproj");

        Assert.Empty(ToolkitContentReader.Read(read));
        Assert.False(ToolkitContentConverter.HasContent(read));
    }

    // ---------- 9.3 conversion ----------
    [Fact]
    public async Task Sdx_basic_converts_the_font_and_the_texture()
    {
        var read = await ReadAsync(Fixture("sdx-basic"), "SdxBasic.csproj");

        var result = await ConvertAsync(read, Fixture("sdx-basic"));

        Assert.Equal("Content/Content.mgcb", result.MgcbRelativePath);
        Assert.Equal(["Content/Arial16.spritefont|Created", "Content/Balls.dds|Copied", "Content/Content.mgcb|Created"], result.Changes.Select(c => $"{c.RelativePath}|{c.Kind}"));
        Assert.Equal(2, result.Report.CountsByStatus[AssetStatus.Converted]);
        Assert.Equal(1, result.Report.CountsByType["Font"]);
        Assert.Equal(1, result.Report.CountsByType["Texture"]);
        var mgcb = Change(result, "Content/Content.mgcb").NewText!;
        Assert.Contains("#begin Arial16.spritefont\n/importer:FontDescriptionImporter\n/processor:FontDescriptionProcessor\n/build:Arial16.spritefont", mgcb, StringComparison.Ordinal);
        Assert.Contains("#begin Balls.dds\n/importer:TextureImporter\n/processor:TextureProcessor", mgcb, StringComparison.Ordinal);
        Assert.Contains("/platform:Windows", mgcb, StringComparison.Ordinal);
        Assert.Contains("<FontName>Arial</FontName>", Change(result, "Content/Arial16.spritefont").NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Asset_names_are_kept_without_extension_and_the_report_lists_them_with_the_extension()
    {
        var (read, folder) = await ProjectAsync(
            ["ToolkitTexture|Art\\hero.png|Sprites\\hero.png", "ToolkitAudio|Snd\\boom.wav"],
            ("Art/hero.png", "x"), ("Snd/boom.wav", "x"));

        var result = await ConvertAsync(read, folder, gameDir: "Game");

        Assert.Equal("Game/Content/Content.mgcb", result.MgcbRelativePath);
        Assert.Contains("Game/Content/Sprites/hero.png", result.Changes.Select(c => c.RelativePath));
        Assert.Contains("Game/Content/Snd/boom.wav", result.Changes.Select(c => c.RelativePath));
        Assert.Contains(result.ManualNotes, n => n.StartsWith("Content.Load names: 'Snd/boom' (from Snd/boom.wav), 'Sprites/hero' (from Art/hero.png).", StringComparison.Ordinal) && n.Contains("has to drop it", StringComparison.Ordinal));
        Assert.Contains("/importer:WavImporter\n/processor:SoundEffectProcessor", Change(result, "Game/Content/Content.mgcb").NewText, StringComparison.Ordinal);
        Assert.Contains(result.ManualNotes, n => n.StartsWith("Textures:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ToolkitTexture", "a.png", "TextureImporter", "TextureProcessor")]
    [InlineData("ToolkitTexture", "a.JPG", "TextureImporter", "TextureProcessor")]
    [InlineData("ToolkitTexture", "a.dds", "TextureImporter", "TextureProcessor")]
    [InlineData("ToolkitTexture", "a.tga", "TextureImporter", "TextureProcessor")]
    [InlineData("ToolkitModel", "a.fbx", "FbxImporter", "ModelProcessor")]
    [InlineData("ToolkitModel", "a.x", "XImporter", "ModelProcessor")]
    [InlineData("ToolkitAudio", "a.wav", "WavImporter", "SoundEffectProcessor")]
    public async Task Each_converted_kind_gets_its_stock_importer_and_processor(string action, string file, string importer, string processor)
    {
        var (read, folder) = await ProjectAsync([$"{action}|{file}"], (file, "x"));

        var result = await ConvertAsync(read, folder);

        var asset = Assert.Single(result.Report.Assets);
        Assert.Equal(AssetStatus.Converted, asset.Status);
        Assert.Equal((importer, processor), (asset.MonoGameImporter, asset.MonoGameProcessor));
        Assert.Contains($"/importer:{importer}\n/processor:{processor}", Change(result, "Content/Content.mgcb").NewText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ToolkitTexture", "a.tiff")]
    [InlineData("ToolkitModel", "a.obj")]
    [InlineData("ToolkitModel", "a.dae")]
    [InlineData("ToolkitAudio", "a.ogg")]
    [InlineData("ToolkitAudio", "a.mp3")]
    public async Task Other_formats_are_manual_work_and_are_copied_unchanged(string action, string file)
    {
        var (read, folder) = await ProjectAsync([$"{action}|{file}"], (file, "x"));

        var result = await ConvertAsync(read, folder);

        var asset = Assert.Single(result.Report.Assets);
        Assert.Equal(AssetStatus.ManualWork, asset.Status);
        Assert.Equal(ChangeKind.ManualWork, Change(result, "Content/" + file).Kind);
        Assert.DoesNotContain("#begin", Change(result, "Content/Content.mgcb").NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_effect_is_copied_and_flagged()
    {
        var read = await ReadAsync(Fixture("sdx-effects"), "SdxEffects.csproj");

        var result = await ConvertAsync(read, Fixture("sdx-effects"));

        var effect = Change(result, "Content/Tint.fx");
        Assert.Equal(ChangeKind.ManualWork, effect.Kind);
        Assert.Contains("SDX013", effect.Reason, StringComparison.Ordinal);
        Assert.Equal(1, result.Report.CountsByType["Effect"]);
        Assert.DoesNotContain("Tint.fx", Change(result, "Content/Content.mgcb").NewText, StringComparison.Ordinal);
        Assert.Contains(result.ManualNotes, n => n.Contains("SDX013", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_compiled_tkb_file_with_no_source_is_copied_and_flagged()
    {
        var read = await ReadAsync(Fixture("sdx-prebuilt"), "SdxPrebuilt.csproj");

        var result = await ConvertAsync(read, Fixture("sdx-prebuilt"));

        var asset = Assert.Single(result.Report.Assets);
        Assert.Equal(AssetStatus.Prebuilt, asset.Status);
        Assert.Equal("Balls", asset.AssetName);
        Assert.Equal(ChangeKind.Copied, Change(result, "Content/Balls.tkb").Kind);
        Assert.Contains(result.ManualNotes, n => n.Contains("SDX015", StringComparison.Ordinal));
        Assert.Equal(1, result.Report.CountsByType["Prebuilt"]);
    }

    [Fact]
    public async Task A_tkb_next_to_its_source_is_not_listed_twice()
    {
        var (read, folder) = await ProjectAsync(["ToolkitTexture|a.png", "Content|a.tkb"], ("a.png", "x"), ("a.tkb", "x"));

        var result = await ConvertAsync(read, folder);

        Assert.Equal(AssetStatus.Converted, Assert.Single(result.Report.Assets).Status);
    }

    [Fact]
    public async Task A_path_that_starts_with_the_content_root_folder_gives_a_name_without_it()
    {
        var (read, folder) = await ProjectAsync(["ToolkitTexture|Content\\Sprites\\hero.png"], ("Content/Sprites/hero.png", "x"));

        var result = await ConvertAsync(read, folder);

        Assert.Equal("Sprites/hero", Assert.Single(result.Report.Assets).AssetName);
        Assert.Contains("Content/Sprites/hero.png", result.Changes.Select(c => c.RelativePath));
        Assert.Contains("#begin Sprites/hero.png", Change(result, "Content/Content.mgcb").NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_source_is_skipped_and_reported()
    {
        var (read, folder) = await ProjectAsync(["ToolkitTexture|gone.png"]);

        var result = await ConvertAsync(read, folder);

        Assert.Equal(AssetStatus.Missing, Assert.Single(result.Report.Assets).Status);
        Assert.Equal(ChangeKind.Skipped, Change(result, "Content/gone.png").Kind);
    }

    [Fact]
    public async Task Two_items_with_one_asset_name_are_both_kept_as_a_conflict()
    {
        var (read, folder) = await ProjectAsync(["ToolkitTexture|a/hero.png|hero.png", "ToolkitTexture|b/hero.png|hero.png"], ("a/hero.png", "1"), ("b/hero.png", "2"));

        var result = await ConvertAsync(read, folder);

        Assert.All(result.Report.Assets, a => Assert.Equal(AssetStatus.Conflict, a.Status));
        Assert.Equal(2, result.Report.Assets.Count);
        Assert.Contains("Content/_conflict/a/hero.png", result.Changes.Select(c => c.RelativePath));
        Assert.Contains("Content/_conflict/b/hero.png", result.Changes.Select(c => c.RelativePath));
    }

    [Fact]
    public async Task A_name_that_climbs_out_of_the_content_folder_is_moved_under_external()
    {
        var (read, folder) = await ProjectAsync(["ToolkitTexture|x.png|..\\..\\up\\x.png"], ("x.png", "x"));

        var result = await ConvertAsync(read, folder);

        Assert.Equal("_external/x", Assert.Single(result.Report.Assets).AssetName);
        Assert.Contains("Content/_external/x.png", result.Changes.Select(c => c.RelativePath));
    }

    [Fact]
    public async Task A_font_with_a_wrong_root_element_is_manual_work_with_the_clear_message()
    {
        var (read, folder) = await ProjectAsync(["ToolkitFont|f.xml"], ("f.xml", "<Other/>"));

        var result = await ConvertAsync(read, folder);

        var asset = Assert.Single(result.Report.Assets);
        Assert.Equal(AssetStatus.ManualWork, asset.Status);
        Assert.Contains("not 'TkFont'", asset.Note, StringComparison.Ordinal);
        Assert.Equal(ChangeKind.ManualWork, Change(result, "Content/f.xml").Kind);
    }

    [Fact]
    public async Task Font_warnings_reach_the_content_notes()
    {
        var (read, folder) = await ProjectAsync(["ToolkitFont|f.xml"], ("f.xml", "<TkFont><FontName>Arial</FontName><LineSpacing>3</LineSpacing></TkFont>"));

        var result = await ConvertAsync(read, folder);

        Assert.Contains(result.ManualNotes, n => n.StartsWith("Content/f.spritefont: LineSpacing is '3'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_font_only_xna_game_studio_installed_follows_the_substitution_option()
    {
        var (read, folder) = await ProjectAsync(["ToolkitFont|f.xml"], ("f.xml", "<TkFont><FontName>Kootenay</FontName></TkFont>"));

        var off = await ConvertAsync(read, folder, new ContentOptions(MonoGameTarget.WindowsDX, SubstituteFonts: false));
        var on = await ConvertAsync(read, folder, new ContentOptions(MonoGameTarget.WindowsDX, SubstituteFonts: true));

        Assert.Equal(AssetStatus.ManualWork, Assert.Single(off.Report.Assets).Status);
        Assert.Contains("Turn on font substitution", Assert.Single(off.Report.Assets).Note, StringComparison.Ordinal);
        Assert.Equal(AssetStatus.Converted, Assert.Single(on.Report.Assets).Status);
        Assert.DoesNotContain("Kootenay", Change(on, "Content/f.spritefont").NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_project_with_no_toolkit_content_gets_no_mgcb()
    {
        var read = await ReadAsync(Fixture("sdx-lowlevel"), "SdxLowLevel.csproj");

        var result = await ConvertAsync(read, Fixture("sdx-lowlevel"));

        Assert.Null(result.MgcbRelativePath);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public async Task The_conversion_does_not_touch_the_source_and_is_repeatable()
    {
        var folder = Fixture("sdx-basic");
        var before = MigrationTestSupport.Tree(folder);
        var read = await ReadAsync(folder, "SdxBasic.csproj");

        var first = await ConvertAsync(read, folder);
        var second = await ConvertAsync(read, folder);

        Assert.Equal(before, MigrationTestSupport.Tree(folder));
        Assert.Equal(first.Changes.Select(c => (c.RelativePath, c.NewText)), second.Changes.Select(c => (c.RelativePath, c.NewText)));
    }

    [Fact]
    public async Task A_cancelled_token_stops_the_conversion()
    {
        var read = await ReadAsync(Fixture("sdx-basic"), "SdxBasic.csproj");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ToolkitContentConverter(new ReadOnlyFileSystem()).ConvertAsync(read, Fixture("sdx-basic"), string.Empty, _windows, cts.Token));
    }

    private static string Fixture(string name) => FixturePaths.Of(name);

    private static FileChange Change(ContentConversionResult result, string path) =>
        Assert.Single(result.Changes, c => c.RelativePath == path);
}
