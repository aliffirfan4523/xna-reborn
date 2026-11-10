using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator.Rewriters;

/// <summary>The RenderState rewriter maps known assignments and marks the rest.</summary>
[Trait("Category", "Migrator")]
public class RenderStateRewriterTests
{
    private const string _marker = "// XNAREBORN: XNA024: Replace this RenderState use with BlendState, DepthStencilState, RasterizerState or SamplerState objects.";

    private static readonly string[] _windows = ["WINDOWS", "DEBUG", "TRACE"];

    private readonly RenderStateRewriter _rewriter = new();

    private static string Wrap(string body, string usings = "using Microsoft.Xna.Framework.Graphics;\n") =>
        usings +
        "\n" +
        "class G\n" +
        "{\n" +
        "    GraphicsDevice GraphicsDevice;\n" +
        "\n" +
        "    void Draw()\n" +
        "    {\n" +
        body +
        "    }\n" +
        "}\n";

    private static SourceRewriteContext Context(string text, string path = "Game1.cs") => new(path, text, _windows);

    [Theory]
    [InlineData("GraphicsDevice.RenderState.AlphaBlendEnable = true;", "GraphicsDevice.BlendState = BlendState.AlphaBlend;")]
    [InlineData("GraphicsDevice.RenderState.AlphaBlendEnable = false;", "GraphicsDevice.BlendState = BlendState.Opaque;")]
    [InlineData("GraphicsDevice.RenderState.DepthBufferEnable = true;", "GraphicsDevice.DepthStencilState = DepthStencilState.Default;")]
    [InlineData("GraphicsDevice.RenderState.DepthBufferEnable = false;", "GraphicsDevice.DepthStencilState = DepthStencilState.None;")]
    [InlineData("GraphicsDevice.RenderState.CullMode = CullMode.None;", "GraphicsDevice.RasterizerState = RasterizerState.CullNone;")]
    [InlineData("GraphicsDevice.RenderState.CullMode = CullMode.CullClockwiseFace;", "GraphicsDevice.RasterizerState = RasterizerState.CullClockwise;")]
    [InlineData("GraphicsDevice.RenderState.CullMode = CullMode.CullCounterClockwiseFace;", "GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;")]
    [InlineData("this.GraphicsDevice.RenderState.AlphaBlendEnable = true;", "this.GraphicsDevice.BlendState = BlendState.AlphaBlend;")]
    [InlineData("graphics.GraphicsDevice.RenderState.DepthBufferEnable = false;", "graphics.GraphicsDevice.DepthStencilState = DepthStencilState.None;")]
    [InlineData("device.RenderState.CullMode = Microsoft.Xna.Framework.Graphics.CullMode.None;", "device.RasterizerState = RasterizerState.CullNone;")]
    public void Known_assignments_are_replaced_exactly(string before, string after)
    {
        var text = Wrap("        " + before + "\n");
        var context = Context(text);

        var result = _rewriter.Rewrite(context);

        Assert.Equal(Wrap("        " + after + "\n"), result.Text);
        var note = Assert.Single(result.Notes);
        Assert.Equal("XNA024", note.RuleId);
        Assert.False(note.NeedsManualWork);
    }

    [Fact]
    public void Alpha_blend_note_mentions_premultiplied_alpha()
    {
        var context = Context(Wrap("        GraphicsDevice.RenderState.AlphaBlendEnable = true;\n"));

        var note = Assert.Single(_rewriter.Rewrite(context).Notes);

        Assert.Equal(
            "Replaced 'RenderState.AlphaBlendEnable = true' with 'BlendState = BlendState.AlphaBlend'. BlendState.AlphaBlend expects premultiplied alpha; use BlendState.NonPremultiplied for straight alpha.",
            note.Message);
    }

    [Fact]
    public void Surrounding_trivia_and_other_lines_are_kept()
    {
        var body =
            "        // enable blending\n" +
            "\n" +
            "\t  GraphicsDevice.RenderState.AlphaBlendEnable = true; // trailing\n" +
            "        GraphicsDevice.Clear(Color.Black);\n";
        var expected =
            "        // enable blending\n" +
            "\n" +
            "\t  GraphicsDevice.BlendState = BlendState.AlphaBlend; // trailing\n" +
            "        GraphicsDevice.Clear(Color.Black);\n";

        var result = _rewriter.Rewrite(Context(Wrap(body)));

        Assert.Equal(Wrap(expected), result.Text);
    }

    [Fact]
    public void Several_statements_are_all_replaced()
    {
        var body =
            "        GraphicsDevice.RenderState.AlphaBlendEnable = true;\n" +
            "        GraphicsDevice.RenderState.DepthBufferEnable = false;\n" +
            "        GraphicsDevice.RenderState.CullMode = CullMode.None;\n";
        var expected =
            "        GraphicsDevice.BlendState = BlendState.AlphaBlend;\n" +
            "        GraphicsDevice.DepthStencilState = DepthStencilState.None;\n" +
            "        GraphicsDevice.RasterizerState = RasterizerState.CullNone;\n";

        var result = _rewriter.Rewrite(Context(Wrap(body)));

        Assert.Equal(Wrap(expected), result.Text);
        Assert.Equal(3, result.Notes.Count);
        Assert.All(result.Notes, n => Assert.False(n.NeedsManualWork));
    }

    [Fact]
    public void Embedded_statement_and_line_broken_receiver_chain_are_replaced()
    {
        var body =
            "        if (GraphicsDevice != null) GraphicsDevice.RenderState.DepthBufferEnable = true;\n" +
            "        GraphicsDevice\n" +
            "            .RenderState.AlphaBlendEnable = false;\n";
        var expected =
            "        if (GraphicsDevice != null) GraphicsDevice.DepthStencilState = DepthStencilState.Default;\n" +
            "        GraphicsDevice\n" +
            "            .BlendState = BlendState.Opaque;\n";

        var result = _rewriter.Rewrite(Context(Wrap(body)));

        Assert.Equal(Wrap(expected), result.Text);
    }

    [Fact]
    public void Aliased_or_unimported_namespace_gets_qualified_state_names()
    {
        const string body = "        GraphicsDevice.RenderState.AlphaBlendEnable = false;\n";
        var qualifiedOnly = Wrap(body, usings: string.Empty).Replace("GraphicsDevice GraphicsDevice;", "Microsoft.Xna.Framework.Graphics.GraphicsDevice GraphicsDevice;");
        var aliased = Wrap(body, "using G = Microsoft.Xna.Framework.Graphics;\n");

        var fromQualified = _rewriter.Rewrite(Context(qualifiedOnly));
        var fromAlias = _rewriter.Rewrite(Context(aliased));

        Assert.Contains("GraphicsDevice.BlendState = Microsoft.Xna.Framework.Graphics.BlendState.Opaque;", fromQualified.Text);
        Assert.Contains("GraphicsDevice.BlendState = Microsoft.Xna.Framework.Graphics.BlendState.Opaque;", fromAlias.Text);
    }

    [Theory]
    [InlineData("GraphicsDevice.RenderState.DepthBufferWriteEnable = false;")]
    [InlineData("GraphicsDevice.RenderState.SourceBlend = Blend.One;")]
    [InlineData("GraphicsDevice.RenderState.AlphaBlendEnable = enabled;")]
    [InlineData("GraphicsDevice.RenderState.AlphaBlendEnable = !true;")]
    [InlineData("GraphicsDevice.RenderState.CullMode = mode;")]
    [InlineData("GraphicsDevice.RenderState.CullMode = CullMode.Unknown;")]
    [InlineData("GraphicsDevice.RenderState.CullMode = Other.None;")]
    [InlineData("GraphicsDevice.RenderState.AlphaBlendEnable |= true;")]
    [InlineData("GraphicsDevice.RenderState.AlphaBlendEnable = /* why */ true;")]
    [InlineData("var rs = GraphicsDevice.RenderState;")]
    [InlineData("var on = GraphicsDevice.RenderState.AlphaBlendEnable;")]
    [InlineData("Save(GraphicsDevice.RenderState);")]
    [InlineData("flag = GraphicsDevice.RenderState.AlphaBlendEnable = true;")]
    [InlineData("GraphicsDevice.RenderState = old;")]
    public void Ambiguous_uses_get_a_marker_and_no_change(string statement)
    {
        var text = Wrap("        " + statement + "\n");
        var expected = Wrap("        " + _marker + "\n        " + statement + "\n");

        var result = _rewriter.Rewrite(Context(text));

        Assert.Equal(expected, result.Text);
        var note = Assert.Single(result.Notes);
        Assert.Equal("XNA024", note.RuleId);
        Assert.True(note.NeedsManualWork);
    }

    [Fact]
    public void Mixed_file_replaces_known_and_marks_unknown()
    {
        var body =
            "        GraphicsDevice.RenderState.AlphaBlendEnable = true;\n" +
            "        GraphicsDevice.RenderState.SourceBlend = Blend.One;\n" +
            "        GraphicsDevice.RenderState.DepthBufferEnable = false;\n";
        var expected =
            "        GraphicsDevice.BlendState = BlendState.AlphaBlend;\n" +
            "        " + _marker + "\n" +
            "        GraphicsDevice.RenderState.SourceBlend = Blend.One;\n" +
            "        GraphicsDevice.DepthStencilState = DepthStencilState.None;\n";

        var result = _rewriter.Rewrite(Context(Wrap(body)));

        Assert.Equal(Wrap(expected), result.Text);
        Assert.Equal(2, result.Notes.Count(n => !n.NeedsManualWork));
        Assert.Equal(1, result.Notes.Count(n => n.NeedsManualWork));
    }

    [Fact]
    public void Two_uses_in_one_statement_get_one_marker()
    {
        const string statement = "Copy(GraphicsDevice.RenderState, GraphicsDevice.RenderState);";

        var result = _rewriter.Rewrite(Context(Wrap("        " + statement + "\n")));

        Assert.Equal(Wrap("        " + _marker + "\n        " + statement + "\n"), result.Text);
        Assert.Single(result.Notes);
    }

    [Fact]
    public void Several_marked_statements_on_one_line_and_in_separate_lines_all_get_markers()
    {
        var body =
            "        a = GraphicsDevice.RenderState; b = GraphicsDevice.RenderState;\n" +
            "        c = GraphicsDevice.RenderState;\n";

        var result = _rewriter.Rewrite(Context(Wrap(body)));

        Assert.Equal(2, result.Text.Split(_marker).Length - 1);
        Assert.Equal(2, result.Notes.Count);
        var again = _rewriter.Rewrite(Context(result.Text));
        Assert.Equal(result.Text, again.Text);
    }

    [Theory]
    [InlineData("using Microsoft.Xna.Framework;\n")]
    [InlineData("")]
    public void File_without_graphics_import_is_untouched(string usings)
    {
        var text = Wrap(
            "        GraphicsDevice.RenderState.AlphaBlendEnable = true;\n        var rs = GraphicsDevice.RenderState;\n",
            usings).Replace("GraphicsDevice GraphicsDevice;", "object GraphicsDevice;");
        var context = Context(text);

        var result = _rewriter.Rewrite(context);

        Assert.Same(text, result.Text);
        Assert.Empty(result.Notes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("using Microsoft.Xna.Framework.Graphics;\n")]
    [InlineData("using Microsoft.Xna.Framework.Graphics;\n\nclass A { void M() { var x = 1; GraphicsDevice.BlendState = BlendState.Opaque; } }\n")]
    public void Files_without_render_state_are_returned_as_the_same_text(string text)
    {
        var context = Context(text);

        var result = _rewriter.Rewrite(context);

        Assert.Same(text, result.Text);
        Assert.False(result.Changed(context));
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void Render_state_in_comments_and_strings_is_ignored()
    {
        var text = Wrap(
            "        // GraphicsDevice.RenderState.AlphaBlendEnable = true;\n" +
            "        var s = \"GraphicsDevice.RenderState.AlphaBlendEnable = true;\";\n");

        var result = _rewriter.Rewrite(Context(text));

        Assert.Same(text, result.Text);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void Code_in_inactive_conditional_blocks_is_not_touched()
    {
        var text = Wrap(
            "#if XBOX\n" +
            "        GraphicsDevice.RenderState.AlphaBlendEnable = true;\n" +
            "#endif\n");

        var result = _rewriter.Rewrite(Context(text));

        Assert.Same(text, result.Text);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void Code_in_active_conditional_blocks_is_rewritten_with_the_original_symbols()
    {
        var text = Wrap(
            "#if WINDOWS\n" +
            "        GraphicsDevice.RenderState.AlphaBlendEnable = true;\n" +
            "#endif\n");

        var result = _rewriter.Rewrite(Context(text));

        Assert.Equal(
            Wrap("#if WINDOWS\n        GraphicsDevice.BlendState = BlendState.AlphaBlend;\n#endif\n"),
            result.Text);
    }

    [Fact]
    public void Ambiguous_use_in_a_conditional_block_that_cannot_hold_a_marker_still_reports_a_note()
    {
        var text = Wrap(
            "#if WINDOWS\n" +
            "        var rs = GraphicsDevice.RenderState;\n" +
            "#endif\n");

        var result = _rewriter.Rewrite(Context(text));

        Assert.Equal(text, result.Text);
        var note = Assert.Single(result.Notes);
        Assert.True(note.NeedsManualWork);
    }

    [Theory]
    [InlineData("GraphicsDevice.RenderState.AlphaBlendEnable = true;")]
    [InlineData("GraphicsDevice.RenderState.SourceBlend = Blend.One;")]
    [InlineData("var rs = GraphicsDevice.RenderState;")]
    public void Second_run_changes_nothing_and_adds_no_notes(string statement)
    {
        var first = _rewriter.Rewrite(Context(Wrap("        " + statement + "\n")));

        var second = _rewriter.Rewrite(Context(first.Text));

        Assert.Equal(first.Text, second.Text);
        Assert.Empty(second.Notes);
    }

    [Fact]
    public void Crlf_line_endings_are_kept_for_replacements_and_markers()
    {
        var text = Wrap(
            "        GraphicsDevice.RenderState.AlphaBlendEnable = true;\n" +
            "        var rs = GraphicsDevice.RenderState;\n").Replace("\n", "\r\n", StringComparison.Ordinal);
        var expected = Wrap(
            "        GraphicsDevice.BlendState = BlendState.AlphaBlend;\n" +
            "        " + _marker + "\n" +
            "        var rs = GraphicsDevice.RenderState;\n").Replace("\n", "\r\n", StringComparison.Ordinal);

        var result = _rewriter.Rewrite(Context(text));

        Assert.Equal(expected, result.Text);
        Assert.DoesNotContain("\n", result.Text.Replace("\r\n", string.Empty, StringComparison.Ordinal));
        Assert.Equal(result.Text, _rewriter.Rewrite(Context(result.Text)).Text);
    }

    [Fact]
    public void Bom_is_kept()
    {
        var text = "\uFEFF" + Wrap("        GraphicsDevice.RenderState.DepthBufferEnable = true;\n");

        var result = _rewriter.Rewrite(Context(text));

        Assert.Equal("\uFEFF" + Wrap("        GraphicsDevice.DepthStencilState = DepthStencilState.Default;\n"), result.Text);
    }

    [Fact]
    public void Engine_runs_the_rewriter_only_when_its_rule_is_enabled()
    {
        var registry = new RewriterRegistry();
        registry.Register(_rewriter);
        var engine = new SourceRewriteEngine(registry);
        var text = Wrap("        GraphicsDevice.RenderState.AlphaBlendEnable = true;\n");

        var off = engine.Rewrite("Game1.cs", text, _windows, new HashSet<string>());
        var on = engine.Rewrite("Game1.cs", text, _windows, new HashSet<string> { "XNA024" });

        Assert.False(off.Changed);
        Assert.True(on.Changed);
        Assert.Equal(["XNA024"], on.AppliedRuleIds);
        Assert.Empty(on.Failures);
        Assert.Equal(Wrap("        GraphicsDevice.BlendState = BlendState.AlphaBlend;\n"), on.Text);
    }

    [Fact]
    public void Rule_id_is_xna024()
    {
        Assert.Equal("XNA024", _rewriter.RuleId);
    }

    [Fact]
    public void Null_context_throws()
    {
        Assert.Throws<ArgumentNullException>(() => _rewriter.Rewrite(null!));
    }

    [Fact]
    public void Legacy_fixture_changes_only_the_render_state_line()
    {
        var path = Path.Combine(FixturePaths.Of("xna31-legacy"), "Game1.cs");
        var original = File.ReadAllText(path);
        const string before = "GraphicsDevice.RenderState.AlphaBlendEnable = true;";
        const string after = "GraphicsDevice.BlendState = BlendState.AlphaBlend;";
        Assert.Contains(before, original);

        var result = _rewriter.Rewrite(Context(original, "Game1.cs"));

        Assert.Equal(original.Replace(before, after, StringComparison.Ordinal), result.Text);
        var note = Assert.Single(result.Notes);
        Assert.False(note.NeedsManualWork);
    }
}
