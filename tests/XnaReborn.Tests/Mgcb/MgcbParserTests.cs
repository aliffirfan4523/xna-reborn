using XnaReborn.Core.Mgcb;

namespace XnaReborn.Tests.Mgcb;

/// <summary>Task 3.2: <see cref="MgcbParser"/> turns mgcb text into a model with line numbers and never throws.</summary>
public sealed class MgcbParserTests
{
    private const string _sample = """

        #----------------------------- Global Properties ----------------------------#

        /outputDir:bin/$(Platform)
        /intermediateDir:obj/$(Platform)
        /platform:DesktopGL
        /config:
        /profile:Reach
        /compress:False
        /rebuild

        #-------------------------------- References --------------------------------#

        /reference:..\..\..\..\Program Files (x86)\MonoGame\v3.0\Tools\MonoGame.Content.Pipeline.dll
        /reference:Pipeline\GlowPipeline.dll

        #---------------------------------- Content ---------------------------------#

        #begin Textures/hero.png
        /importer:TextureImporter
        /processor:TextureProcessor
        /processorParam:ColorKeyColor=255,0,255,255
        /processorParam:GenerateMipmaps=False
        /build:Textures/hero.png

        #begin Sounds/click.wav
        /copy:Sounds/click.wav;Out/click.wav

        """;

    private static string WithLineEnding(string text, string ending) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", ending, StringComparison.Ordinal);

    [Fact]
    public void Global_settings_keep_their_values_and_line_numbers()
    {
        var file = MgcbParser.Parse(_sample);

        Assert.Equal("DesktopGL", file.Setting("platform")!.Value);
        Assert.Equal(6, file.Setting("platform")!.Line);
        Assert.Equal("Reach", file.Setting("PROFILE")!.Value);
        Assert.Equal("False", file.Setting("compress")!.Value);
        Assert.Equal(string.Empty, file.Setting("config")!.Value);
        Assert.Null(file.Setting("rebuild")!.Value);
        Assert.Null(file.Setting("missing"));
        Assert.Empty(file.Diagnostics);
    }

    [Fact]
    public void References_are_listed_with_their_paths_and_lines()
    {
        var file = MgcbParser.Parse(_sample);

        Assert.Equal(2, file.References.Count);
        Assert.Equal(@"..\..\..\..\Program Files (x86)\MonoGame\v3.0\Tools\MonoGame.Content.Pipeline.dll", file.References[0].Path);
        Assert.Equal(14, file.References[0].Line);
        Assert.Equal(@"Pipeline\GlowPipeline.dll", file.References[1].Path);
        Assert.DoesNotContain(file.GlobalSettings, s => s.Name.Equals("reference", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Entries_carry_importer_processor_parameters_and_the_begin_line()
    {
        var file = MgcbParser.Parse(_sample);

        Assert.Equal(2, file.Entries.Count);
        var hero = file.Entries[0];
        Assert.Equal("Textures/hero.png", hero.Source);
        Assert.Null(hero.Destination);
        Assert.False(hero.IsCopy);
        Assert.Equal("TextureImporter", hero.Importer);
        Assert.Equal("TextureProcessor", hero.Processor);
        Assert.Equal(["ColorKeyColor", "GenerateMipmaps"], hero.Parameters.Select(p => p.Name));
        Assert.Equal(["255,0,255,255", "False"], hero.Parameters.Select(p => p.Value));
        Assert.Equal(19, hero.Line);

        var click = file.Entries[1];
        Assert.True(click.IsCopy);
        Assert.Equal("Sounds/click.wav", click.Source);
        Assert.Equal("Out/click.wav", click.Destination);
        Assert.Null(click.Importer);
        Assert.Empty(click.Parameters);
    }

    [Fact]
    public void Importer_does_not_leak_from_one_entry_to_the_next()
    {
        var file = MgcbParser.Parse("#begin a.png\n/importer:TextureImporter\n/build:a.png\n#begin b.png\n/build:b.png\n");

        Assert.Equal("TextureImporter", file.Entries[0].Importer);
        Assert.Null(file.Entries[1].Importer);
    }

    [Fact]
    public void An_entry_without_begin_uses_the_build_line_as_its_line()
    {
        var file = MgcbParser.Parse("/importer:TextureImporter\n/build:a.png\n");

        Assert.Equal(2, Assert.Single(file.Entries).Line);
        Assert.Equal("TextureImporter", file.Entries[0].Importer);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Line_endings_do_not_change_the_result(string ending)
    {
        var lf = MgcbParser.Parse(_sample);
        var other = MgcbParser.Parse(WithLineEnding(_sample, ending));

        Assert.Equal(lf.GlobalSettings, other.GlobalSettings);
        Assert.Equal(lf.References, other.References);
        Assert.Equal(lf.Entries.Select(e => (e.Source, e.Importer, e.Line)), other.Entries.Select(e => (e.Source, e.Importer, e.Line)));
    }

    [Fact]
    public void Comments_and_blank_lines_are_ignored()
    {
        var file = MgcbParser.Parse("# just a comment\n\n   \n#---- section ----#\n#beginning is a comment too\n/platform:Windows\n");

        Assert.Equal("Windows", Assert.Single(file.GlobalSettings).Value);
        Assert.Empty(file.Entries);
        Assert.Empty(file.Diagnostics);
    }

    [Fact]
    public void A_malformed_line_becomes_a_diagnostic_and_parsing_continues()
    {
        var file = MgcbParser.Parse("/platform:Windows\nthis is not an option\n/:\n/processorParam:NoEquals\n/build:\n/reference:\n/profile:Reach\n", "Game/Content.mgcb");

        Assert.Equal(["platform", "profile"], file.GlobalSettings.Select(s => s.Name));
        Assert.Equal(5, file.Diagnostics.Count);
        Assert.All(file.Diagnostics, d =>
        {
            Assert.Equal("ANA010", d.Code);
            Assert.Equal("Game/Content.mgcb", d.File);
        });
        Assert.Contains("line 2", file.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_text_gives_an_empty_file()
    {
        var file = MgcbParser.Parse(string.Empty);

        Assert.Empty(file.GlobalSettings);
        Assert.Empty(file.References);
        Assert.Empty(file.Entries);
        Assert.Empty(file.Diagnostics);
    }

    [Fact]
    public void The_fixture_mgcb_files_parse_without_diagnostics()
    {
        var root = Path.GetFullPath(Path.Combine(Fixtures.FixturePaths.Root));
        var files = Directory.EnumerateFiles(root, "*.mgcb", SearchOption.AllDirectories).ToList();

        Assert.NotEmpty(files);
        Assert.All(files, f => Assert.Empty(MgcbParser.Parse(File.ReadAllText(f), f).Diagnostics));
    }
}
