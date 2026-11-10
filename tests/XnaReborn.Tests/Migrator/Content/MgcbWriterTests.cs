using System.Diagnostics;
using System.Text;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Content;
using Xunit.Abstractions;

namespace XnaReborn.Tests.Migrator.Content;

/// <summary>The text of generated <c>Content.mgcb</c> files.</summary>
[Trait("Category", "Migrator")]
public class MgcbWriterTests
{
    private const string _globalHeading = "#----------------------------- Global Properties ----------------------------#";
    private const string _referencesHeading = "#-------------------------------- References --------------------------------#";
    private const string _contentHeading = "#---------------------------------- Content ---------------------------------#";

    private static readonly KeyValuePair<string, string>[] _noParams = [];

    private static KeyValuePair<string, string> Pair(string key, string value) => new(key, value);

    private static MgcbEntry Texture(string source, string? destination = null, params KeyValuePair<string, string>[] parameters) =>
        new(source, destination, "TextureImporter", "TextureProcessor", parameters);

    private static MgcbDocument Document(
        IReadOnlyList<MgcbEntry>? entries = null,
        IReadOnlyList<string>? copies = null,
        IReadOnlyList<string>? references = null,
        MonoGameTarget target = MonoGameTarget.DesktopGL,
        string profile = "HiDef",
        bool compress = false) =>
        new(target, profile, compress, references ?? [], entries ?? [], copies ?? []);

    private static ConvertedAsset Asset(string sourcePath, string assetName, string importer = "TextureImporter", string processor = "TextureProcessor") =>
        new(
            new ContentItem(sourcePath, null, importer, processor, new Dictionary<string, string>()),
            assetName,
            importer,
            processor,
            AssetStatus.Converted,
            null);

    private static MappedParams NoParams() => new(_noParams, []);

    [Fact]
    public void Empty_document_has_the_exact_layout_of_the_design()
    {
        var expected =
            _globalHeading + "\n" +
            "\n" +
            "/outputDir:bin/$(Platform)\n" +
            "/intermediateDir:obj/$(Platform)\n" +
            "/platform:DesktopGL\n" +
            "/config:\n" +
            "/profile:HiDef\n" +
            "/compress:False\n" +
            "\n" +
            _referencesHeading + "\n" +
            "\n" +
            "\n" +
            _contentHeading + "\n";

        Assert.Equal(expected, MgcbWriter.Write(Document()));
    }

    [Fact]
    public void Section_headings_are_equally_wide_like_the_ones_in_the_MonoGame_templates()
    {
        Assert.All([_globalHeading, _referencesHeading, _contentHeading], heading => Assert.Equal(78, heading.Length));
    }

    [Fact]
    public void Entry_block_has_importer_processor_params_and_build_in_that_order()
    {
        var text = MgcbWriter.Write(Document([
            Texture("Textures/hero.png", null, new("PremultiplyAlpha", "True"), new("ColorKeyColor", "255,0,255,255")),
        ]));

        var expectedTail =
            _contentHeading + "\n" +
            "\n" +
            "#begin Textures/hero.png\n" +
            "/importer:TextureImporter\n" +
            "/processor:TextureProcessor\n" +
            "/processorParam:PremultiplyAlpha=True\n" +
            "/processorParam:ColorKeyColor=255,0,255,255\n" +
            "/build:Textures/hero.png\n";
        Assert.EndsWith(expectedTail, text);
    }

    [Fact]
    public void Params_keep_the_order_given_and_are_not_sorted()
    {
        var text = MgcbWriter.Write(Document([Texture("a.png", null, new("Zeta", "1"), new("Alpha", "2"), new("Mid", "3"))]));

        var lines = text.Split('\n').Where(l => l.StartsWith("/processorParam:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(["/processorParam:Zeta=1", "/processorParam:Alpha=2", "/processorParam:Mid=3"], lines);
    }

    [Fact]
    public void Entry_with_a_destination_uses_the_source_semicolon_destination_form()
    {
        var text = MgcbWriter.Write(Document([Texture("Textures/hero.png", "Textures/big.png")]));

        Assert.Contains("\n/build:Textures/hero.png;Textures/big.png\n", text);
        Assert.Contains("\n#begin Textures/hero.png\n", text);
    }

    [Fact]
    public void Destination_equal_to_source_is_written_in_the_short_form()
    {
        var text = MgcbWriter.Write(Document([Texture("Textures/hero.png", "Textures/hero.png")]));

        Assert.Contains("\n/build:Textures/hero.png\n", text);
        Assert.DoesNotContain(";", text);
    }

    [Theory]
    [InlineData(MonoGameTarget.DesktopGL, "DesktopGL")]
    [InlineData(MonoGameTarget.WindowsDX, "Windows")]
    public void Target_maps_to_the_platform_value(MonoGameTarget target, string platform)
    {
        var text = MgcbWriter.Write(Document(target: target));

        Assert.Contains($"\n/platform:{platform}\n", text);
    }

    [Theory]
    [InlineData(null, "HiDef")]
    [InlineData("", "HiDef")]
    [InlineData("Reach", "Reach")]
    [InlineData("reach", "Reach")]
    [InlineData("REACH", "Reach")]
    [InlineData(" Reach ", "Reach")]
    [InlineData("HiDef", "HiDef")]
    [InlineData("hidef", "HiDef")]
    [InlineData("Extreme", "HiDef")]
    [InlineData("Mobile", "HiDef")]
    public void MapProfile_gives_Reach_or_HiDef(string? input, string expected)
    {
        Assert.Equal(expected, MgcbWriter.MapProfile(input));
    }

    [Theory]
    [InlineData("Reach", "Reach")]
    [InlineData("reach", "Reach")]
    [InlineData("HiDef", "HiDef")]
    [InlineData("HIDEF", "HiDef")]
    public void Profile_is_written_in_its_canonical_spelling(string profile, string expected)
    {
        Assert.Contains($"\n/profile:{expected}\n", MgcbWriter.Write(Document(profile: profile)));
    }

    [Theory]
    [InlineData(true, "True")]
    [InlineData(false, "False")]
    public void Compress_is_written_as_True_or_False(bool compress, string expected)
    {
        Assert.Contains($"\n/compress:{expected}\n", MgcbWriter.Write(Document(compress: compress)));
    }

    [Fact]
    public void Config_is_always_empty()
    {
        Assert.Contains("\n/config:\n", MgcbWriter.Write(Document()));
    }

    [Fact]
    public void References_are_written_in_the_given_order_before_the_content_section()
    {
        var text = MgcbWriter.Write(Document(references: ["Pipeline/Zeta.dll", "Pipeline/Alpha.dll"]));

        var expected =
            _referencesHeading + "\n" +
            "\n" +
            "/reference:Pipeline/Zeta.dll\n" +
            "/reference:Pipeline/Alpha.dll\n" +
            "\n" +
            _contentHeading + "\n";
        Assert.Contains(expected, text);
    }

    [Fact]
    public void Copy_paths_get_their_own_begin_block()
    {
        var text = MgcbWriter.Write(Document(copies: ["Data/level.dat"]));

        Assert.EndsWith(_contentHeading + "\n\n#begin Data/level.dat\n/copy:Data/level.dat\n", text);
    }

    [Fact]
    public void Entries_are_sorted_by_output_name_and_copies_come_after_them()
    {
        // The entry built from "a.png" is written as "m.png", so it sorts by that name. The #begin line shows the source path.
        var text = MgcbWriter.Write(Document(
            [Texture("z.png"), Texture("Sprites/b.png"), Texture("a.png", "m.png"), Texture("Sprites/A.png")],
            copies: ["notes.txt", "Data/b.bin", "data/A.bin"]));

        var begins = text.Split('\n').Where(l => l.StartsWith("#begin ", StringComparison.Ordinal)).Select(l => l[7..]).ToArray();
        Assert.Equal(["a.png", "Sprites/A.png", "Sprites/b.png", "z.png", "data/A.bin", "Data/b.bin", "notes.txt"], begins);
    }

    [Fact]
    public void Sorting_uses_the_destination_when_there_is_one()
    {
        var text = MgcbWriter.Write(Document([Texture("a.png", "zz.png"), Texture("b.png", "aa.png"), Texture("c.png")]));

        var builds = text.Split('\n').Where(l => l.StartsWith("/build:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(["/build:b.png;aa.png", "/build:c.png", "/build:a.png;zz.png"], builds);
    }

    [Fact]
    public void Sorting_ignores_case_first_then_uses_ordinal_order()
    {
        var text = MgcbWriter.Write(Document([Texture("b.png"), Texture("a.png"), Texture("A.png"), Texture("B.png")], copies: ["x", "X", "a"]));

        var begins = text.Split('\n').Where(l => l.StartsWith("#begin ", StringComparison.Ordinal)).Select(l => l[7..]).ToArray();
        Assert.Equal(["A.png", "a.png", "B.png", "b.png", "a", "X", "x"], begins);
    }

    [Fact]
    public void Output_does_not_depend_on_the_order_of_the_input()
    {
        MgcbEntry[] entries =
        [
            Texture("a.png", null, Pair("P", "1")),
            Texture("a.png", null, Pair("P", "2")),
            Texture("A.png"),
            Texture("b/c.png"),
            new("b/c.png", null, "WavImporter", "SoundEffectProcessor", _noParams),
        ];
        var first = MgcbWriter.Write(Document(entries, ["q", "p"]));
        var second = MgcbWriter.Write(Document(entries.Reverse().ToArray(), ["p", "q"]));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Spaces_in_paths_are_written_as_they_are_without_quotes()
    {
        var text = MgcbWriter.Write(Document(
            [new("Sounds/Tropical paradise.mp3", null, "Mp3Importer", "SongProcessor", _noParams), Texture("My Textures/my hero.png", "My Textures/big hero.png")],
            copies: ["Data/level one.dat"],
            references: ["Pipeline Assemblies/My Pipeline.dll"]));

        Assert.Contains("\n#begin Sounds/Tropical paradise.mp3\n", text);
        Assert.Contains("\n/build:Sounds/Tropical paradise.mp3\n", text);
        Assert.Contains("\n/build:My Textures/my hero.png;My Textures/big hero.png\n", text);
        Assert.Contains("\n/copy:Data/level one.dat\n", text);
        Assert.Contains("\n/reference:Pipeline Assemblies/My Pipeline.dll\n", text);
        Assert.DoesNotContain("\"", text);
    }

    [Fact]
    public void Output_uses_LF_has_no_BOM_and_ends_with_exactly_one_newline()
    {
        var text = MgcbWriter.Write(Document([Texture("a.png")], ["b.txt"], ["r.dll"]));

        Assert.DoesNotContain("\r", text);
        Assert.NotEqual('\uFEFF', text[0]);
        Assert.EndsWith("\n", text);
        Assert.False(text.EndsWith("\n\n", StringComparison.Ordinal));
    }

    [Fact]
    public void Writing_twice_gives_identical_text()
    {
        var document = Document([Texture("b.png", "c.png"), Texture("a.png")], ["z"], ["r.dll"], MonoGameTarget.WindowsDX, "Reach", true);

        Assert.Equal(MgcbWriter.Write(document), MgcbWriter.Write(document));
    }

    [Fact]
    public void Empty_importer_and_processor_still_write_their_lines_so_nothing_leaks_from_the_previous_entry()
    {
        var text = MgcbWriter.Write(Document([new("a.dat", null, string.Empty, string.Empty, _noParams)]));

        Assert.EndsWith("#begin a.dat\n/importer:\n/processor:\n/build:a.dat\n", text);
    }

    [Fact]
    public Task Representative_document_snapshot()
    {
        var document = new MgcbDocument(
            MonoGameTarget.DesktopGL,
            "Reach",
            Compress: true,
            References: ["Pipeline/My Pipeline.dll"],
            Entries:
            [
                new("Textures/hero.png", null, "TextureImporter", "TextureProcessor", [new("PremultiplyAlpha", "True"), new("ColorKeyColor", "255,0,255,255"), new("ColorKeyEnabled", "True")]),
                new("Textures/hero sheet.png", "Textures/sheet.png", "TextureImporter", "TextureProcessor", _noParams),
                new("Fonts/Arial.spritefont", null, "FontDescriptionImporter", "FontDescriptionProcessor", [new("PremultiplyAlpha", "True")]),
                new("Sounds/Tropical paradise.mp3", null, "Mp3Importer", "SongProcessor", [new("Quality", "Best")]),
                new("Sounds/jump.wav", null, "WavImporter", "SoundEffectProcessor", _noParams),
                new("Shaders/Glow.fx", null, "EffectImporter", "EffectProcessor", [new("DebugMode", "Auto")]),
                new("Models/ship.fbx", "Models/player.fbx", "FbxImporter", "ModelProcessor", [new("Scale", "2"), new("GenerateMipmaps", "True")]),
                new("Levels/level1.xml", null, "XmlImporter", "PassThroughProcessor", _noParams),
            ],
            CopyPaths: ["Data/level one.dat", "Audio/Music.xsb"]);

        return Verifier.Verify(MgcbWriter.Write(document), "txt");
    }

    [Fact]
    public void ToEntry_without_a_rename_has_no_destination()
    {
        var entry = MgcbWriter.ToEntry(Asset("Textures/hero.png", "Textures/hero"), NoParams());

        Assert.Equal("Textures/hero.png", entry.SourcePath);
        Assert.Null(entry.DestinationPath);
        Assert.Equal("TextureImporter", entry.Importer);
        Assert.Equal("TextureProcessor", entry.Processor);
    }

    [Fact]
    public void ToEntry_with_a_name_override_uses_the_asset_name_plus_the_source_extension()
    {
        var entry = MgcbWriter.ToEntry(Asset("Textures/hero.png", "Textures/big"), NoParams());

        Assert.Equal("Textures/big.png", entry.DestinationPath);
    }

    [Fact]
    public void ToEntry_keeps_dots_inside_the_asset_name()
    {
        var entry = MgcbWriter.ToEntry(Asset("Textures/hero.png", "Textures/hero.v2"), NoParams());

        Assert.Equal("Textures/hero.v2.png", entry.DestinationPath);
    }

    [Fact]
    public void ToEntry_for_a_source_without_extension_has_no_destination_when_the_name_matches()
    {
        var entry = MgcbWriter.ToEntry(Asset("Data/blob", "Data/blob"), NoParams());

        Assert.Null(entry.DestinationPath);
    }

    [Fact]
    public void ToEntry_for_a_source_without_extension_and_another_name_uses_the_name_alone()
    {
        var entry = MgcbWriter.ToEntry(Asset("Data/blob", "Data/other"), NoParams());

        Assert.Equal("Data/other", entry.DestinationPath);
    }

    [Fact]
    public void ToEntry_turns_backslashes_into_slashes_and_drops_a_leading_dot_slash()
    {
        var entry = MgcbWriter.ToEntry(Asset(".\\Textures\\hero.png", "Textures/hero"), NoParams());

        Assert.Equal("Textures/hero.png", entry.SourcePath);
        Assert.Null(entry.DestinationPath);
    }

    [Fact]
    public void ToEntry_keeps_the_mapped_params_in_their_order()
    {
        var mapped = new MappedParams([new("Quality", "Best"), new("Another", "x")], []);

        var entry = MgcbWriter.ToEntry(Asset("Sounds/a.mp3", "Sounds/a", "Mp3Importer", "SongProcessor"), mapped);

        Assert.Equal(["Quality=Best", "Another=x"], entry.Params.Select(p => $"{p.Key}={p.Value}"));
        Assert.Equal("Mp3Importer", entry.Importer);
        Assert.Equal("SongProcessor", entry.Processor);
    }

    [Fact]
    public void ToEntry_and_the_name_resolver_agree_on_the_output_name()
    {
        ContentItem[] items =
        [
            new("Textures/hero.png", "big", "TextureImporter", "TextureProcessor", new Dictionary<string, string>()),
            new("Sounds/Tropical paradise.mp3", null, "Mp3Importer", "SongProcessor", new Dictionary<string, string>()),
            new("a.b.png", null, "TextureImporter", "TextureProcessor", new Dictionary<string, string>()),
        ];
        var names = AssetNameResolver.Resolve(items, "Content").Names;

        var entries = names.Select(n => MgcbWriter.ToEntry(
            new ConvertedAsset(n.Item, n.AssetName, n.Item.Importer!, n.Item.Processor!, AssetStatus.Converted, null),
            NoParams())).ToArray();

        Assert.Equal(["Textures/big.png", null, null], entries.Select(e => e.DestinationPath));
    }

    [Fact]
    public void A_clean_entry_has_no_problems()
    {
        Assert.Empty(MgcbWriter.FindProblems(Texture("Textures/my hero.png", "Textures/big hero.png", Pair("ColorKeyColor", "255, 0, 255, 255"))));
    }

    [Theory]
    [InlineData("Textures/a;b.png")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a\nb.png")]
    [InlineData("a\rb.png")]
    [InlineData("Content.mgcb")]
    [InlineData("Other/Project.MGCB")]
    public void Unwritable_source_paths_are_reported_and_refused(string source)
    {
        var entry = Texture(source);

        Assert.NotEmpty(MgcbWriter.FindProblems(entry));
        Assert.Throws<ArgumentException>(() => MgcbWriter.Write(Document([entry])));
    }

    [Theory]
    [InlineData("Textures/a;b.png")]
    [InlineData("Textures/x.mgcb")]
    [InlineData("a\nb.png")]
    public void Unwritable_destination_paths_are_reported_and_refused(string destination)
    {
        var entry = Texture("a.png", destination);

        Assert.NotEmpty(MgcbWriter.FindProblems(entry));
        Assert.Throws<ArgumentException>(() => MgcbWriter.Write(Document([entry])));
    }

    [Theory]
    [InlineData("Defines", "A=1")]
    [InlineData("ColorKeyColor", "{R:0 G:0 B:0 A:0}")]
    [InlineData("Name:With", "1")]
    [InlineData("Name=With", "1")]
    [InlineData("", "1")]
    [InlineData("Quality", "Best\nBad")]
    public void Params_that_mgcb_would_ignore_or_misread_are_reported_and_refused(string key, string value)
    {
        var entry = Texture("a.png", null, Pair(key, value));

        Assert.NotEmpty(MgcbWriter.FindProblems(entry));
        Assert.Throws<ArgumentException>(() => MgcbWriter.Write(Document([entry])));
    }

    [Fact]
    public void A_source_path_that_ends_in_mgcb_is_fine_when_a_destination_follows_it()
    {
        // The line is "/build:a.mgcb;a.png" and does not end in ".mgcb". Only the destination may not end that way.
        var entry = new MgcbEntry("a.mgcb", "a.png", "TextureImporter", "TextureProcessor", _noParams);

        Assert.Empty(MgcbWriter.FindProblems(entry));
    }

    [Theory]
    [InlineData("Quiet")]
    [InlineData("")]
    public void Unknown_profile_in_the_document_is_refused(string profile)
    {
        var problems = MgcbWriter.FindProblems(Document(profile: profile));

        Assert.Single(problems);
        Assert.Throws<ArgumentException>(() => MgcbWriter.Write(Document(profile: profile)));
    }

    [Fact]
    public void Bad_copy_paths_and_references_are_refused()
    {
        Assert.Throws<ArgumentException>(() => MgcbWriter.Write(Document(copies: ["a;b.txt"])));
        Assert.Throws<ArgumentException>(() => MgcbWriter.Write(Document(copies: ["old.mgcb"])));
        Assert.Throws<ArgumentException>(() => MgcbWriter.Write(Document(copies: [""])));
        Assert.Throws<ArgumentException>(() => MgcbWriter.Write(Document(references: ["a\nb.dll"])));
        Assert.Throws<ArgumentException>(() => MgcbWriter.Write(Document(references: ["x.mgcb"])));
    }

    [Fact]
    public void Document_problems_name_every_bad_item()
    {
        var problems = MgcbWriter.FindProblems(Document([Texture("a;b.png"), Texture("c;d.png")], ["e;f.txt"], profile: "Nope"));

        Assert.Contains(problems, p => p.Contains("a;b.png", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("c;d.png", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("e;f.txt", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("Nope", StringComparison.Ordinal));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => MgcbWriter.Write(null!));
        Assert.Throws<ArgumentNullException>(() => MgcbWriter.ToEntry(null!, NoParams()));
        Assert.Throws<ArgumentNullException>(() => MgcbWriter.ToEntry(Asset("a.png", "a"), null!));
        Assert.Throws<ArgumentNullException>(() => MgcbWriter.FindProblems((MgcbEntry)null!));
        Assert.Throws<ArgumentNullException>(() => MgcbWriter.FindProblems((MgcbDocument)null!));
    }
}

/// <summary>
/// Builds a file written by <see cref="MgcbWriter"/> with the real content builder, <c>dotnet-mgcb</c> 3.8.6.1.
/// Needs the network the first time (tool restore) and an installed Arial font, so it runs only when <c>XNAREBORN_INTEGRATION=1</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MgcbContentBuilderIntegrationTests(ITestOutputHelper testOutput) : IDisposable
{
    private const string _toolsManifest = """
        {
          "version": 1,
          "isRoot": true,
          "tools": {
            "dotnet-mgcb": {
              "version": "3.8.6.1",
              "commands": [ "mgcb" ]
            }
          }
        }
        """;

    private const string _arialFont = """
        <?xml version="1.0" encoding="utf-8"?>
        <XnaContent xmlns:Graphics="Microsoft.Xna.Framework.Content.Pipeline.Graphics">
          <Asset Type="Graphics:FontDescription">
            <FontName>Arial</FontName>
            <Size>14</Size>
            <Spacing>0</Spacing>
            <UseKerning>true</UseKerning>
            <Style>Regular</Style>
            <CharacterRegions>
              <CharacterRegion>
                <Start>&#32;</Start>
                <End>&#126;</End>
              </CharacterRegion>
            </CharacterRegions>
          </Asset>
        </XnaContent>
        """;

    /// <summary>A 1 by 1 pixel RGBA PNG.</summary>
    private static readonly byte[] _png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01,
        0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41,
        0x54, 0x78, 0xDA, 0x63, 0xFC, 0xCF, 0xC0, 0x50, 0x0F, 0x00, 0x04, 0x85, 0x01, 0x80, 0x84, 0xA9, 0x8C, 0x21, 0x00, 0x00,
        0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
    ];

    private static readonly TimeSpan _toolTimeout = TimeSpan.FromMinutes(5);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "xr-mgcb-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            try
            {
                Directory.Delete(_folder, recursive: true);
            }
            catch (IOException)
            {
                // The folder is in the temp directory and can be removed later.
            }
        }
    }

    [IntegrationFact]
    public async Task Desktop_document_with_every_entry_kind_builds_to_the_expected_xnb_files()
    {
        var document = SampleDocument(MonoGameTarget.DesktopGL, "HiDef", compress: false);

        await BuildAsync(document);

        Assert.Equal(
            [
                "Data/level one.dat",
                "Fonts/Arial.xnb",
                "Sounds/Tropical paradise.xnb",
                "Textures/big.xnb",
                "Textures/hero.v2.xnb",
                "Textures/my hero.xnb",
            ],
            BuiltFiles("DesktopGL"));
        Assert.False(File.Exists(Path.Combine(_folder, "bin", "DesktopGL", "Textures", "hero.xnb")), "The renamed asset must not also appear under its source name.");
        Assert.Equal(('d', (byte)0x01), Header("DesktopGL", "Textures/my hero.xnb"));
    }

    [IntegrationFact]
    public async Task Windows_reach_compressed_document_builds_under_the_Windows_output_folder()
    {
        var document = SampleDocument(MonoGameTarget.WindowsDX, MgcbWriter.MapProfile("reach"), compress: true);

        await BuildAsync(document);

        Assert.Equal(
            [
                "Data/level one.dat",
                "Fonts/Arial.xnb",
                "Sounds/Tropical paradise.xnb",
                "Textures/big.xnb",
                "Textures/hero.v2.xnb",
                "Textures/my hero.xnb",
            ],
            BuiltFiles("Windows"));
        Assert.Equal(('w', (byte)0x40), Header("Windows", "Textures/my hero.xnb"));
    }

    private static MgcbDocument SampleDocument(MonoGameTarget target, string profile, bool compress)
    {
        var png = new ConvertedAsset(
            new ContentItem("Textures/my hero.png", null, "TextureImporter", "TextureProcessor", new Dictionary<string, string>()),
            "Textures/my hero",
            "TextureImporter",
            "TextureProcessor",
            AssetStatus.Converted,
            null);
        var renamed = png with
        {
            Item = png.Item with { SourcePath = "Textures/sprite.png", NameOverride = "hero.v2" },
            AssetName = "Textures/hero.v2",
        };
        var renamedPlain = png with
        {
            Item = png.Item with { SourcePath = "Textures/hero.png", NameOverride = "big" },
            AssetName = "Textures/big",
        };
        var wav = new ConvertedAsset(
            new ContentItem("Sounds/Tropical paradise.wav", null, "WavImporter", "SoundEffectProcessor", new Dictionary<string, string>()),
            "Sounds/Tropical paradise",
            "WavImporter",
            "SoundEffectProcessor",
            AssetStatus.Converted,
            null);
        var font = new ConvertedAsset(
            new ContentItem("Fonts/Arial.spritefont", null, "FontDescriptionImporter", "FontDescriptionProcessor", new Dictionary<string, string>()),
            "Fonts/Arial",
            "FontDescriptionImporter",
            "FontDescriptionProcessor",
            AssetStatus.Converted,
            null);

        var textureParams = new MappedParams([new("PremultiplyAlpha", "True"), new("ColorKeyColor", "255,0,255,255"), new("ColorKeyEnabled", "False")], []);
        var noParams = new MappedParams([], []);

        return new MgcbDocument(
            target,
            profile,
            compress,
            References: [],
            Entries:
            [
                MgcbWriter.ToEntry(font, new MappedParams([new("PremultiplyAlpha", "True")], [])),
                MgcbWriter.ToEntry(wav, new MappedParams([new("Quality", "Best")], [])),
                MgcbWriter.ToEntry(png, textureParams),
                MgcbWriter.ToEntry(renamed, noParams),
                MgcbWriter.ToEntry(renamedPlain, noParams),
            ],
            CopyPaths: ["Data/level one.dat"]);
    }

    private async Task BuildAsync(MgcbDocument document)
    {
        Directory.CreateDirectory(_folder);
        CreateAssets();
        var mgcb = MgcbWriter.Write(document);
        await File.WriteAllTextAsync(Path.Combine(_folder, "Content.mgcb"), mgcb, new UTF8Encoding(false));
        Directory.CreateDirectory(Path.Combine(_folder, ".config"));
        await File.WriteAllTextAsync(Path.Combine(_folder, ".config", "dotnet-tools.json"), _toolsManifest, new UTF8Encoding(false));
        testOutput.WriteLine(mgcb);

        var (restoreCode, restoreText) = await RunAsync("tool restore");
        testOutput.WriteLine(restoreText);
        Assert.True(restoreCode == 0, $"dotnet tool restore failed with exit code {restoreCode}.{Environment.NewLine}{restoreText}");

        var (buildCode, buildText) = await RunAsync("mgcb /@:Content.mgcb");
        testOutput.WriteLine(buildText);
        Assert.True(buildCode == 0, $"dotnet mgcb failed with exit code {buildCode}.{Environment.NewLine}{buildText}");
        Assert.Contains("Build 6 succeeded, 0 failed.", buildText, StringComparison.Ordinal);
    }

    private void CreateAssets()
    {
        void Write(string relative, byte[] bytes)
        {
            var path = Path.Combine(_folder, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }

        Write("Textures/my hero.png", _png);
        Write("Textures/sprite.png", _png);
        Write("Textures/hero.png", _png);
        Write("Sounds/Tropical paradise.wav", Wav());
        Write("Fonts/Arial.spritefont", new UTF8Encoding(false).GetBytes(_arialFont));
        Write("Data/level one.dat", [1, 2, 3, 4]);
    }

    /// <summary>A mono, 8 kHz, 8-bit PCM WAV with eight samples.</summary>
    private static byte[] Wav()
    {
        byte[] samples = [128, 200, 128, 50, 128, 200, 128, 50];
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + samples.Length);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(8000);
            writer.Write(8000);
            writer.Write((short)1);
            writer.Write((short)8);
            writer.Write("data"u8);
            writer.Write(samples.Length);
            writer.Write(samples);
        }

        return stream.ToArray();
    }

    private string[] BuiltFiles(string platformFolder)
    {
        var root = Path.Combine(_folder, "bin", platformFolder);
        Assert.True(Directory.Exists(root), $"The content builder did not create {root}.");
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Reads the XNB header: "XNB", the platform letter, the format version and the flag byte. In the flag byte 0x01 means the HiDef
    /// profile and 0x40 means LZ4 compression (checked with the tool: Reach without compression gives 0x00, HiDef with it 0x41).
    /// </summary>
    private (char Platform, byte Flags) Header(string platformFolder, string relative)
    {
        var bytes = File.ReadAllBytes(Path.Combine(_folder, "bin", platformFolder, relative.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Equal("XNB", Encoding.ASCII.GetString(bytes, 0, 3));
        testOutput.WriteLine($"{platformFolder}/{relative}: platform '{(char)bytes[3]}', flags 0x{bytes[5]:X2}");
        return ((char)bytes[3], bytes[5]);
    }

    private async Task<(int ExitCode, string Output)> RunAsync(string arguments)
    {
        var start = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = _folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        var log = new StringBuilder();
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => Append(log, e.Data);
        process.ErrorDataReceived += (_, e) => Append(log, e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = new CancellationTokenSource(_toolTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return (-1, $"dotnet {arguments} did not finish within {_toolTimeout.TotalMinutes} minutes.{Environment.NewLine}{Snapshot(log)}");
        }

        // Flush the redirected streams.
        process.WaitForExit();
        return (process.ExitCode, Snapshot(log));
    }

    private static void Append(StringBuilder log, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (log)
        {
            log.AppendLine(line);
        }
    }

    private static string Snapshot(StringBuilder log)
    {
        lock (log)
        {
            return log.ToString();
        }
    }
}
