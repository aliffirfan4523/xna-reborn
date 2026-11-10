using System.Text;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Content;
using XnaReborn.Migrator.Output;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Safety;

namespace XnaReborn.Tests.Migrator.Content;

/// <summary>Content conversion of a whole game project: names, statuses, fonts, Content.mgcb and the planned copies.</summary>
[Trait("Category", "Migrator")]
public sealed class ContentConverterTests : IDisposable
{
    private static readonly ContentOptions _plain = new(MonoGameTarget.DesktopGL, SubstituteFonts: false);
    private static readonly ContentOptions _substitute = new(MonoGameTarget.DesktopGL, SubstituteFonts: true);

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-content-" + Guid.NewGuid().ToString("N"));

    public ContentConverterTests()
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

    private static async Task<(ContentConversionResult Result, string Root)> ConvertFixtureAsync(
        string fixture,
        string game,
        ContentOptions options,
        CancellationToken cancellationToken = default)
    {
        var root = FixturePaths.Of(fixture);
        return (await ConvertAsync(root, game, options, cancellationToken), root);
    }

    private static async Task<ContentConversionResult> ConvertAsync(
        string root,
        string gameProject,
        ContentOptions options,
        CancellationToken cancellationToken = default)
    {
        var fileSystem = new ReadOnlyFileSystem();
        var read = await new ProjectReader(fileSystem).ReadAsync(Path.Combine(root, gameProject), root, cancellationToken);
        var slash = gameProject.Replace('\\', '/').LastIndexOf('/');
        var gameDir = slash < 0 ? string.Empty : gameProject.Replace('\\', '/')[..slash];
        return await new ContentConverter(fileSystem).ConvertAsync(read, root, gameDir, options, cancellationToken);
    }

    private static FileChange Change(ContentConversionResult result, string path) =>
        Assert.Single(result.Changes, c => c.RelativePath == path);

    private static ConvertedAsset Asset(ContentConversionResult result, string assetName) =>
        Assert.Single(result.Report.Assets, a => a.AssetName == assetName);

    private string NewSource(string name, params (string Path, byte[] Bytes)[] files)
    {
        var root = Path.Combine(_temp, name);
        foreach (var (path, bytes) in files)
        {
            var full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, bytes);
        }

        return root;
    }

    private static (string Path, byte[] Bytes) Text(string path, string text) => (path, new UTF8Encoding(false).GetBytes(text));

    private static (string Path, byte[] Bytes) Bytes(string path) => (path, [1, 2, 3]);

    private static string GameProject(string contentProjectInclude, string kind = "NestedContentProject") =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="4.0">
          <PropertyGroup>
            <OutputType>WinExe</OutputType>
            <AssemblyName>G</AssemblyName>
            <XnaFrameworkVersion>v4.0</XnaFrameworkVersion>
            <XnaPlatform>Windows</XnaPlatform>
          </PropertyGroup>
          <ItemGroup>
            <Reference Include="Microsoft.Xna.Framework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=842cf8be1de50553" />
          </ItemGroup>
          <ItemGroup>
            <{kind} Include="{contentProjectInclude}" />
          </ItemGroup>
        </Project>
        """;

    private static string ContentProject(string items, string properties = "", string references = "") =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="4.0">
          <PropertyGroup>
            <XnaFrameworkVersion>v4.0</XnaFrameworkVersion>
            {properties}
          </PropertyGroup>
          <ItemGroup>
            {references}
          </ItemGroup>
          <ItemGroup>
            {items}
          </ItemGroup>
        </Project>
        """;

    private static string Item(string include, string? name = null, string importer = "TextureImporter", string processor = "TextureProcessor", string extra = "") =>
        $"<Compile Include=\"{include}\">"
        + (name is null ? string.Empty : $"<Name>{name}</Name>")
        + $"<Importer>{importer}</Importer><Processor>{processor}</Processor>{extra}</Compile>";

    [Fact]
    public async Task Basic_fixture_gets_a_content_mgcb_and_both_assets_copied()
    {
        var (result, root) = await ConvertFixtureAsync("xna40-basic", "Xna40Basic.csproj", _plain);

        Assert.Equal("Content/Content.mgcb", result.MgcbRelativePath);
        Assert.Equal(["Content/Content.mgcb", "Content/Fonts/Font.spritefont", "Content/Textures/hero.png"], result.Changes.Select(c => c.RelativePath));
        var mgcb = Change(result, "Content/Content.mgcb");
        Assert.Equal(ChangeKind.Created, mgcb.Kind);
        Assert.Contains("/importer:TextureImporter\n/processor:TextureProcessor\n/processorParam:PremultiplyAlpha=True\n/build:Textures/hero.png\n", mgcb.NewText, StringComparison.Ordinal);
        Assert.Contains("/importer:FontDescriptionImporter\n/processor:FontDescriptionProcessor\n/build:Fonts/Font.spritefont\n", mgcb.NewText, StringComparison.Ordinal);
        Assert.Contains("/platform:DesktopGL", mgcb.NewText, StringComparison.Ordinal);
        Assert.Contains("/profile:HiDef", mgcb.NewText, StringComparison.Ordinal);

        var hero = Change(result, "Content/Textures/hero.png");
        Assert.Equal(ChangeKind.Copied, hero.Kind);
        Assert.Equal(Path.Combine(root, "Content", "Textures", "hero.png"), hero.SourcePath);
        Assert.Equal(ChangeKind.Copied, Change(result, "Content/Fonts/Font.spritefont").Kind);
        Assert.Empty(result.ManualNotes);
        Assert.Equal(2, result.Report.CountsByStatus[AssetStatus.Converted]);
        Assert.Equal(1, result.Report.CountsByType["Texture"]);
        Assert.Equal(1, result.Report.CountsByType["Font"]);
    }

    [Fact]
    public async Task Windows_target_and_reach_profile_reach_the_mgcb()
    {
        var root = NewSource(
            "reach",
            Text("G.csproj", GameProject("Content\\Content.contentproj")),
            Text("Content/Content.contentproj", ContentProject(Item("a.png"), "<XnaProfile>Reach</XnaProfile>")),
            Bytes("Content/a.png"));

        var result = await ConvertAsync(root, "G.csproj", new ContentOptions(MonoGameTarget.WindowsDX, false));

        var text = Change(result, "Content/Content.mgcb").NewText!;
        Assert.Contains("/platform:Windows", text, StringComparison.Ordinal);
        Assert.Contains("/profile:Reach", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Custom_content_without_substitution_sends_the_xna_only_font_and_custom_items_to_manual_work()
    {
        var (result, root) = await ConvertFixtureAsync("xna40-custom-content", "CustomContent.csproj", _plain);

        var mgcb = Change(result, "Content/Content.mgcb").NewText!;
        Assert.Contains("/build:Textures/hero.png", mgcb, StringComparison.Ordinal);
        Assert.Contains("/build:Shaders/Glow.fx", mgcb, StringComparison.Ordinal);
        Assert.DoesNotContain("ship.png", mgcb, StringComparison.Ordinal);
        Assert.DoesNotContain("Sounds.xap", mgcb, StringComparison.Ordinal);
        Assert.DoesNotContain("Hud.spritefont", mgcb, StringComparison.Ordinal);
        Assert.DoesNotContain("/reference:", mgcb, StringComparison.Ordinal);

        Assert.Equal(AssetStatus.ManualWork, Asset(result, "Textures/ship").Status);
        Assert.Equal(ChangeKind.ManualWork, Change(result, "Content/Textures/ship.png").Kind);
        Assert.Equal(AssetStatus.ManualWork, Asset(result, "Audio/Sounds").Status);
        Assert.Equal(ChangeKind.ManualWork, Change(result, "Content/Audio/Sounds.xap").Kind);

        var shader = Change(result, "Content/Shaders/Glow.fx");
        Assert.Equal(ChangeKind.Copied, shader.Kind);
        Assert.Contains("Shader review needed", shader.Reason, StringComparison.Ordinal);

        var hud = Change(result, "Content/Fonts/Hud.spritefont");
        Assert.Equal(ChangeKind.ManualWork, hud.Kind);
        Assert.Equal(Path.Combine(root, "Content", "Fonts", "Hud.spritefont"), hud.SourcePath);
        Assert.Contains("Font 'Pericles' shipped only with XNA Game Studio. Turn on font substitution (suggested: ", hud.Reason, StringComparison.Ordinal);
        Assert.Equal(hud.Reason, Asset(result, "Fonts/Hud").Note);
        Assert.Equal(AssetStatus.ManualWork, Asset(result, "Fonts/Hud").Status);

        var pipeline = Assert.Single(result.ManualNotes, n => n.Contains("MyPipeline", StringComparison.Ordinal));
        Assert.Contains("Content/Content.contentproj", pipeline, StringComparison.Ordinal);
        Assert.Contains("../Pipeline/MyPipeline.dll", pipeline, StringComparison.Ordinal);
        Assert.Equal(pipeline, Assert.Single(result.Report.Notes, n => n.Contains("MyPipeline", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Custom_content_with_substitution_keeps_the_font_converted_with_a_modified_copy()
    {
        var (result, root) = await ConvertFixtureAsync("xna40-custom-content", "CustomContent.csproj", _substitute);

        var original = File.ReadAllText(Path.Combine(root, "Content", "Fonts", "Hud.spritefont"));
        var hud = Change(result, "Content/Fonts/Hud.spritefont");
        Assert.Equal(ChangeKind.Modified, hud.Kind);
        Assert.Equal(original, hud.OriginalText);
        Assert.Contains("<FontName>Georgia</FontName>", hud.NewText, StringComparison.Ordinal);
        Assert.Equal(original.Replace("Pericles", "Georgia", StringComparison.Ordinal), hud.NewText);
        Assert.Contains("Pericles", hud.Reason, StringComparison.Ordinal);
        Assert.Contains("Georgia", hud.Reason, StringComparison.Ordinal);
        Assert.Null(hud.SourcePath);

        Assert.Equal(AssetStatus.Converted, Asset(result, "Fonts/Hud").Status);
        Assert.Contains("/build:Fonts/Hud.spritefont", Change(result, "Content/Content.mgcb").NewText, StringComparison.Ordinal);
        Assert.Contains(result.ManualNotes, n => n.Contains("Pericles", StringComparison.Ordinal) && n.Contains("Georgia", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Substituted_font_keeps_a_byte_order_mark_by_marking_the_original_text()
    {
        var font = "<?xml version=\"1.0\"?><XnaContent><Asset Type=\"Graphics:FontDescription\"><FontName>Pericles</FontName><Size>12</Size></Asset></XnaContent>";
        var root = NewSource(
            "bom",
            Text("G.csproj", GameProject("Content\\Content.contentproj")),
            Text("Content/Content.contentproj", ContentProject(Item("F.spritefont", importer: "FontDescriptionImporter", processor: "FontDescriptionProcessor"))),
            ("Content/F.spritefont", [0xEF, 0xBB, 0xBF, .. new UTF8Encoding(false).GetBytes(font)]));

        var result = await ConvertAsync(root, "G.csproj", _substitute);

        var change = Change(result, "Content/F.spritefont");
        Assert.Equal(ChangeKind.Modified, change.Kind);
        Assert.StartsWith("\uFEFF", change.OriginalText!, StringComparison.Ordinal);
        Assert.DoesNotContain('\uFEFF', change.NewText!);
    }

    [Fact]
    public async Task A_utf16_xna_only_font_cannot_be_substituted_and_goes_to_manual_work()
    {
        var font = "<?xml version=\"1.0\" encoding=\"utf-16\"?><XnaContent><Asset><FontName>Pericles</FontName></Asset></XnaContent>";
        var root = NewSource(
            "utf16",
            Text("G.csproj", GameProject("Content\\Content.contentproj")),
            Text("Content/Content.contentproj", ContentProject(Item("F.spritefont", importer: "FontDescriptionImporter", processor: "FontDescriptionProcessor"))),
            ("Content/F.spritefont", [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(font)]));

        var result = await ConvertAsync(root, "G.csproj", _substitute);

        Assert.Equal(AssetStatus.ManualWork, Asset(result, "F").Status);
        Assert.Equal(ChangeKind.ManualWork, Change(result, "Content/F.spritefont").Kind);
        Assert.DoesNotContain("F.spritefont", Change(result, "Content/Content.mgcb").NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_invalid_font_file_is_manual_work_with_the_checker_text()
    {
        var root = NewSource(
            "badfont",
            Text("G.csproj", GameProject("Content\\Content.contentproj")),
            Text("Content/Content.contentproj", ContentProject(Item("F.spritefont", importer: "FontDescriptionImporter", processor: "FontDescriptionProcessor"))),
            Text("Content/F.spritefont", "<XnaContent><Asset>"));

        var result = await ConvertAsync(root, "G.csproj", _substitute);

        var asset = Asset(result, "F");
        Assert.Equal(AssetStatus.ManualWork, asset.Status);
        Assert.Contains("not valid XML", asset.Note, StringComparison.Ordinal);
        Assert.DoesNotContain("/build:F.spritefont", Change(result, "Content/Content.mgcb").NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Multi_project_content_from_a_sibling_folder_lands_under_the_game_folder()
    {
        var (result, root) = await ConvertFixtureAsync("multi-project-sln", "Game/Game.csproj", _plain);

        Assert.Equal("Game/Content/Content.mgcb", result.MgcbRelativePath);
        Assert.Equal(["Game/Content/Content.mgcb", "Game/Content/Textures/icon.png"], result.Changes.Select(c => c.RelativePath));
        var icon = Change(result, "Game/Content/Textures/icon.png");
        Assert.Equal(ChangeKind.Copied, icon.Kind);
        Assert.Equal(Path.Combine(root, "GameContent", "Textures", "icon.png"), icon.SourcePath);
        Assert.Contains("/build:Textures/icon.png", Change(result, "Game/Content/Content.mgcb").NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_content_root_that_is_not_Content_names_the_output_folder()
    {
        var root = NewSource(
            "root",
            Text("G.csproj", GameProject("Assets\\Assets.contentproj")),
            Text("Assets/Assets.contentproj", ContentProject(Item("a.png"), "<ContentRootDirectory>Data\\Art</ContentRootDirectory>")),
            Bytes("Assets/a.png"));

        var result = await ConvertAsync(root, "G.csproj", _plain);

        Assert.Equal("Data/Art/Content.mgcb", result.MgcbRelativePath);
        Assert.Equal(ChangeKind.Copied, Change(result, "Data/Art/a.png").Kind);
    }

    [Fact]
    public async Task A_content_project_reference_is_read_like_a_nested_project()
    {
        var root = NewSource(
            "projref",
            Text("Game/G.csproj", GameProject("..\\Content\\Content.contentproj", "ProjectReference")),
            Text("Content/Content.contentproj", ContentProject(Item("a.png"))),
            Bytes("Content/a.png"));

        var result = await ConvertAsync(root, "Game/G.csproj", _plain);

        Assert.Equal("Game/Content/Content.mgcb", result.MgcbRelativePath);
        Assert.Equal(ChangeKind.Copied, Change(result, "Game/Content/a.png").Kind);
    }

    [Fact]
    public async Task A_name_override_is_written_as_source_and_destination()
    {
        var root = NewSource(
            "name",
            Text("G.csproj", GameProject("Content\\Content.contentproj")),
            Text("Content/Content.contentproj", ContentProject(Item("Textures\\hero.png", name: "big"))),
            Bytes("Content/Textures/hero.png"));

        var result = await ConvertAsync(root, "G.csproj", _plain);

        Assert.Contains("/build:Textures/hero.png;Textures/big.png\n", Change(result, "Content/Content.mgcb").NewText, StringComparison.Ordinal);
        Assert.Equal("Textures/big", Assert.Single(result.Report.Assets).AssetName);
    }

    [Fact]
    public async Task Items_with_one_name_are_conflicts_kept_as_manual_work_and_left_out_of_the_mgcb()
    {
        var root = NewSource(
            "conflict",
            Text("G.csproj", GameProject("Content\\Content.contentproj")),
            Text("Content/Content.contentproj", ContentProject(Item("hero.png") + Item("hero.jpg"))),
            Bytes("Content/hero.png"),
            Bytes("Content/hero.jpg"));

        var result = await ConvertAsync(root, "G.csproj", _plain);

        Assert.All(result.Report.Assets, a => Assert.Equal(AssetStatus.Conflict, a.Status));
        Assert.Equal(ChangeKind.ManualWork, Change(result, "Content/hero.png").Kind);
        Assert.Equal(ChangeKind.ManualWork, Change(result, "Content/hero.jpg").Kind);
        Assert.DoesNotContain("/build:", Change(result, "Content/Content.mgcb").NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_are_resolved_over_the_items_of_all_content_projects_together()
    {
        var root = NewSource(
            "twoprojects",
            Text("G.csproj", GameProject("A\\A.contentproj")),
            Text("A/A.contentproj", ContentProject(Item("x.png"))),
            Text("B/B.contentproj", ContentProject(Item("x.png"))),
            Bytes("A/x.png"),
            Bytes("B/x.png"));
        var csproj = Path.Combine(root, "G.csproj");
        File.WriteAllText(csproj, File.ReadAllText(csproj).Replace(
            "<NestedContentProject Include=\"A\\A.contentproj\" />",
            "<NestedContentProject Include=\"A\\A.contentproj\" /><NestedContentProject Include=\"B\\B.contentproj\" />",
            StringComparison.Ordinal));

        var result = await ConvertAsync(root, "G.csproj", _plain);

        Assert.Equal(2, result.Report.Assets.Count);
        Assert.All(result.Report.Assets, a => Assert.Equal(AssetStatus.Conflict, a.Status));
        var paths = result.Changes.Select(c => c.RelativePath).ToList();
        Assert.Contains("Content/x.png", paths);
        Assert.Contains("Content/x-2.png", paths);
        Assert.Contains(result.ManualNotes, n => n.Contains("Content/x.png", StringComparison.Ordinal) && n.Contains("Content/x-2.png", StringComparison.Ordinal));
        Assert.Equal(Path.Combine(root, "A", "x.png"), Change(result, "Content/x.png").SourcePath);
        Assert.Equal(Path.Combine(root, "B", "x.png"), Change(result, "Content/x-2.png").SourcePath);
    }

    [Fact]
    public async Task Sources_outside_the_content_folder_collide_and_get_numbered_names_that_the_mgcb_follows()
    {
        var root = NewSource(
            "external",
            Text("G.csproj", GameProject("Content\\Content.contentproj")),
            Text("Content/Content.contentproj", ContentProject(Item("..\\One\\x.png", name: "one") + Item("..\\Two\\x.png", name: "two"))),
            Bytes("One/x.png"),
            Bytes("Two/x.png"));

        var result = await ConvertAsync(root, "G.csproj", _plain);

        Assert.Equal(
            ["Content/Content.mgcb", "Content/_external/x-2.png", "Content/_external/x.png"],
            result.Changes.Select(c => c.RelativePath));
        Assert.Equal(Path.Combine(root, "One", "x.png"), Change(result, "Content/_external/x.png").SourcePath);
        Assert.Equal(Path.Combine(root, "Two", "x.png"), Change(result, "Content/_external/x-2.png").SourcePath);
        var mgcb = Change(result, "Content/Content.mgcb").NewText!;
        Assert.Contains("/build:_external/x.png;_external/one.png\n", mgcb, StringComparison.Ordinal);
        Assert.Contains("/build:_external/x-2.png;_external/two.png\n", mgcb, StringComparison.Ordinal);
        Assert.Contains(result.ManualNotes, n => n.Contains("x-2.png", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_dot_dot_inside_the_content_folder_is_collapsed_in_the_output_path()
    {
        var root = NewSource(
            "dotdot",
            Text("G.csproj", GameProject("Content\\Content.contentproj")),
            Text("Content/Content.contentproj", ContentProject(Item("Textures\\..\\Art\\a.png"))),
            Bytes("Content/Art/a.png"));

        var result = await ConvertAsync(root, "G.csproj", _plain);

        Assert.Contains(result.Changes, c => c.RelativePath == "Content/Art/a.png");
        Assert.All(result.Changes, c => Assert.DoesNotContain("..", c.RelativePath, StringComparison.Ordinal));
        Assert.Equal("Art/a", Assert.Single(result.Report.Assets).AssetName);
        Assert.Empty(result.ManualNotes);
    }

    [Fact]
    public async Task A_missing_source_is_skipped_and_never_takes_a_path_from_a_real_file()
    {
        var root = NewSource(
            "missing",
            Text("G.csproj", GameProject("Content\\Content.contentproj")),
            Text("Content/Content.contentproj", ContentProject(Item("gone.png") + Item("here.png"))),
            Bytes("Content/here.png"));

        var result = await ConvertAsync(root, "G.csproj", _plain);

        var gone = Change(result, "Content/gone.png");
        Assert.Equal(ChangeKind.Skipped, gone.Kind);
        Assert.Contains("gone.png", gone.Reason, StringComparison.Ordinal);
        Assert.Equal(AssetStatus.Missing, Asset(result, "gone").Status);
        Assert.DoesNotContain("gone.png", Change(result, "Content/Content.mgcb").NewText, StringComparison.Ordinal);
        Assert.Equal(1, result.Report.CountsByStatus[AssetStatus.Missing]);
    }

    [Fact]
    public async Task Dropped_processor_parameters_become_notes_naming_the_asset()
    {
        var root = NewSource(
            "params",
            Text("G.csproj", GameProject("Content\\Content.contentproj")),
            Text(
                "Content/Content.contentproj",
                ContentProject(Item(
                    "F.spritefont",
                    importer: "FontDescriptionImporter",
                    processor: "FontDescriptionProcessor",
                    extra: "<ProcessorParameters_DefaultCharacter>?</ProcessorParameters_DefaultCharacter><ProcessorParameters_PremultiplyAlpha>True</ProcessorParameters_PremultiplyAlpha>"))),
            Text("Content/F.spritefont", "<XnaContent><Asset><FontName>Arial</FontName></Asset></XnaContent>"));

        var result = await ConvertAsync(root, "G.csproj", _plain);

        var note = Assert.Single(result.ManualNotes);
        Assert.StartsWith("Content/F.spritefont: processor parameter DefaultCharacter:", note, StringComparison.Ordinal);
        var mgcb = Change(result, "Content/Content.mgcb").NewText!;
        Assert.Contains("/processorParam:PremultiplyAlpha=True", mgcb, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultCharacter", mgcb, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entry_mgcb_cannot_express_goes_to_manual_work_with_the_problem()
    {
        var root = NewSource(
            "semicolon",
            Text("G.csproj", GameProject("Content\\Content.contentproj")),
            Text("Content/Content.contentproj", ContentProject(Item("a;b.png") + Item("ok.png"))),
            Bytes("Content/a;b.png"),
            Bytes("Content/ok.png"));

        var result = await ConvertAsync(root, "G.csproj", _plain);

        var asset = Asset(result, "a;b");
        Assert.Equal(AssetStatus.ManualWork, asset.Status);
        Assert.Contains("';'", asset.Note, StringComparison.Ordinal);
        Assert.Equal(ChangeKind.ManualWork, Change(result, "Content/a;b.png").Kind);
        var mgcb = Change(result, "Content/Content.mgcb").NewText!;
        Assert.DoesNotContain("a;b", mgcb, StringComparison.Ordinal);
        Assert.Contains("/build:ok.png", mgcb, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_prebuilt_xnb_is_copied_by_mgcb_and_reported_as_prebuilt()
    {
        var root = NewSource(
            "prebuilt",
            Text("G.csproj", GameProject("Content\\Content.contentproj")),
            Text("Content/Content.contentproj", "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"><ItemGroup><Compile Include=\"Old\\thing.xnb\" /></ItemGroup></Project>"),
            Bytes("Content/Old/thing.xnb"));

        var result = await ConvertAsync(root, "G.csproj", _plain);

        Assert.Equal(AssetStatus.Prebuilt, Assert.Single(result.Report.Assets).Status);
        Assert.Contains("/copy:Old/thing.xnb\n", Change(result, "Content/Content.mgcb").NewText, StringComparison.Ordinal);
        Assert.Equal(ChangeKind.Copied, Change(result, "Content/Old/thing.xnb").Kind);
        Assert.Equal(1, result.Report.CountsByType["Prebuilt"]);
    }

    [Fact]
    public async Task A_content_project_that_cannot_be_read_keeps_all_its_files_as_manual_work()
    {
        var root = NewSource(
            "unreadable",
            Text("G.csproj", GameProject("Content\\Content.contentproj")),
            Text("Content/Content.contentproj", "<Project><this is not xml"),
            Bytes("Content/Textures/a.png"));

        var result = await ConvertAsync(root, "G.csproj", _plain);

        Assert.Equal("Content/Content.mgcb", result.MgcbRelativePath);
        Assert.Equal(ChangeKind.ManualWork, Change(result, "Content/Textures/a.png").Kind);
        Assert.Equal(ChangeKind.ManualWork, Change(result, "Content/Content.contentproj").Kind);
        Assert.Contains(result.ManualNotes, n => n.StartsWith("Content/Content.contentproj: Content project could not be read", StringComparison.Ordinal));
        Assert.DoesNotContain("/build:", Change(result, "Content/Content.mgcb").NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_content_root_is_noted_and_the_first_wins()
    {
        var root = NewSource(
            "roots",
            Text("G.csproj", GameProject("A\\A.contentproj")),
            Text("A/A.contentproj", ContentProject(Item("a.png"), "<ContentRootDirectory>One</ContentRootDirectory>")),
            Text("B/B.contentproj", ContentProject(Item("b.png"), "<ContentRootDirectory>Two</ContentRootDirectory>")),
            Bytes("A/a.png"),
            Bytes("B/b.png"));
        var csproj = Path.Combine(root, "G.csproj");
        File.WriteAllText(csproj, File.ReadAllText(csproj).Replace(
            "<NestedContentProject Include=\"A\\A.contentproj\" />",
            "<NestedContentProject Include=\"A\\A.contentproj\" /><NestedContentProject Include=\"B\\B.contentproj\" />",
            StringComparison.Ordinal));

        var result = await ConvertAsync(root, "G.csproj", _plain);

        Assert.Equal("One/Content.mgcb", result.MgcbRelativePath);
        Assert.Contains("One/b.png", result.Changes.Select(c => c.RelativePath));
        Assert.Contains(result.ManualNotes, n => n.Contains("'Two'", StringComparison.Ordinal) && n.Contains("'One'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_project_without_content_gives_an_empty_result()
    {
        var root = FixturePaths.Of("xna31-rewritable");
        var result = await ConvertAsync(root, "Xna31Rewritable.csproj", _plain);

        Assert.Empty(result.Changes);
        Assert.Empty(result.ManualNotes);
        Assert.Null(result.MgcbRelativePath);
        Assert.Empty(result.Report.Assets);
    }

    [Fact]
    public async Task Output_is_deterministic_and_sorted()
    {
        var (first, _) = await ConvertFixtureAsync("xna40-custom-content", "CustomContent.csproj", _substitute);
        var (second, _) = await ConvertFixtureAsync("xna40-custom-content", "CustomContent.csproj", _substitute);

        Assert.Equal(first.Changes, second.Changes);
        Assert.Equal(first.ManualNotes, second.ManualNotes);
        Assert.Equal(first.Report.Assets.Select(a => (a.AssetName, a.Status)), second.Report.Assets.Select(a => (a.AssetName, a.Status)));
        Assert.Equal(first.Changes.Select(c => c.RelativePath).Order(StringComparer.Ordinal), first.Changes.Select(c => c.RelativePath));
        Assert.Equal(first.ManualNotes.Order(StringComparer.Ordinal), first.ManualNotes);
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task A_cancelled_token_stops_the_conversion_and_the_source_is_not_changed()
    {
        var root = FixturePaths.Of("xna40-basic");
        var before = FolderState.Capture(root);
        var read = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(root, "Xna40Basic.csproj"), root, CancellationToken.None);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ContentConverter(new ReadOnlyFileSystem()).ConvertAsync(read, root, string.Empty, _plain, cts.Token));

        Assert.Equal(before, FolderState.Capture(root));
    }

    [Fact]
    public async Task Null_and_blank_arguments_are_refused()
    {
        var root = FixturePaths.Of("xna40-basic");
        var read = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(root, "Xna40Basic.csproj"), root, CancellationToken.None);
        var converter = new ContentConverter(new ReadOnlyFileSystem());

        Assert.Throws<ArgumentNullException>(() => new ContentConverter(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => converter.ConvertAsync(null!, root, string.Empty, _plain, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => converter.ConvertAsync(read, " ", string.Empty, _plain, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => converter.ConvertAsync(read, root, null!, _plain, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => converter.ConvertAsync(read, root, string.Empty, null!, CancellationToken.None));
    }
}
