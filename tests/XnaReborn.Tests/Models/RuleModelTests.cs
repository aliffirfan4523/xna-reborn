using System.Text.Json;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Tests.Models;

public class RuleModelTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "XnaReborn.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("XnaReborn.sln not found above " + AppContext.BaseDirectory);
    }

    private static Rule ParseRule(string matchJson)
    {
        var json = $$"""
            {"id":"XNA001","title":"t","severity":"Error","classification":"Manual","match":{{matchJson}},"explanation":"e","resolution":"r"}
            """;
        return JsonSerializer.Deserialize<Rule>(json, RuleCatalogJson.Options)!;
    }

    [Fact]
    public void RealCatalogDeserializes()
    {
        var path = Path.Combine(FindRepoRoot(), "rules", "xna-to-monogame.json");
        var catalog = JsonSerializer.Deserialize<RuleCatalog>(File.ReadAllText(path), RuleCatalogJson.Options)!;

        Assert.Equal(1, catalog.Version);
        Assert.Equal("xna-to-monogame", catalog.Name);
        Assert.Empty(catalog.Rules);
        Assert.NotNull(catalog.Scoring);
        Assert.Equal(ScoringOptions.Default.SeverityWeights.OrderBy(p => p.Key), catalog.Scoring.SeverityWeights.OrderBy(p => p.Key));
        Assert.Equal(ScoringOptions.Default.ClassificationFactors.OrderBy(p => p.Key), catalog.Scoring.ClassificationFactors.OrderBy(p => p.Key));
    }

    [Fact]
    public void ReferenceMatch()
    {
        var m = Assert.IsType<ReferenceMatch>(ParseRule("""{"name":"Microsoft.Xna.*","type":"reference"}""").Match);
        Assert.Equal("Microsoft.Xna.*", m.Name);
    }

    [Fact]
    public void NamespaceMatch()
    {
        var m = Assert.IsType<NamespaceMatch>(ParseRule("""{"type":"namespace","namespace":"Microsoft.Xna.Framework"}""").Match);
        Assert.Equal("Microsoft.Xna.Framework", m.Namespace);
        Assert.True(m.IncludeChildren);
        var off = Assert.IsType<NamespaceMatch>(ParseRule("""{"type":"namespace","namespace":"A","includeChildren":false}""").Match);
        Assert.False(off.IncludeChildren);
    }

    [Fact]
    public void TypeMatch()
    {
        var m = Assert.IsType<TypeMatch>(ParseRule("""{"namespaces":["A.B","C"],"name":"Foo","type":"type"}""").Match);
        Assert.Equal("Foo", m.Name);
        Assert.Equal(["A.B", "C"], m.Namespaces);
    }

    [Fact]
    public void MemberMatch()
    {
        var m = Assert.IsType<MemberMatch>(ParseRule(
            """{"name":"Resolve","receiver":"g","namespaces":["A"],"kind":"Invocation","arguments":{"min":1,"max":2},"type":"member"}""").Match);
        Assert.Equal("Resolve", m.Name);
        Assert.Equal("g", m.Receiver);
        Assert.Equal(["A"], m.Namespaces);
        Assert.Equal(MemberMatchKind.Invocation, m.Kind);
        Assert.Equal(new ArgumentRange(1, 2), m.Arguments);

        var d = Assert.IsType<MemberMatch>(ParseRule("""{"type":"member","name":"X","namespaces":["A"]}""").Match);
        Assert.Equal(MemberMatchKind.Any, d.Kind);
        Assert.Null(d.Receiver);
        Assert.Null(d.Arguments);
    }

    [Fact]
    public void ProjectPropertyMatch()
    {
        var m = Assert.IsType<ProjectPropertyMatch>(ParseRule("""{"name":"XnaPlatform","equals":"Xbox 360","matches":"^X","type":"projectProperty"}""").Match);
        Assert.Equal("XnaPlatform", m.Name);
        Assert.Equal("Xbox 360", m.EqualsValue);
        Assert.Equal("^X", m.MatchesPattern);
    }

    [Fact]
    public void ProjectItemMatch()
    {
        var m = Assert.IsType<ProjectItemMatch>(ParseRule("""{"include":"*.targets","itemType":"Import","type":"projectItem"}""").Match);
        Assert.Equal("Import", m.ItemType);
        Assert.Equal("*.targets", m.Include);
    }

    [Fact]
    public void FilePatternMatch()
    {
        var m = Assert.IsType<FilePatternMatch>(ParseRule("""{"glob":"**/*.xap","type":"filePattern"}""").Match);
        Assert.Equal("**/*.xap", m.Glob);
    }

    [Fact]
    public void ContentItemMatch()
    {
        var m = Assert.IsType<ContentItemMatch>(ParseRule("""{"extension":".spritefont","importer":"I*","processor":"P","type":"contentItem"}""").Match);
        Assert.Equal(".spritefont", m.Extension);
        Assert.Equal("I*", m.Importer);
        Assert.Equal("P", m.Processor);
    }

    [Fact]
    public void RulesRoundTrip()
    {
        var catalog = new RuleCatalog(
            1,
            "test",
            ScoringOptions.Default,
            [
                new Rule("XNA001", "a", Severity.Blocker, Classification.Unsupported, RuleCategory.Api, [XnaVersion.V31, XnaVersion.V40], new ReferenceMatch("X"), "e", "r", null),
                new Rule("XNA002", "b", Severity.Info, Classification.AutoFixable, null, null, new MemberMatch("M", ["N"], "r", MemberMatchKind.Access, new ArgumentRange(null, 3)), "e", "r", "project-migrator:6.2"),
                new Rule("XNA003", "c", Severity.Warning, Classification.Manual, RuleCategory.Content, null, new ProjectPropertyMatch("P", "v", null), "e", "r", null),
                new Rule("XNA004", "d", Severity.Warning, Classification.Manual, null, null, new ContentItemMatch(".x"), "e", "r", null),
            ]);

        var json = JsonSerializer.Serialize(catalog, RuleCatalogJson.Options);
        var back = JsonSerializer.Deserialize<RuleCatalog>(json, RuleCatalogJson.Options)!;

        Assert.Equal(json, JsonSerializer.Serialize(back, RuleCatalogJson.Options));
        Assert.Contains("\"type\":\"reference\"", json);
        Assert.Contains("\"severity\":\"Blocker\"", json);
        Assert.Contains("\"xnaVersions\":[\"3.1\",\"4.0\"]", json);
        Assert.Equal(catalog.Rules.Select(r => r.Id), back.Rules.Select(r => r.Id));
        Assert.Equal(new[] { XnaVersion.V31, XnaVersion.V40 }, back.Rules[0].XnaVersions);
    }

    [Fact]
    public void UnknownMatchTypeFails()
    {
        Assert.Throws<JsonException>(() => ParseRule("""{"type":"bogus","name":"x"}"""));
    }

    [Fact]
    public void XnaVersionsBind()
    {
        var json = """
            {"id":"XNA001","title":"t","severity":"Info","classification":"Manual","xnaVersions":["3.1","4.0"],
             "match":{"type":"filePattern","glob":"*"},"explanation":"e","resolution":"r"}
            """;
        var rule = JsonSerializer.Deserialize<Rule>(json, RuleCatalogJson.Options)!;
        Assert.Equal(new[] { XnaVersion.V31, XnaVersion.V40 }, rule.XnaVersions);
    }
}
