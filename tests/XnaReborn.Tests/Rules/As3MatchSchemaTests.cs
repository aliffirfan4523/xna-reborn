using System.Text.Json;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Tests.Rules;

/// <summary>Flash task 6.1: the four AS3 match types load, validate and round trip; bad forms are rejected with a path.</summary>
[Trait("Category", "Rules")]
public sealed class As3MatchSchemaTests
{
    private static string Catalog(string id, string match) =>
        $$"""
        {"version":1,"name":"t","appliesTo":"flash","rules":[{"id":"{{id}}","title":"t","severity":"Info","classification":"Manual",
         "match":{{match}},"explanation":"e","resolution":"r"}]}
        """;

    [Theory]
    [InlineData("""{"type":"as3Import","pattern":"flash.display.*"}""", typeof(As3ImportMatch))]
    [InlineData("""{"type":"as3Syntax","construct":"with"}""", typeof(As3SyntaxMatch))]
    [InlineData("""{"type":"as3Metadata","tag":"Embed","embedKind":"image"}""", typeof(As3MetadataMatch))]
    [InlineData("""{"type":"as3Api","names":["addChild","stage"]}""", typeof(As3ApiMatch))]
    [InlineData("""{"type":"as3Api","qualified":"FlxG.stage"}""", typeof(As3ApiMatch))]
    public void Valid_forms_load(string match, Type expected)
    {
        var catalog = RuleCatalogLoader.Parse(Catalog("FL040", match));

        Assert.IsType(expected, Assert.Single(catalog.Rules).Match);
        Assert.Equal("flash", catalog.AppliesTo);
    }

    [Theory]
    [InlineData("""{"type":"as3Import"}""", "/rules/0/match")]
    [InlineData("""{"type":"as3Syntax","construct":"with","extra":1}""", "/rules/0/match/extra")]
    [InlineData("""{"type":"as3Metadata","tag":"Embed","embedKind":"video"}""", "/rules/0/match/embedKind")]
    [InlineData("""{"type":"as3Api"}""", "/rules/0/match")]
    [InlineData("""{"type":"as3Api","names":["a"],"qualified":"B.c"}""", "/rules/0/match")]
    public void Invalid_forms_are_rejected_with_a_path(string match, string path)
    {
        var exception = Assert.Throws<RuleCatalogException>(() => RuleCatalogLoader.Parse(Catalog("FL040", match)));

        Assert.Contains(exception.Errors, e => e.Contains(path, StringComparison.Ordinal));
    }

    [Fact]
    public void Matches_round_trip_through_json()
    {
        var original = new RuleMatch[]
        {
            new As3ImportMatch("org.flixel.*"),
            new As3SyntaxMatch("e4x"),
            new As3MetadataMatch("Embed", "audio"),
            new As3MetadataMatch("SWF"),
            new As3ApiMatch(Names: ["a", "b"]),
            new As3ApiMatch(Qualified: "FlxG.stage"),
        };

        foreach (var match in original)
        {
            var json = JsonSerializer.Serialize(match, RuleCatalogJson.Options);
            var back = JsonSerializer.Deserialize<RuleMatch>(json, RuleCatalogJson.Options);
            Assert.Equal(json, JsonSerializer.Serialize(back, RuleCatalogJson.Options));
            Assert.IsType(match.GetType(), back);
        }
    }

    [Fact]
    public void Fl_ids_are_accepted_and_other_prefixes_are_not()
    {
        Assert.NotNull(RuleCatalogLoader.Parse(Catalog("FL060", """{"type":"as3Syntax","construct":"with"}""")));
        Assert.Throws<RuleCatalogException>(() => RuleCatalogLoader.Parse(Catalog("FX060", """{"type":"as3Syntax","construct":"with"}""")));
    }
}
