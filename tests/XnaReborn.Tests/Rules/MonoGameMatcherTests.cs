using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Analyzer.Rules.Matchers;
using XnaReborn.Core.Mgcb;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Tests.Rules;

/// <summary>Task 6: <c>projectItem.metadata</c> and the <c>mgcbSetting</c> match, each with positive and negative cases.</summary>
[Trait("Category", "Rules")]
public sealed class MonoGameMatcherTests
{
    private const string _mgcb = """
        /platform:Windows
        /profile:Reach
        /compress:False
        /config:
        /reference:..\..\Program Files (x86)\MonoGame\v3.0\Tools\MonoGame.Content.Pipeline.dll
        /reference:Pipeline\GlowPipeline.dll

        #begin Textures/hero.png
        /importer:TextureImporter
        /processor:TextureProcessor
        /build:Textures/hero.png

        #begin Data/level.lvl
        /importer:LevelImporter
        /processor:LevelProcessor
        /build:Data/level.lvl

        #begin Sounds/click.wav
        /copy:Sounds/click.wav
        """;

    private static ProjectReadResult Project(IReadOnlyList<ProjectItem>? items = null, string? mgcb = null)
    {
        var info = new ProjectInfo("Game.csproj", "Game", false, XnaVersion.Unknown, XnaPlatform.Unknown, null, null, [], [], [], [], [], ProjectFamily.MonoGameLegacy);
        IReadOnlyList<ParsedMgcb>? files = mgcb is null ? null : [new ParsedMgcb("Content/Content.mgcb", MgcbParser.Parse(mgcb))];
        return new ProjectReadResult(info, new Dictionary<string, string>(), items ?? [], [], null, files);
    }

    private static ProjectItem Package(string id, string? version)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (version is not null)
        {
            metadata["Version"] = version;
        }

        return new ProjectItem("PackageReference", id, metadata);
    }

    private static MgcbSettingMatch Setting(string setting, string? equals = null, string? matches = null) =>
        new(setting, equals, matches);

    // ---- projectItem.metadata ----

    private static ProjectItemMatch NotPinned(string? include = "MonoGame.Framework.*") =>
        new("PackageReference", include, new MetadataFilter("Version", NotEquals: "3.8.6.1"));

    [Fact]
    public void Metadata_notEquals_finds_a_package_below_the_pin()
    {
        var project = Project([Package("MonoGame.Framework.DesktopGL", "3.8.0.1641")]);

        Assert.Equal(["MonoGame.Framework.DesktopGL"], ProjectItemMatcher.Match(NotPinned(), project));
    }

    [Fact]
    public void Metadata_notEquals_ignores_a_package_at_the_pin()
    {
        var project = Project([Package("MonoGame.Framework.DesktopGL", "3.8.6.1")]);

        Assert.Empty(ProjectItemMatcher.Match(NotPinned(), project));
    }

    [Fact]
    public void Metadata_filters_do_not_match_an_item_without_that_metadata()
    {
        var project = Project([Package("MonoGame.Framework.DesktopGL", null)]);

        Assert.Empty(ProjectItemMatcher.Match(NotPinned(), project));
        Assert.Empty(ProjectItemMatcher.Match(new ProjectItemMatch("PackageReference", null, new MetadataFilter("Version", EqualsValue: "1")), project));
        Assert.Empty(ProjectItemMatcher.Match(new ProjectItemMatch("PackageReference", null, new MetadataFilter("Version", MatchesPattern: ".")), project));
    }

    [Fact]
    public void Metadata_equals_and_matches_filter_by_value_and_name_ignores_case()
    {
        var project = Project([Package("A", "3.8.6.1"), Package("B", "3.7.0.1708")]);

        Assert.Equal(["A"], ProjectItemMatcher.Match(new ProjectItemMatch("PackageReference", null, new MetadataFilter("version", EqualsValue: "3.8.6.1")), project));
        Assert.Equal(["B"], ProjectItemMatcher.Match(new ProjectItemMatch("PackageReference", null, new MetadataFilter("Version", MatchesPattern: @"^3\.7\.")), project));
    }

    [Fact]
    public void Metadata_filters_combine_and_the_include_still_applies()
    {
        var project = Project([Package("MonoGame.Extended", "4.0.0"), Package("MonoGame.Framework.DesktopGL", "3.8.0.1641")]);
        var match = new ProjectItemMatch("PackageReference", "MonoGame.Framework.*", new MetadataFilter("Version", NotEquals: "3.8.6.1", MatchesPattern: @"^3\.8\."));

        Assert.Equal(["MonoGame.Framework.DesktopGL"], ProjectItemMatcher.Match(match, project));
    }

    [Fact]
    public void Without_metadata_the_projectItem_match_is_unchanged()
    {
        var project = Project([Package("MonoGame.Framework.DesktopGL", null), new ProjectItem("Reference", "System", new Dictionary<string, string>())]);

        Assert.Equal(["MonoGame.Framework.DesktopGL"], ProjectItemMatcher.Match(new ProjectItemMatch("PackageReference", "MonoGame.*"), project));
    }

    [Fact]
    public async Task Version_attributes_and_child_elements_are_both_read_as_metadata()
    {
        var folder = Path.Combine(Path.GetTempPath(), "xnareborn-meta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(
                Path.Combine(folder, "Game.csproj"),
                """<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.0.1641" /><PackageReference Include="MonoGame.Framework.WindowsDX"><Version>3.8.1.303</Version></PackageReference></ItemGroup></Project>""");

            var read = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(folder, "Game.csproj"), folder, CancellationToken.None);

            Assert.Equal(["MonoGame.Framework.DesktopGL", "MonoGame.Framework.WindowsDX"], ProjectItemMatcher.Match(NotPinned(), read));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // ---- mgcbSetting: global settings ----

    [Theory]
    [InlineData("platform", "Windows", null)]
    [InlineData("platform", null, "^(Windows|DesktopGL)$")]
    [InlineData("profile", "reach", null)]
    [InlineData("compress", "False", null)]
    [InlineData("config", "", null)]
    [InlineData("platform", null, null)]
    public void Global_setting_positive(string setting, string? equals, string? matches)
    {
        var found = MgcbSettingMatcher.Match(Setting(setting, equals, matches), Project(mgcb: _mgcb));

        var hit = Assert.Single(found);
        Assert.Equal("Content/Content.mgcb", hit.Path);
        Assert.StartsWith(setting + "=", hit.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("platform", "DesktopGL", null)]
    [InlineData("platform", null, "^(DesktopGL|WindowsStoreApp)$")]
    [InlineData("profile", "HiDef", null)]
    [InlineData("compress", "True", null)]
    public void Global_setting_negative_when_the_value_differs(string setting, string? equals, string? matches)
    {
        Assert.Empty(MgcbSettingMatcher.Match(Setting(setting, equals, matches), Project(mgcb: _mgcb)));
    }

    [Fact]
    public void Global_setting_negative_when_the_file_does_not_state_it()
    {
        Assert.Empty(MgcbSettingMatcher.Match(Setting("compress"), Project(mgcb: "/platform:Windows\n")));
    }

    [Fact]
    public void A_project_without_an_mgcb_file_never_matches()
    {
        Assert.Empty(MgcbSettingMatcher.Match(Setting("platform"), Project()));
        Assert.Empty(MgcbSettingMatcher.Match(new MgcbSettingMatch("entry", NonStandardPair: true), Project()));
    }

    [Fact]
    public void Global_setting_reports_the_line_of_the_option()
    {
        var found = MgcbSettingMatcher.Match(Setting("profile"), Project(mgcb: _mgcb));

        Assert.Equal(2, Assert.Single(found).Line);
    }

    // ---- mgcbSetting: references ----

    private const string _installFolder = @"(?i)MonoGame\\v3\.0\\";

    [Fact]
    public void Reference_install_folder_positive_and_line()
    {
        var found = MgcbSettingMatcher.Match(Setting("reference", matches: _installFolder), Project(mgcb: _mgcb));

        var hit = Assert.Single(found);
        Assert.Equal(5, hit.Line);
        Assert.Contains("MonoGame.Content.Pipeline.dll", hit.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reference_install_folder_negative_when_there_are_no_references()
    {
        Assert.Empty(MgcbSettingMatcher.Match(Setting("reference", matches: _installFolder), Project(mgcb: "/platform:DesktopGL\n")));
    }

    [Fact]
    public void Reference_install_folder_negative_when_the_reference_is_a_custom_assembly()
    {
        Assert.Empty(MgcbSettingMatcher.Match(Setting("reference", matches: _installFolder), Project(mgcb: "/reference:Pipeline\\GlowPipeline.dll\n")));
    }

    // The pattern the catalog uses for "a custom pipeline assembly": any reference that is not in the install folder.
    private const string _customAssembly = @"^(?!.*MonoGame\\v3\.0\\).+$";

    [Fact]
    public void Custom_assembly_pattern_finds_only_the_custom_reference()
    {
        var found = MgcbSettingMatcher.Match(Setting("reference", matches: _customAssembly), Project(mgcb: _mgcb));

        var hit = Assert.Single(found);
        Assert.Equal("reference=Pipeline\\GlowPipeline.dll", hit.Text);
        Assert.Equal(6, hit.Line);
    }

    [Fact]
    public void Custom_assembly_pattern_negative_when_only_the_install_folder_is_referenced()
    {
        var mgcb = "/reference:..\\..\\Program Files (x86)\\MonoGame\\v3.0\\Tools\\MonoGame.Content.Pipeline.dll\n";

        Assert.Empty(MgcbSettingMatcher.Match(Setting("reference", matches: _customAssembly), Project(mgcb: mgcb)));
    }

    // ---- mgcbSetting: entries ----

    [Fact]
    public void Entry_nonStandardPair_positive_finds_the_custom_importer_and_its_line()
    {
        var found = MgcbSettingMatcher.Match(new MgcbSettingMatch("entry", NonStandardPair: true), Project(mgcb: _mgcb));

        var hit = Assert.Single(found);
        Assert.Equal(13, hit.Line);
        Assert.Equal("Data/level.lvl (LevelImporter/LevelProcessor)", hit.Text);
    }

    [Fact]
    public void Entry_nonStandardPair_negative_when_only_standard_pairs_and_copies()
    {
        var mgcb = "#begin a.png\n/importer:TextureImporter\n/processor:TextureProcessor\n/build:a.png\n#begin b.wav\n/copy:b.wav\n";

        Assert.Empty(MgcbSettingMatcher.Match(new MgcbSettingMatch("entry", NonStandardPair: true), Project(mgcb: mgcb)));
    }

    [Theory]
    [InlineData("Texture*", null, 1)]
    [InlineData(null, "LevelProcessor", 1)]
    [InlineData("TextureImporter", "TextureProcessor", 1)]
    [InlineData("TextureImporter", "LevelProcessor", 0)]
    [InlineData("Missing*", null, 0)]
    [InlineData(null, null, 3)]
    public void Entry_importer_and_processor_filters(string? importer, string? processor, int expected)
    {
        var found = MgcbSettingMatcher.Match(new MgcbSettingMatch("entry", Importer: importer, Processor: processor), Project(mgcb: _mgcb));

        Assert.Equal(expected, found.Count);
    }

    // ---- through the engine ----

    [Fact]
    public void The_engine_gives_one_project_level_finding_located_at_the_first_match()
    {
        var catalog = RuleCatalogLoader.Parse("""
            {"version":1,"name":"t","appliesTo":"monogame","rules":[
              {"id":"MG901","title":"t","severity":"Warning","classification":"Manual",
               "match":{"type":"mgcbSetting","setting":"reference","matches":".dll"},"explanation":"e","resolution":"r"}]}
            """);
        var engine = new RuleEngine(catalog, new ReadOnlyFileSystem());

        var findings = engine.EvaluateProject(Project(mgcb: _mgcb), Path.GetTempPath(), "Game.csproj", CancellationToken.None);

        var finding = Assert.Single(findings);
        Assert.Equal(new SourceLocation("Content/Content.mgcb", 5, 1), finding.Location);
        Assert.Contains("MonoGame.Content.Pipeline.dll", finding.Snippet, StringComparison.Ordinal);
        Assert.Contains("GlowPipeline.dll", finding.Snippet, StringComparison.Ordinal);
    }
}
