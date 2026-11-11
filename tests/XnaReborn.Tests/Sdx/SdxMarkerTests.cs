using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Migrator.Source;
using XnaReborn.Tests.Device;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Sdx;

/// <summary>Task 6.3 and 7: every Manual and Unsupported source rule has a marker, and the whole chain of rewriters works on the fixtures.</summary>
[Trait("Category", "Sdx")]
public sealed class SdxMarkerTests
{
    private static readonly string[] _symbols = ["DEBUG", "TRACE"];

    private static readonly Lazy<IReadOnlyList<RuleCatalog>> _catalogs = new(() =>
        RuleCatalogLoader.LoadAllAsync(
            [Path.Combine(FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json"), Path.Combine(FixturePaths.Root, "..", "..", "rules", "sharpdx-toolkit-to-monogame.json")],
            new ReadOnlyFileSystem(),
            CancellationToken.None).GetAwaiter().GetResult());

    // A positive and a negative snippet for each marker rule. The snippets keep a framework using so the file counts as Toolkit code.
    private static readonly (string Rule, string Positive, string Negative)[] _cases =
    [
        ("SDX005", "using SharpDX.Direct3D11;\nclass C { }\n", "using SharpDX.Direct3D9;\nclass C { }\n"),
        ("SDX006", "using SharpDX.DXGI;\nclass C { }\n", "using SharpDX.Direct3D9;\nclass C { }\n"),
        ("SDX007", "using SharpDX.Direct2D1;\nclass C { }\n", "using SharpDX.DirectWrite;\nclass C { }\n"),
        ("SDX008", "using SharpDX.XAudio2;\nclass C { }\n", "using SharpDX.Toolkit.Audio;\nclass C { }\n"),
        ("SDX026", "using SharpDX;\nclass C { MouseManager m; }\n", "using SharpDX;\nclass C { MouseHelper m; }\n"),
        ("SDX027", "using SharpDX;\nclass C { PointerManager p; }\n", "using SharpDX;\nclass C { PointerHelper p; }\n"),
        ("SDX028", "using SharpDX;\nclass C { bool M(S s) { return s.IsKeyPressed(1); } }\n", "using SharpDX;\nclass C { bool M(S s) { return s.IsKeyDown(1); } }\n"),
        ("SDX032", "using SharpDX;\nclass C { object M() { return Matrix.LookAtLH(a, b, c); } }\n", "using SharpDX;\nclass C { object M() { return Matrix.LookAtRH(a, b, c); } }\n"),
        ("SDX033", "using SharpDX;\nclass C { object M() { return Vector3.TransformCoordinate(a, b); } }\n", "using SharpDX;\nclass C { object M() { return Vector3.Transform(a, b); } }\n"),
        ("SDX034", "using SharpDX;\nclass C { RectangleF r; }\n", "using SharpDX;\nclass C { Rectangle r; }\n"),
        ("SDX035", "using SharpDX;\nclass C { Color4 c; }\n", "using SharpDX;\nclass C { Color c; }\n"),
        ("SDX043", "using SharpDX;\nclass C { void M() { GraphicsDevice.SetRenderTargets(d, t); } }\n", "using SharpDX;\nclass C { void M() { GraphicsDevice.SetRenderTargets(t); GraphicsDevice.SetRenderTargets(a, b, c, d, e, f, g, h, i); } }\n"),
        ("SDX045", "using SharpDX;\nclass C { object M() { return RenderTarget2D.New(d, 1, 1, f); } }\n", "using SharpDX;\nclass C { object M() { return new RenderTarget2D(d, 1, 1); } }\n"),
        ("SDX046", "using SharpDX;\nclass C { object M() { return Texture2D.New(d, 1, 1, f); } }\n", "using SharpDX;\nclass C { object M() { return new Texture2D(d, 1, 1); } }\n"),
        ("SDX047", "using SharpDX;\nclass C { Model m; }\n", "using SharpDX;\nclass C { object m; }\n"),
        ("SDX048", "using SharpDX;\nclass C { GeometricPrimitive p; }\n", "using SharpDX;\nclass C { object p; }\n"),
        ("SDX049", "using SharpDX;\nclass C { void M(P p) { p.SetResource(t); } }\n", "using SharpDX;\nclass C { void M(P p) { p.SetValue(t); } }\n"),
        ("SDX050", "using SharpDX;\nclass C { void M() { GraphicsDevice.DrawQuad(e); } }\n", "using SharpDX;\nclass C { void M() { GraphicsDevice.Draw(e); } }\n"),
        ("SDX051", "using SharpDX;\nclass C { void M() { spriteBatch.Begin(a, e); } }\n", "using SharpDX;\nclass C { void M() { spriteBatch.Begin(); spriteBatch.Begin(a, b, c, d, e, f); other.Begin(a, e); } }\n"),
        ("SDX061", "using SharpDX;\nclass C { void M(G g) { g.PreferredGraphicsProfile = null; } }\n", "using SharpDX;\nclass C { void M(G g) { g.GraphicsProfile = null; } }\n"),
        ("SDX062", "using SharpDX;\nclass C : GameSystem { }\n", "using SharpDX;\nclass C : GameComponent { }\n"),
        ("SDX063", "using SharpDX;\nclass C { SharpDXElement e; }\n", "using SharpDX;\nclass C { object e; }\n"),
    ];

    public static IEnumerable<object[]> Rows() => _cases.Select(c => new object[] { c.Rule });

    private static (string Rule, string Positive, string Negative) CaseOf(string rule) => _cases.Single(c => c.Rule == rule);

    private static SourceRewriteResult Run(string rule, string text)
    {
        DefaultRewriters.Create().TryGet(rule, out var rewriter);
        return rewriter!.Rewrite(new SourceRewriteContext("Game1.cs", text, _symbols));
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void The_positive_snippet_gets_a_marker_for_the_rule_and_the_code_is_otherwise_unchanged(string rule)
    {
        var (_, positive, _) = CaseOf(rule);

        var result = Run(rule, positive);

        var lines = result.Text.Split('\n');
        Assert.Single(lines, l => l.TrimStart().StartsWith("// XNAREBORN: " + rule + ":", StringComparison.Ordinal));
        Assert.Equal(positive, string.Join('\n', lines.Where(l => !l.TrimStart().StartsWith("// XNAREBORN:", StringComparison.Ordinal))));
        Assert.True(Assert.Single(result.Notes).NeedsManualWork);
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void The_negative_snippet_is_left_alone(string rule)
    {
        var (_, _, negative) = CaseOf(rule);

        var result = Run(rule, negative);

        Assert.Equal(negative, result.Text);
        Assert.Empty(result.Notes);
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void A_second_run_adds_no_second_marker(string rule)
    {
        var once = Run(rule, CaseOf(rule).Positive);

        Assert.Equal(once.Text, Run(rule, once.Text).Text);
    }

    [Fact]
    public void Only_the_first_use_in_a_file_is_marked()
    {
        var text = "using SharpDX;\nclass C\n{\n    RectangleF a;\n    RectangleF b;\n}\n";

        var result = Run("SDX034", text);

        Assert.Equal("using SharpDX;\nclass C\n{\n    // XNAREBORN: SDX034: MonoGame has no RectangleF. Use Rectangle, or a small struct of your own.\n    RectangleF a;\n    RectangleF b;\n}\n", result.Text);
    }

    [Fact]
    public void A_file_with_no_framework_context_is_not_marked()
    {
        Assert.Equal("class C { RectangleF r; }\n", Run("SDX034", "class C { RectangleF r; }\n").Text);
    }

    [Fact]
    public void Every_manual_and_unsupported_source_rule_of_the_catalog_has_a_marker_case_and_a_registered_rewriter()
    {
        var registry = DefaultRewriters.Create();
        var markable = _catalogs.Value[1].Rules
            .Where(r => r.Classification != Classification.AutoFixable && r.Match is NamespaceMatch or TypeMatch or MemberMatch)
            .Select(r => r.Id)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(markable, _cases.Select(c => c.Rule).Order(StringComparer.Ordinal));
        Assert.All(markable, id => Assert.True(registry.TryGet(id, out _), id + " has no rewriter"));
    }

    [Fact]
    public void Every_autofixable_source_rule_of_the_catalog_has_a_registered_rewriter()
    {
        var registry = DefaultRewriters.Create();
        var fixable = _catalogs.Value[1].Rules
            .Where(r => r.Classification == Classification.AutoFixable && r.Match is NamespaceMatch or TypeMatch or MemberMatch)
            .Select(r => r.Id);

        Assert.All(fixable, id => Assert.True(registry.TryGet(id, out _), id + " has no rewriter"));
    }

    [Theory]
    [InlineData("sdx-lowlevel")]
    [InlineData("sdx-effects")]
    public async Task Every_manual_or_unsupported_source_finding_of_a_fixture_is_marked_in_its_file(string fixture)
    {
        var report = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of(fixture), _catalogs.Value, null, CancellationToken.None);
        var file = Path.Combine(FixturePaths.Of(fixture), "Game1.cs");

        var migrated = SdxPipeline.Rewrite(File.ReadAllText(file)).Text;

        var markable = report.Findings
            .Where(f => f.RuleId.StartsWith("SDX", StringComparison.Ordinal) && f.Classification != Classification.AutoFixable && f.Location is not null)
            .Select(f => f.RuleId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(markable);
        Assert.All(markable, id => Assert.Contains("// XNAREBORN: " + id + ":", migrated, StringComparison.Ordinal));
    }

    [Fact]
    public void Autofixable_rules_of_the_fixture_leave_no_toolkit_namespace_behind()
    {
        var migrated = SdxPipeline.Rewrite(File.ReadAllText(Path.Combine(FixturePaths.Of("sdx-basic"), "Game1.cs"))).Text;

        Assert.DoesNotContain("SharpDX", migrated, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyboardManager", migrated, StringComparison.Ordinal);
        Assert.DoesNotContain("ToDisposeContent", migrated, StringComparison.Ordinal);
        Assert.DoesNotContain("XNAREBORN", migrated, StringComparison.Ordinal);
    }

    [Fact]
    public void Migrating_a_file_twice_changes_nothing_the_second_time()
    {
        var once = SdxPipeline.Rewrite(File.ReadAllText(Path.Combine(FixturePaths.Of("sdx-lowlevel"), "Game1.cs"))).Text;

        var twice = SdxPipeline.Rewrite(once);

        Assert.Equal(once, twice.Text);
    }

    [MonoGameFact]
    public void The_migrated_sdx_basic_game_compiles_against_monogame()
    {
        var game = SdxPipeline.Rewrite(File.ReadAllText(Path.Combine(FixturePaths.Of("sdx-basic"), "Game1.cs"))).Text;
        var program = File.ReadAllText(Path.Combine(FixturePaths.Of("sdx-basic"), "Program.cs"));

        var errors = SdxPipeline.CompileErrors(("Game1.cs", game), ("Program.cs", program));

        Assert.Empty(errors);
    }

    [MonoGameFact]
    public void The_migrated_input_code_of_sdx_basic_reads_the_keyboard_the_monogame_way()
    {
        var game = SdxPipeline.Rewrite(File.ReadAllText(Path.Combine(FixturePaths.Of("sdx-basic"), "Game1.cs"))).Text;

        Assert.Contains("var state = Keyboard.GetState();", game, StringComparison.Ordinal);
        Assert.Contains("using Microsoft.Xna.Framework.Input;", game, StringComparison.Ordinal);
        Assert.Contains("GraphicsDevice.BlendState = BlendState.AlphaBlend;", game, StringComparison.Ordinal);
        Assert.Contains("Matrix.CreateLookAt(", game, StringComparison.Ordinal);
        Assert.Contains("MathHelper.PiOver4", game, StringComparison.Ordinal);
    }
}
