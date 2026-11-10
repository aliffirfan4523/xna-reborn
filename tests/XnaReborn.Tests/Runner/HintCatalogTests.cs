using System.Text.Json;
using XnaReborn.Runner.Hints;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Runner;

[Trait("Category", "Runner")]
public class HintCatalogTests
{
    private static string Hint(string id, string match, string extra = "") =>
        $$"""{ "id": "{{id}}", "match": {{match}}, "title": "T", "advice": "A"{{extra}} }""";

    private static string Catalog(params string[] hints) =>
        $$"""{ "version": 1, "hints": [ {{string.Join(",", hints)}} ] }""";

    [Fact]
    public void Embedded_catalog_loads_and_has_build_and_runtime_hints()
    {
        var catalog = HintCatalog.Default;

        Assert.True(catalog.Entries.Count(e => !e.Match.IsRuntime) >= 10);
        Assert.True(catalog.Entries.Count(e => e.Match.IsRuntime) >= 5);
        Assert.Equal(catalog.Entries.Count, catalog.Entries.Select(e => e.Hint.Id).Distinct().Count());
    }

    [Fact]
    public void Every_rule_id_in_the_catalog_exists_in_the_real_rule_catalog()
    {
        var rulesPath = Path.Combine(FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(rulesPath));
        var ruleIds = doc.RootElement.GetProperty("rules").EnumerateArray()
            .Select(r => r.GetProperty("id").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var linked = HintCatalog.Default.Entries.Where(e => e.Hint.RuleId is not null).ToList();

        Assert.NotEmpty(linked);
        foreach (var e in linked)
        {
            Assert.True(ruleIds.Contains(e.Hint.RuleId!), $"{e.Hint.Id} points to unknown rule {e.Hint.RuleId}");
        }
    }

    [Fact]
    public void Every_hint_has_a_title_and_advice()
    {
        foreach (var e in HintCatalog.Default.Entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Hint.Title), e.Hint.Id);
            Assert.False(string.IsNullOrWhiteSpace(e.Hint.Advice), e.Hint.Id);
        }
    }

    [Fact]
    public void Parse_reads_all_fields()
    {
        var catalog = HintCatalog.Parse(Catalog(
            Hint("H1", """{ "code": "CS1", "messageContains": "x", "messageRegex": "y+" }""", ", \"ruleId\": \"XNA001\"")));

        var entry = Assert.Single(catalog.Entries);
        Assert.Equal(new FixHint("H1", "T", "A", "XNA001"), entry.Hint);
        Assert.Equal("CS1", entry.Match.Code);
        Assert.Equal("x", entry.Match.MessageContains);
        Assert.Equal("y+", entry.Match.MessageRegex);
        Assert.False(entry.Match.IsRuntime);
    }

    [Fact]
    public void Parse_keeps_catalog_order()
    {
        var catalog = HintCatalog.Parse(Catalog(
            Hint("B", """{ "code": "CS2" }"""),
            Hint("A", """{ "code": "CS1" }""")));

        Assert.Equal(["B", "A"], catalog.Entries.Select(e => e.Hint.Id));
    }

    public static IEnumerable<object[]> InvalidCatalogs() =>
    [
        ["not json at all", "not valid JSON"],
        ["[]", "must be a JSON object"],
        ["""{ "hints": [] }""", "numeric \"version\""],
        ["""{ "version": 2, "hints": [] }""", "version is 2"],
        ["""{ "version": 1 }""", "\"hints\" array"],
        [Catalog(Hint("H1", """{ "code": "A" }"""), Hint("h1", """{ "code": "B" }""")), "h1 is used more than once"],
        [Catalog(Hint("H1", "{ }")), "no match field"],
        [Catalog(Hint("H1", """{ "code": "  " }""")), "no match field"],
        [Catalog(Hint("H1", """{ "code": "A", "runtimeContains": "x" }""")), "mixes build fields"],
        [Catalog(Hint("H1", """{ "messageRegex": "a", "runtimeRegex": "b" }""")), "mixes build fields"],
        [Catalog(Hint("H1", """{ "messageRegex": "(unclosed" }""")), "not a valid regular expression"],
        [Catalog(Hint("H1", """{ "runtimeRegex": "[" }""")), "runtimeRegex"],
        [Catalog(Hint("H1", """{ "codee": "A" }""")), "unknown match field \"codee\""],
        [Catalog(Hint("H1", """{ "code": 5 }""")), "\"code\" must be a string"],
        ["""{ "version": 1, "hints": [ { "id": "H1", "title": "T", "advice": "A" } ] }""", "needs a \"match\" object"],
        ["""{ "version": 1, "hints": [ { "match": { "code": "A" }, "title": "T", "advice": "A" } ] }""", "needs a non-empty \"id\""],
        ["""{ "version": 1, "hints": [ { "id": "H1", "match": { "code": "A" }, "advice": "A" } ] }""", "Hint H1 needs a non-empty \"title\""],
        ["""{ "version": 1, "hints": [ { "id": "H1", "match": { "code": "A" }, "title": "T" } ] }""", "Hint H1 needs a non-empty \"advice\""],
        ["""{ "version": 1, "hints": [ 5 ] }""", "Hint number 1 must be a JSON object"],
    ];

    [Theory]
    [MemberData(nameof(InvalidCatalogs))]
    public void Parse_rejects_invalid_catalogs_with_a_clear_message(string json, string expected)
    {
        var ex = Assert.Throws<HintCatalogException>(() => HintCatalog.Parse(json));

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Regexes_get_a_one_second_timeout()
    {
        var catalog = HintCatalog.Parse(Catalog(Hint("H1", """{ "messageRegex": "a+" }""")));

        Assert.Equal(TimeSpan.FromSeconds(1), catalog.Entries[0].MessageRegex!.MatchTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1), HintCatalog.RegexTimeout);
    }

    [Fact]
    public void Load_reads_a_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "hints-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, Catalog(Hint("H1", """{ "code": "A" }""")));
        try
        {
            Assert.Single(HintCatalog.Load(path).Entries);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
