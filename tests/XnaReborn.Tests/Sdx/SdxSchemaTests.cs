using Microsoft.CodeAnalysis.CSharp;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Analyzer.Source;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Tests.Sdx;

/// <summary>Schema and matcher changes for the SharpDX Toolkit catalog (sharpdx-toolkit task 3.1 and 3.3).</summary>
[Trait("Category", "Sdx")]
public sealed class SdxSchemaTests
{
    private static string Catalog(string id, string match, string header = "") =>
        $$"""
        {"version":1,"name":"t"{{header}},"rules":[{"id":"{{id}}","title":"t","severity":"Warning","classification":"Manual",
         "match":{{match}},"explanation":"e","resolution":"r"}]}
        """;

    private const string _names = """{"type":"member","names":["RotationX","RotationY"],"receiver":"Matrix","namespaces":["SharpDX"]}""";

    private static Rule NamesRule(params string[] names) =>
        new("SDX030", "t", Severity.Warning, Classification.Manual, null, null,
            new MemberMatch(null, ["SharpDX"], "Matrix", MemberMatchKind.Any, null, names), "e", "r", null);

    private static IReadOnlyList<SourceMatch> Run(string code, params Rule[] rules) =>
        SourceRuleMatcher.Match(CSharpSyntaxTree.ParseText(code), rules);

    // ---------- 3.1 id pattern ----------
    [Fact]
    public void An_sdx_id_validates()
    {
        var catalog = RuleCatalogLoader.Parse(Catalog("SDX001", """{"type":"reference","name":"SharpDX*"}"""));

        Assert.Equal("SDX001", Assert.Single(catalog.Rules).Id);
    }

    [Theory]
    [InlineData("ABC001")]
    [InlineData("SDX01")]
    [InlineData("sdx001")]
    public void An_id_with_another_prefix_or_shape_is_rejected(string id)
    {
        var ex = Assert.Throws<RuleCatalogException>(() => RuleCatalogLoader.Parse(Catalog(id, """{"type":"reference","name":"X"}""")));

        Assert.Contains(ex.Errors, e => e.Contains("not a match", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_real_xna_catalog_still_validates()
    {
        var path = Path.Combine(Fixtures.FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json");

        var catalog = RuleCatalogLoader.Parse(File.ReadAllText(path));

        Assert.Null(catalog.AppliesTo);
        Assert.True(catalog.AppliesToFamily(ProjectFamily.Xna));
    }

    // ---------- 3.2 appliesTo ----------
    [Fact]
    public void Applies_to_selects_the_framework_and_defaults_to_xna()
    {
        var toolkit = RuleCatalogLoader.Parse(Catalog("SDX001", """{"type":"reference","name":"X"}""", ",\"appliesTo\":\"SharpDxToolkit\""));
        var plain = RuleCatalogLoader.Parse(Catalog("SDX001", """{"type":"reference","name":"X"}"""));

        Assert.True(toolkit.AppliesToFamily(ProjectFamily.SharpDxToolkit));
        Assert.False(toolkit.AppliesToFamily(ProjectFamily.Xna));
        Assert.False(toolkit.AppliesToFamily(ProjectFamily.MonoGameLegacy));
        Assert.True(plain.AppliesToFamily(ProjectFamily.Xna));
        Assert.False(plain.AppliesToFamily(ProjectFamily.SharpDxToolkit));
    }

    [Fact]
    public void Applies_to_with_an_unknown_value_is_rejected()
    {
        Assert.Throws<RuleCatalogException>(() =>
            RuleCatalogLoader.Parse(Catalog("SDX001", """{"type":"reference","name":"X"}""", ",\"appliesTo\":\"Unity\"")));
    }

    // ---------- 3.3 member names ----------
    [Fact]
    public void Member_rule_with_names_loads_and_round_trips()
    {
        var catalog = RuleCatalogLoader.Parse(Catalog("SDX030", _names));

        var match = Assert.IsType<MemberMatch>(Assert.Single(catalog.Rules).Match);
        Assert.Null(match.Name);
        Assert.Equal(["RotationX", "RotationY"], match.AllNames);
        var json = System.Text.Json.JsonSerializer.Serialize(catalog, RuleCatalogJson.Options);
        Assert.DoesNotContain("\"name\":null", json, StringComparison.Ordinal);
        Assert.Equal(["RotationX", "RotationY"], Assert.IsType<MemberMatch>(RuleCatalogLoader.Parse(json).Rules[0].Match).Names);
    }

    [Fact]
    public void A_member_rule_with_both_name_and_names_is_rejected()
    {
        var both = """{"type":"member","name":"RotationX","names":["RotationX","RotationY"],"namespaces":["SharpDX"]}""";

        var ex = Assert.Throws<RuleCatalogException>(() => RuleCatalogLoader.Parse(Catalog("SDX030", both)));

        Assert.Contains(ex.Errors, e => e.Contains("exactly one of name or names", StringComparison.Ordinal));
    }

    [Fact]
    public void A_member_rule_with_neither_name_nor_names_is_rejected()
    {
        var none = """{"type":"member","namespaces":["SharpDX"]}""";

        var ex = Assert.Throws<RuleCatalogException>(() => RuleCatalogLoader.Parse(Catalog("SDX030", none)));

        Assert.Contains(ex.Errors, e => e.Contains("exactly one of name or names", StringComparison.Ordinal));
    }

    [Fact]
    public void Names_matches_each_listed_name_once_per_position()
    {
        const string code = """
            using SharpDX;
            class C
            {
                void M()
                {
                    var a = Matrix.RotationX(1f);
                    var b = Matrix.RotationY(1f) * Matrix.RotationX(2f);
                }
            }
            """;

        var matches = Run(code, NamesRule("RotationX", "RotationY", "RotationX"));

        Assert.Equal(["6:17", "7:17", "7:40"], matches.Select(m => $"{m.Line}:{m.Column}"));
    }

    [Fact]
    public void Names_does_not_match_an_unlisted_name()
    {
        Assert.Empty(Run("using SharpDX; class C { void M() { var a = Matrix.RotationZ(1f); } }", NamesRule("RotationX", "RotationY")));
    }

    [Fact]
    public void Names_does_not_match_without_the_receiver()
    {
        const string code = "using SharpDX; class C { void M(Matrix m) { var a = m.RotationX(1f); var b = RotationY(1f); } }";

        Assert.Empty(Run(code, NamesRule("RotationX", "RotationY")));
    }

    [Fact]
    public void Names_respects_the_argument_count()
    {
        var rule = new Rule("SDX030", "t", Severity.Warning, Classification.Manual, null, null,
            new MemberMatch(null, ["SharpDX"], "Matrix", MemberMatchKind.Invocation, new ArgumentRange(1, 1), ["RotationX", "RotationY"]), "e", "r", null);

        Assert.Single(Run("using SharpDX; class C { void M() { Matrix.RotationX(1f); } }", rule));
        Assert.Empty(Run("using SharpDX; class C { void M() { Matrix.RotationX(1f, 2f); } }", rule));
    }

    [Fact]
    public void A_rule_with_name_still_matches_as_before()
    {
        var rule = new Rule("ME", "t", Severity.Warning, Classification.Manual, null, null,
            new MemberMatch("RotationX", ["SharpDX"], "Matrix"), "e", "r", null);

        Assert.Single(Run("using SharpDX; class C { void M() { Matrix.RotationX(1f); Matrix.RotationY(1f); } }", rule));
    }
}
