using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Tests.Rules;

/// <summary>Task 3.3 and 3.4: the schema accepts MG ids, appliesTo, mgcbSetting and projectItem metadata; ids may not repeat across catalogs.</summary>
[Trait("Category", "Rules")]
public sealed class MonoGameSchemaTests
{
    private static string Catalog(string rules, string extra = "") =>
        $$"""{"version":1,"name":"test"{{extra}},"rules":[{{rules}}]}""";

    private static string Rule(string id, string match, string extra = "") =>
        $$"""
        {"id":"{{id}}","title":"t","severity":"Warning","classification":"Manual",
         "match":{{match}},"explanation":"e","resolution":"r"{{extra}}}
        """;

    private static RuleCatalogException Fails(string json) =>
        Assert.Throws<RuleCatalogException>(() => RuleCatalogLoader.Parse(json));

    [Theory]
    [InlineData("MG001")]
    [InlineData("MG999")]
    [InlineData("XNA001")]
    public void Id_pattern_accepts_xna_and_mg_ids(string id)
    {
        var catalog = RuleCatalogLoader.Parse(Catalog(Rule(id, """{"type":"reference","name":"x"}""")));

        Assert.Equal(id, Assert.Single(catalog.Rules).Id);
    }

    [Theory]
    [InlineData("MG1")]
    [InlineData("MG0001")]
    [InlineData("ABC001")]
    [InlineData("mg001")]
    public void Id_pattern_rejects_other_ids(string id)
    {
        var exception = Fails(Catalog(Rule(id, """{"type":"reference","name":"x"}""")));

        Assert.Contains(exception.Errors, e => e.Contains("/rules/0/id:", StringComparison.Ordinal));
    }

    [Fact]
    public void Error_lines_name_an_mg_rule_by_its_id()
    {
        var exception = Fails(Catalog(Rule("MG002", """{"type":"reference","name":"x","other":1}""")));

        Assert.Contains(exception.Errors, e => e.StartsWith("MG002 /rules/0/match/other:", StringComparison.Ordinal));
    }

    [Fact]
    public void AppliesTo_defaults_to_xna_and_reads_monogame()
    {
        var xna = RuleCatalogLoader.Parse(Catalog(string.Empty));
        var mg = RuleCatalogLoader.Parse(Catalog(string.Empty, ",\"appliesTo\":\"monogame\""));

        Assert.Null(xna.AppliesTo);
        Assert.True(xna.AppliesToFamily(ProjectFamily.Xna));
        Assert.False(xna.AppliesToFamily(ProjectFamily.MonoGameLegacy));
        Assert.Equal("monogame", mg.AppliesTo);
        Assert.True(mg.AppliesToFamily(ProjectFamily.MonoGameLegacy));
        Assert.True(mg.AppliesToFamily(ProjectFamily.MonoGameCurrent));
        Assert.False(mg.AppliesToFamily(ProjectFamily.Xna));
        Assert.False(mg.AppliesToFamily(ProjectFamily.Other));
    }

    [Fact]
    public void AppliesTo_rejects_an_unknown_value()
    {
        var exception = Fails(Catalog(string.Empty, ",\"appliesTo\":\"playstation\""));

        Assert.Contains(exception.Errors, e => e.Contains("/appliesTo:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{"type":"mgcbSetting","setting":"platform","equals":"Windows"}""")]
    [InlineData("""{"type":"mgcbSetting","setting":"reference","matches":"(?i)MonoGame\\\\v3\\.0\\\\"}""")]
    [InlineData("""{"type":"mgcbSetting","setting":"compress"}""")]
    [InlineData("""{"type":"mgcbSetting","setting":"entry","importer":"Texture*","processor":"Outline*","nonStandardPair":true}""")]
    public void MgcbSetting_valid_forms_load(string match)
    {
        var catalog = RuleCatalogLoader.Parse(Catalog(Rule("MG023", match)));

        Assert.IsType<MgcbSettingMatch>(Assert.Single(catalog.Rules).Match);
    }

    [Theory]
    [InlineData("""{"type":"mgcbSetting"}""", "/rules/0/match")]
    [InlineData("""{"type":"mgcbSetting","setting":"outputDir"}""", "/rules/0/match/setting")]
    [InlineData("""{"type":"mgcbSetting","setting":"platform","extra":1}""", "/rules/0/match/extra")]
    [InlineData("""{"type":"mgcbSetting","setting":"platform","matches":"("}""", "/rules/0/match/matches")]
    [InlineData("""{"type":"mgcbSetting","setting":"platform","importer":"X"}""", "/rules/0/match/importer")]
    [InlineData("""{"type":"mgcbSetting","setting":"entry","equals":"X"}""", "/rules/0/match/equals")]
    public void MgcbSetting_invalid_forms_are_rejected_with_a_path(string match, string path)
    {
        var exception = Fails(Catalog(Rule("MG023", match)));

        Assert.Contains(exception.Errors, e => e.Contains(path + ":", StringComparison.Ordinal));
    }

    [Fact]
    public void ProjectItem_metadata_filter_loads_into_the_record()
    {
        var json = """{"type":"projectItem","itemType":"PackageReference","include":"MonoGame.*","metadata":{"name":"Version","notEquals":"3.8.6.1"}}""";

        var match = Assert.IsType<ProjectItemMatch>(Assert.Single(RuleCatalogLoader.Parse(Catalog(Rule("MG008", json))).Rules).Match);

        Assert.Equal(new MetadataFilter("Version", null, "3.8.6.1", null), match.Metadata);
    }

    [Fact]
    public void ProjectItem_without_metadata_still_loads_and_has_no_filter()
    {
        var match = Assert.IsType<ProjectItemMatch>(Assert.Single(RuleCatalogLoader.Parse(Catalog(Rule("MG001", """{"type":"projectItem","itemType":"Import"}"""))).Rules).Match);

        Assert.Null(match.Metadata);
    }

    [Theory]
    [InlineData("""{"type":"projectItem","itemType":"Import","metadata":{}}""", "/rules/0/match/metadata")]
    [InlineData("""{"type":"projectItem","itemType":"Import","metadata":{"name":"Version","extra":1}}""", "/rules/0/match/metadata/extra")]
    [InlineData("""{"type":"projectItem","itemType":"Import","metadata":{"name":"Version","matches":"("}}""", "/rules/0/match/metadata/matches")]
    public void ProjectItem_invalid_metadata_is_rejected_with_a_path(string match, string path)
    {
        var exception = Fails(Catalog(Rule("MG008", match)));

        Assert.Contains(exception.Errors, e => e.Contains(path, StringComparison.Ordinal));
    }

    [Fact]
    public void Mgcb_and_metadata_matches_round_trip_through_json()
    {
        var original = new[]
        {
            (RuleMatch)new MgcbSettingMatch("entry", Importer: "X*", NonStandardPair: true),
            new MgcbSettingMatch("platform", EqualsValue: "Windows"),
            new ProjectItemMatch("PackageReference", "MonoGame.*", new MetadataFilter("Version", NotEquals: "3.8.6.1")),
        };

        foreach (var match in original)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(match, RuleCatalogJson.Options);
            Assert.Equal(match, System.Text.Json.JsonSerializer.Deserialize<RuleMatch>(json, RuleCatalogJson.Options));
        }
    }

    [Fact]
    public void Same_id_in_two_catalogs_is_rejected_and_every_duplicate_is_listed()
    {
        var a = RuleCatalogLoader.Parse(Catalog(Rule("XNA001", """{"type":"reference","name":"x"}""") + "," + Rule("XNA002", """{"type":"reference","name":"y"}""")));
        var b = RuleCatalogLoader.Parse(Catalog(Rule("XNA002", """{"type":"reference","name":"z"}""") + "," + Rule("XNA001", """{"type":"reference","name":"w"}""")));

        var exception = Assert.Throws<RuleCatalogException>(() => RuleCatalogLoader.EnsureUniqueIds([a, b]));

        Assert.Equal(2, exception.Errors.Count);
        Assert.Contains(exception.Errors, e => e.StartsWith("XNA001", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, e => e.StartsWith("XNA002", StringComparison.Ordinal));
    }

    [Fact]
    public void Different_ids_in_two_catalogs_are_accepted()
    {
        var a = RuleCatalogLoader.Parse(Catalog(Rule("XNA001", """{"type":"reference","name":"x"}""")));
        var b = RuleCatalogLoader.Parse(Catalog(Rule("MG001", """{"type":"reference","name":"x"}""")));

        RuleCatalogLoader.EnsureUniqueIds([a, b]);
    }

    [Fact]
    public async Task LoadAllAsync_rejects_the_same_file_loaded_twice()
    {
        var path = Path.GetFullPath(Path.Combine(Fixtures.FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json"));

        var exception = await Assert.ThrowsAsync<RuleCatalogException>(() =>
            RuleCatalogLoader.LoadAllAsync([path, path], new ReadOnlyFileSystem(), CancellationToken.None));

        Assert.NotEmpty(exception.Errors);
    }

    [Fact]
    public async Task LoadAllAsync_loads_one_catalog()
    {
        var path = Path.GetFullPath(Path.Combine(Fixtures.FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json"));

        var catalogs = await RuleCatalogLoader.LoadAllAsync([path], new ReadOnlyFileSystem(), CancellationToken.None);

        Assert.Equal("xna-to-monogame", Assert.Single(catalogs).Name);
    }
}
