using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Rules;

/// <summary>The <c>nonStandardPair</c> flag of the contentItem match: schema, loading and matching.</summary>
[Trait("Category", "Rules")]
public sealed class ContentItemNonStandardTests
{
    private static Rule Rule(RuleMatch match) =>
        new("XNA900", "Title", Severity.Warning, Classification.Manual, null, null, match, "why", "how", null);

    private static ProjectReadResult Project(params ContentItem[] content)
    {
        var info = new ProjectInfo("Game.csproj", "Game", true, XnaVersion.V40, XnaPlatform.Windows, null, null, [], [], [], [], content);
        return new ProjectReadResult(info, new Dictionary<string, string>(), [], []);
    }

    private static ContentItem Item(string path, string? importer, string? processor) =>
        new(path, null, importer, processor, new Dictionary<string, string>());

    private static IReadOnlyList<string> Hits(ContentItemMatch match, ProjectReadResult project) =>
        new RuleEngine(new RuleCatalog(1, "t", null, [Rule(match)]), new ReadOnlyFileSystem())
            .EvaluateProject(project, "x", "p", CancellationToken.None)
            .SelectMany(f => f.Snippet!.Split(", "))
            .ToList();

    private static string Catalog(string match) =>
        $$"""{"version":1,"name":"t","rules":[{"id":"XNA001","title":"t","severity":"Warning","classification":"Manual","match":{{match}},"explanation":"e","resolution":"r"}]}""";

    /// <summary>A rule with only nonStandardPair is valid, and the flag reaches the model.</summary>
    [Theory]
    [InlineData("""{"type":"contentItem","nonStandardPair":true}""", true)]
    [InlineData("""{"type":"contentItem","nonStandardPair":false,"extension":".png"}""", false)]
    [InlineData("""{"type":"contentItem","importer":"TextureImporter","nonStandardPair":true}""", true)]
    public void Loader_AcceptsNonStandardPair(string match, bool expected)
    {
        var rule = Assert.Single(RuleCatalogLoader.Parse(Catalog(match)).Rules);

        Assert.Equal(expected, Assert.IsType<ContentItemMatch>(rule.Match).NonStandardPair);
    }

    /// <summary>A non-boolean flag, an unknown property, or a match with no field is rejected.</summary>
    /// <param name="match">The match object as JSON.</param>
    [Theory]
    [InlineData("""{"type":"contentItem","nonStandardPair":"yes"}""")]
    [InlineData("""{"type":"contentItem","nonStandardPair":1}""")]
    [InlineData("""{"type":"contentItem","nonStandardPair":null}""")]
    [InlineData("""{"type":"contentItem","nonStandard":true}""")]
    [InlineData("""{"type":"contentItem"}""")]
    public void Loader_RejectsBadFlag(string match)
    {
        var exception = Assert.Throws<RuleCatalogException>(() => RuleCatalogLoader.Parse(Catalog(match)));

        Assert.Contains(exception.Errors, e => e.StartsWith("XNA001 /rules/0/match", StringComparison.Ordinal));
    }

    /// <summary>The validator message for an empty contentItem names the new flag.</summary>
    [Fact]
    public void Loader_EmptyContentItem_MentionsNonStandardPair()
    {
        var exception = Assert.Throws<RuleCatalogException>(() => RuleCatalogLoader.Parse(Catalog("""{"type":"contentItem"}""")));

        Assert.Contains(exception.Errors, e => e.Contains("nonStandardPair", StringComparison.Ordinal));
    }

    /// <summary>Only items with a custom importer or processor match; items with no names never do.</summary>
    [Fact]
    public void Matcher_FlagsOnlyCustomItems()
    {
        var project = Project(
            Item("a.png", "TextureImporter", "TextureProcessor"),
            Item("b.png", "TextureImporter", "OutlineProcessor"),
            Item("c.dat", "MyImporter", "PassThroughProcessor"),
            Item("d.xap", "XactImporter", "XactProcessor"),
            Item("e.fx", "EffectImporter", "EffectProcessor"),
            Item("f.png", null, null),
            Item("g.png", "textureimporter", "texturEPROCESSOR"),
            Item("h.png", null, "OutlineProcessor"),
            Item("i.png", "TextureImporter", null));

        Assert.Equal(["b.png", "c.dat", "h.png"], Hits(new ContentItemMatch(NonStandardPair: true), project));
    }

    /// <summary>Without the flag, custom pairs do not change what matches.</summary>
    [Fact]
    public void Matcher_WithoutFlag_IgnoresCustomness()
    {
        var project = Project(Item("a.png", "TextureImporter", "TextureProcessor"), Item("b.png", "TextureImporter", "OutlineProcessor"));

        Assert.Equal(["a.png", "b.png"], Hits(new ContentItemMatch(".png"), project));
        Assert.Equal(["a.png", "b.png"], Hits(new ContentItemMatch(NonStandardPair: false), project));
    }

    /// <summary>The other fields still have to match along with the flag.</summary>
    [Fact]
    public void Matcher_FlagCombinesWithOtherFields()
    {
        var project = Project(Item("b.png", "TextureImporter", "OutlineProcessor"), Item("c.dat", "MyImporter", "Other"));

        Assert.Equal(["b.png"], Hits(new ContentItemMatch(".png", NonStandardPair: true), project));
        Assert.Equal(["c.dat"], Hits(new ContentItemMatch(Importer: "My*", NonStandardPair: true), project));
        Assert.Empty(Hits(new ContentItemMatch(".png", Processor: "Texture*", NonStandardPair: true), project));
        Assert.Empty(Hits(new ContentItemMatch(".xap", NonStandardPair: true), project));
    }

    /// <summary>A project with only stock pairs gives no match.</summary>
    [Fact]
    public void Matcher_NoContent_NoMatch()
    {
        Assert.Empty(Hits(new ContentItemMatch(NonStandardPair: true), Project()));
        Assert.Empty(Hits(new ContentItemMatch(NonStandardPair: true), Project(Item("a.png", "TextureImporter", "TextureProcessor"))));
    }

    /// <summary>In the custom-content fixture only ship.png (OutlineProcessor) is custom.</summary>
    [Fact]
    public async Task Fixture_CustomContent_FlagsOnlyShip()
    {
        var root = FixturePaths.Of("xna40-custom-content");
        var project = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(root, "CustomContent.csproj"), root, CancellationToken.None);

        var hits = Hits(new ContentItemMatch(NonStandardPair: true), project);

        var hit = Assert.Single(hits);
        Assert.EndsWith("ship.png", hit, StringComparison.Ordinal);
    }

    /// <summary>The basic fixture has only stock pairs.</summary>
    [Fact]
    public async Task Fixture_Basic_HasNoCustomItems()
    {
        var root = FixturePaths.Of("xna40-basic");
        var project = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(root, "Xna40Basic.csproj"), root, CancellationToken.None);

        Assert.Empty(Hits(new ContentItemMatch(NonStandardPair: true), project));
    }
}
