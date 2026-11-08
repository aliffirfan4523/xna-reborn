using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Analyzer.Source;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Source;

/// <summary>Runs the real reader, scanner, and matcher together on the committed fixtures.</summary>
[Trait("Category", "Rules")]
public class SourceScanEndToEndTests
{
    private static readonly string[] _graphics = ["Microsoft.Xna.Framework.Graphics"];

    private static Rule MakeRule(string id, RuleMatch match) =>
        new(id, "Test " + id, Severity.Error, Classification.Manual, null, null, match, "e", "r", null);

    private static async Task<IReadOnlyList<Finding>> ScanAsync(string fixture, string projectFile, params Rule[] rules)
    {
        var root = FixturePaths.Of(fixture);
        var fileSystem = new ReadOnlyFileSystem();
        var project = await new ProjectReader(fileSystem).ReadAsync(Path.Combine(root, projectFile), root, CancellationToken.None);
        var result = await new SourceScanner(fileSystem).ScanAsync(project, root, root, projectFile, rules, null, CancellationToken.None);

        Assert.Empty(result.Diagnostics);
        return result.Findings;
    }

    private static int LineOf(string fixture, string file, string text)
    {
        var lines = File.ReadAllLines(Path.Combine(FixturePaths.Of(fixture), file));
        return Array.FindIndex(lines, l => l.Contains(text, StringComparison.Ordinal)) + 1;
    }

    [Fact]
    public async Task Namespace_rule_finds_the_gamerservices_using_directive()
    {
        var rule = MakeRule("XNA901", new NamespaceMatch("Microsoft.Xna.Framework.GamerServices"));

        var findings = await ScanAsync("xna40-gamerservices", "Xna40GamerServices.csproj", rule);

        var first = findings.First(f => f.Location!.Line == LineOf("xna40-gamerservices", "Game1.cs", "using Microsoft.Xna.Framework.GamerServices;"));
        Assert.Equal("Game1.cs", first.Location!.File);
        Assert.Equal("Xna40GamerServices.csproj", first.Project);
        Assert.Equal("using Microsoft.Xna.Framework.GamerServices;", first.Snippet);
    }

    [Fact]
    public async Task Namespace_rule_finds_nothing_in_the_basic_fixture()
    {
        var rule = MakeRule("XNA901", new NamespaceMatch("Microsoft.Xna.Framework.GamerServices"));

        Assert.Empty(await ScanAsync("xna40-basic", "Xna40Basic.csproj", rule));
    }

    [Fact]
    public async Task Type_rule_finds_spriteblendmode_in_the_legacy_fixture_only()
    {
        var rule = MakeRule("XNA902", new TypeMatch("SpriteBlendMode", _graphics));

        var legacy = await ScanAsync("xna31-legacy", "Xna31Legacy.csproj", rule);
        var basic = await ScanAsync("xna40-basic", "Xna40Basic.csproj", rule);

        Assert.NotEmpty(legacy);
        Assert.All(legacy, f => Assert.Equal("Game1.cs", f.Location!.File));
        Assert.Contains(legacy, f => f.Location!.Line == LineOf("xna31-legacy", "Game1.cs", "spriteBatch.Begin(SpriteBlendMode.AlphaBlend);"));
        Assert.Empty(basic);
    }

    [Fact]
    public async Task Member_rule_with_receiver_and_argument_count_finds_resolvebackbuffer()
    {
        var rule = MakeRule("XNA903", new MemberMatch("ResolveBackBuffer", _graphics, "GraphicsDevice", MemberMatchKind.Invocation, new ArgumentRange(1, 1)));

        var findings = await ScanAsync("xna31-legacy", "Xna31Legacy.csproj", rule);

        var finding = Assert.Single(findings);
        Assert.Equal(LineOf("xna31-legacy", "Game1.cs", "GraphicsDevice.ResolveBackBuffer(snapshot);"), finding.Location!.Line);
    }

    [Theory]
    [InlineData("device", 1, 1)]
    [InlineData("GraphicsDevice", 2, 3)]
    public async Task Member_rule_with_wrong_receiver_or_argument_count_finds_nothing(string receiver, int min, int max)
    {
        var rule = MakeRule("XNA903", new MemberMatch("ResolveBackBuffer", _graphics, receiver, MemberMatchKind.Invocation, new ArgumentRange(min, max)));

        Assert.Empty(await ScanAsync("xna31-legacy", "Xna31Legacy.csproj", rule));
    }

    [Fact]
    public async Task Member_rule_finds_the_static_texture_loader()
    {
        var rule = MakeRule("XNA904", new MemberMatch("FromFile", _graphics, "Texture2D", MemberMatchKind.Invocation));

        var finding = Assert.Single(await ScanAsync("xna31-legacy", "Xna31Legacy.csproj", rule));

        Assert.Equal(LineOf("xna31-legacy", "Game1.cs", "Texture2D.FromFile("), finding.Location!.Line);
    }

    [Fact]
    public async Task Repeated_scans_give_identical_findings()
    {
        var rules = new[]
        {
            MakeRule("XNA902", new TypeMatch("SpriteBlendMode", _graphics)),
            MakeRule("XNA903", new MemberMatch("ResolveBackBuffer", _graphics)),
        };

        var first = await ScanAsync("xna31-legacy", "Xna31Legacy.csproj", rules);
        var second = await ScanAsync("xna31-legacy", "Xna31Legacy.csproj", rules);

        Assert.Equal(first, second);
    }
}
