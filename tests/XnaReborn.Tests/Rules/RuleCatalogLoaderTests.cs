using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Rules;

/// <summary>Checks that rule catalogs load when valid and that every problem in an invalid one is reported.</summary>
[Trait("Category", "Rules")]
public sealed class RuleCatalogLoaderTests
{
    private const string _validRule = """
        {"id":"XNA001","title":"t","severity":"Info","classification":"Manual",
         "match":{"type":"reference","name":"Microsoft.Xna.Framework"},"explanation":"e","resolution":"r"}
        """;

    private static string Catalog(params string[] rules) =>
        $$"""{"version":1,"name":"test","rules":[{{string.Join(",", rules)}}]}""";

    private static string Rule(string id, string match, string extra = "") =>
        $$"""
        {"id":"{{id}}","title":"t","severity":"Warning","classification":"Manual",
         "match":{{match}},"explanation":"e","resolution":"r"{{extra}}}
        """;

    private static RuleCatalogException Fails(string json) =>
        Assert.Throws<RuleCatalogException>(() => RuleCatalogLoader.Parse(json));

    /// <summary>An empty rules array is a valid catalog.</summary>
    [Fact]
    public void Parse_EmptyCatalog_Loads()
    {
        var catalog = RuleCatalogLoader.Parse(Catalog());

        Assert.Equal(1, catalog.Version);
        Assert.Equal("test", catalog.Name);
        Assert.Empty(catalog.Rules);
        Assert.Null(catalog.Scoring);
    }

    /// <summary>The catalog shipped in the repository loads.</summary>
    [Fact]
    public async Task LoadAsync_RealCatalog_Loads()
    {
        var path = Path.Combine(FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json");

        var catalog = await RuleCatalogLoader.LoadAsync(Path.GetFullPath(path), new ReadOnlyFileSystem(), CancellationToken.None);

        Assert.Equal("xna-to-monogame", catalog.Name);
        Assert.NotNull(catalog.Scoring);
        Assert.Equal(20, catalog.Scoring!.SeverityWeights[Severity.Blocker]);
    }

    /// <summary>Every match type has a valid rule that parses to the right record.</summary>
    /// <param name="match">The match object as JSON.</param>
    /// <param name="expected">The expected match record type.</param>
    [Theory]
    [InlineData("""{"type":"reference","name":"Microsoft.Xna.*"}""", typeof(ReferenceMatch))]
    [InlineData("""{"type":"namespace","namespace":"Microsoft.Xna.Framework","includeChildren":false}""", typeof(NamespaceMatch))]
    [InlineData("""{"type":"type","name":"Game","namespaces":["Microsoft.Xna.Framework"]}""", typeof(TypeMatch))]
    [InlineData("""{"type":"member","name":"Draw","receiver":"spriteBatch","namespaces":["Microsoft.Xna.Framework.Graphics"],"kind":"Invocation","arguments":{"min":1,"max":2}}""", typeof(MemberMatch))]
    [InlineData("""{"type":"projectProperty","name":"XnaFrameworkVersion","matches":"^v3\\."}""", typeof(ProjectPropertyMatch))]
    [InlineData("""{"type":"projectItem","itemType":"Import","include":"*XNA Game Studio*"}""", typeof(ProjectItemMatch))]
    [InlineData("""{"type":"filePattern","glob":"**/*.xap"}""", typeof(FilePatternMatch))]
    [InlineData("""{"type":"contentItem","processor":"Outline*"}""", typeof(ContentItemMatch))]
    [InlineData("""{"type":"spriteFontFamily","families":["Pericles","Pescadero"]}""", typeof(SpriteFontFamilyMatch))]
    public void Parse_EachMatchType_Loads(string match, Type expected)
    {
        var catalog = RuleCatalogLoader.Parse(Catalog(Rule("XNA010", match)));

        Assert.IsType(expected, Assert.Single(catalog.Rules).Match);
    }

    /// <summary>An AutoFixable rule with a migrator task is valid, and the optional fields are read.</summary>
    [Fact]
    public void Parse_AutoFixableWithTask_Loads()
    {
        var json = Catalog("""
            {"id":"XNA020","title":"t","severity":"Error","classification":"AutoFixable","category":"Api","xnaVersions":["3.1","4.0"],
             "match":{"type":"reference","name":"x"},"explanation":"e","resolution":"r","migratorTask":"project-migrator:6.2"}
            """);

        var rule = Assert.Single(RuleCatalogLoader.Parse(json).Rules);

        Assert.Equal(Classification.AutoFixable, rule.Classification);
        Assert.Equal(RuleCategory.Api, rule.Category);
        Assert.Equal([XnaVersion.V31, XnaVersion.V40], rule.XnaVersions);
        Assert.Equal("project-migrator:6.2", rule.MigratorTask);
    }

    /// <summary>Each schema violation is rejected and the error line names the rule id and the JSON path.</summary>
    /// <param name="rule">The rule object as JSON.</param>
    /// <param name="expectedPrefix">The start of the expected error line.</param>
    [Theory]
    [InlineData("""{"id":"XNA001","title":"t","severity":"Info","classification":"Manual","match":{"type":"reference","name":"x"},"resolution":"r"}""", "XNA001 /rules/0:")]
    [InlineData("""{"id":"XNA1","title":"t","severity":"Info","classification":"Manual","match":{"type":"reference","name":"x"},"explanation":"e","resolution":"r"}""", "rule #0 /rules/0/id:")]
    [InlineData("""{"id":"XNA001","title":"t","severity":"Fatal","classification":"Manual","match":{"type":"reference","name":"x"},"explanation":"e","resolution":"r"}""", "XNA001 /rules/0/severity:")]
    [InlineData("""{"id":"XNA001","title":"t","severity":"Info","classification":"Manual","match":{"type":"bogus"},"explanation":"e","resolution":"r"}""", "XNA001 /rules/0/match/type:")]
    [InlineData("""{"id":"XNA001","title":"t","severity":"Info","classification":"Manual","match":{"type":"reference","name":"x"},"explanation":"e","resolution":"r","surprise":true}""", "XNA001 /rules/0/surprise:")]
    [InlineData("""{"id":"XNA001","title":"t","severity":"Info","classification":"AutoFixable","match":{"type":"reference","name":"x"},"explanation":"e","resolution":"r"}""", "XNA001 /rules/0:")]
    [InlineData("""{"id":"XNA001","title":"t","severity":"Info","classification":"Manual","match":{"type":"reference","name":"x","other":1},"explanation":"e","resolution":"r"}""", "XNA001 /rules/0/match/other:")]
    [InlineData("""{"id":"XNA001","title":"t","severity":"Info","classification":"Manual","match":{"type":"contentItem"},"explanation":"e","resolution":"r"}""", "XNA001 /rules/0/match:")]
    public void Parse_SchemaViolation_ReportsRuleAndPath(string rule, string expectedPrefix)
    {
        var exception = Fails(Catalog(rule));

        Assert.Contains(exception.Errors, e => e.StartsWith(expectedPrefix, StringComparison.Ordinal));
        Assert.Equal($"Rule catalog is invalid: {exception.Errors.Count} problem(s).", exception.Message);
    }

    /// <summary>A spriteFontFamily match needs a non-empty, unique list of non-empty names and allows no other property.</summary>
    /// <param name="match">The match object as JSON.</param>
    [Theory]
    [InlineData("""{"type":"spriteFontFamily"}""")]
    [InlineData("""{"type":"spriteFontFamily","families":[]}""")]
    [InlineData("""{"type":"spriteFontFamily","families":[""]}""")]
    [InlineData("""{"type":"spriteFontFamily","families":["A","A"]}""")]
    [InlineData("""{"type":"spriteFontFamily","families":["A"],"extra":1}""")]
    public void Parse_InvalidSpriteFontFamily_Fails(string match)
    {
        var exception = Fails(Catalog(Rule("XNA010", match)));

        Assert.Contains(exception.Errors, e => e.StartsWith("XNA010 /rules/0", StringComparison.Ordinal));
    }

    /// <summary>The message for a missing property says which property.</summary>
    [Fact]
    public void Parse_MissingExplanation_NamesTheProperty()
    {
        var exception = Fails(Catalog("""{"id":"XNA001","title":"t","severity":"Info","classification":"Manual","match":{"type":"reference","name":"x"},"resolution":"r"}"""));

        Assert.Contains(exception.Errors, e => e.Contains("explanation", StringComparison.Ordinal));
    }

    /// <summary>The same id twice is rejected.</summary>
    [Fact]
    public void Parse_DuplicateIds_Fails()
    {
        var exception = Fails(Catalog(_validRule, _validRule));

        var line = Assert.Single(exception.Errors);
        Assert.StartsWith("XNA001 /rules/1/id:", line, StringComparison.Ordinal);
        Assert.Contains("duplicate", line, StringComparison.Ordinal);
    }

    /// <summary>A projectProperty pattern that is not a regex is rejected.</summary>
    [Fact]
    public void Parse_InvalidRegex_Fails()
    {
        var exception = Fails(Catalog(Rule("XNA030", """{"type":"projectProperty","name":"p","matches":"(["}""")));

        var line = Assert.Single(exception.Errors);
        Assert.StartsWith("XNA030 /rules/0/match/matches:", line, StringComparison.Ordinal);
    }

    /// <summary>A valid regex is accepted.</summary>
    [Fact]
    public void Parse_ValidRegex_Loads()
    {
        var catalog = RuleCatalogLoader.Parse(Catalog(Rule("XNA030", """{"type":"projectProperty","name":"p","matches":"^v[34]\\.\\d$"}""")));

        Assert.Single(catalog.Rules);
    }

    /// <summary>An argument range with min above max is rejected, while equal bounds are accepted.</summary>
    [Fact]
    public void Parse_ArgumentRange_ChecksOrder()
    {
        const string template = """{"type":"member","name":"m","namespaces":["A"],"arguments":{"min":MIN,"max":MAX}}""";

        var exception = Fails(Catalog(Rule("XNA031", template.Replace("MIN", "3").Replace("MAX", "1"))));
        var ok = RuleCatalogLoader.Parse(Catalog(Rule("XNA031", template.Replace("MIN", "2").Replace("MAX", "2"))));

        var line = Assert.Single(exception.Errors);
        Assert.StartsWith("XNA031 /rules/0/match/arguments:", line, StringComparison.Ordinal);
        Assert.Single(ok.Rules);
    }

    /// <summary>Text that is not JSON gives one clear error.</summary>
    /// <param name="json">The malformed text.</param>
    [Theory]
    [InlineData("{ nope")]
    [InlineData("")]
    public void Parse_MalformedJson_Fails(string json)
    {
        var exception = Fails(json);

        var line = Assert.Single(exception.Errors);
        Assert.Contains("invalid JSON", line, StringComparison.Ordinal);
    }

    /// <summary>A document that is not an object is rejected.</summary>
    [Fact]
    public void Parse_RootNotObject_Fails()
    {
        Assert.NotEmpty(Fails("[]").Errors);
    }

    /// <summary>A rule without a usable id is labelled by its position.</summary>
    [Fact]
    public void Parse_MissingId_UsesRuleNumber()
    {
        var exception = Fails(Catalog(_validRule, """{"title":"t","severity":"Info","classification":"Manual","match":{"type":"reference","name":"x"},"explanation":"e","resolution":"r"}"""));

        Assert.Contains(exception.Errors, e => e.StartsWith("rule #1 /rules/1:", StringComparison.Ordinal));
    }

    /// <summary>All problems in a catalog arrive in a single exception.</summary>
    [Fact]
    public void Parse_ManyProblems_CollectedTogether()
    {
        var json = Catalog(
            Rule("XNA040", """{"type":"projectProperty","name":"p","matches":"(["}"""),
            Rule("XNA040", """{"type":"member","name":"m","namespaces":["A"],"arguments":{"min":5,"max":1}}"""),
            """{"id":"XNA042","title":"t","severity":"Nope","classification":"Manual","match":{"type":"reference","name":"x"},"explanation":"e","resolution":"r"}""");

        var exception = Fails(json);

        Assert.Equal(4, exception.Errors.Count);
        Assert.Contains(exception.Errors, e => e.StartsWith("XNA040 /rules/0/match/matches:", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, e => e.StartsWith("XNA040 /rules/1/id:", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, e => e.StartsWith("XNA040 /rules/1/match/arguments:", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, e => e.StartsWith("XNA042 /rules/2/severity:", StringComparison.Ordinal));
        Assert.Equal("Rule catalog is invalid: 4 problem(s).", exception.Message);
    }

    /// <summary>A missing file raises a catalog exception that names the path.</summary>
    [Fact]
    public async Task LoadAsync_MissingFile_Fails()
    {
        var path = Path.Combine(Path.GetTempPath(), "no-such-catalog-" + Guid.NewGuid().ToString("N") + ".json");

        var exception = await Assert.ThrowsAsync<RuleCatalogException>(
            () => RuleCatalogLoader.LoadAsync(path, new ReadOnlyFileSystem(), CancellationToken.None));

        Assert.Contains(path, exception.Message, StringComparison.Ordinal);
        Assert.Single(exception.Errors);
    }

    /// <summary>A cancelled token stops the load.</summary>
    [Fact]
    public async Task LoadAsync_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => RuleCatalogLoader.LoadAsync("anything.json", new ReadOnlyFileSystem(), cts.Token));
    }
}
