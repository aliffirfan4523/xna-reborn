using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator.Rewriters;

/// <summary>Effect.Begin and EffectPass.Begin/End become EffectPass.Apply, and unclear patterns get a marker.</summary>
[Trait("Category", "Migrator")]
public class EffectPassRewriterTests
{
    private const string _marker = "// XNAREBORN: XNA039: Replace Begin/End with pass.Apply() before drawing.";

    private static readonly string[] _symbols = ["WINDOWS", "DEBUG", "TRACE"];

    private readonly EffectPassRewriter _rewriter = new("XNA039");

    private static SourceRewriteContext Context(string text) => new("Game1.cs", text, _symbols);

    private static string Wrap(string body, string usings = "using Microsoft.Xna.Framework.Graphics;\n") =>
        usings +
        "\n" +
        "class G\n" +
        "{\n" +
        "    Effect effect;\n" +
        "\n" +
        "    void Draw()\n" +
        "    {\n" +
        body +
        "    }\n" +
        "}\n";

    private const string _fullPattern =
        "        effect.Begin();\n" +
        "        foreach (EffectPass pass in effect.CurrentTechnique.Passes)\n" +
        "        {\n" +
        "            pass.Begin();\n" +
        "            device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 1);\n" +
        "            pass.End();\n" +
        "        }\n" +
        "        effect.End();\n";

    private const string _rewrittenPattern =
        "        foreach (EffectPass pass in effect.CurrentTechnique.Passes)\n" +
        "        {\n" +
        "            pass.Apply();\n" +
        "            device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 1);\n" +
        "        }\n";

    [Theory]
    [InlineData("XNA039")]
    [InlineData("XNA040")]
    public void Rule_id_is_the_one_given(string ruleId)
    {
        Assert.Equal(ruleId, new EffectPassRewriter(ruleId).RuleId);
    }

    [Theory]
    [InlineData("XNA022")]
    [InlineData("")]
    [InlineData("xna039")]
    public void Other_rule_ids_are_refused(string ruleId)
    {
        Assert.Throws<ArgumentException>(() => new EffectPassRewriter(ruleId));
    }

    [Fact]
    public void Null_rule_id_or_context_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new EffectPassRewriter(null!));
        Assert.Throws<ArgumentNullException>(() => _rewriter.Rewrite(null!));
    }

    [Fact]
    public void Full_pattern_becomes_apply_and_removes_begin_and_end_lines()
    {
        var context = Context(Wrap(_fullPattern));

        var result = _rewriter.Rewrite(context);

        Assert.Equal(Wrap(_rewrittenPattern), result.Text);
        var note = Assert.Single(result.Notes);
        Assert.Equal("XNA039", note.RuleId);
        Assert.False(note.NeedsManualWork);
        Assert.Contains("pass.Apply()", note.Message);
    }

    [Fact]
    public void The_note_carries_the_rule_id_of_the_instance()
    {
        var result = new EffectPassRewriter("XNA040").Rewrite(Context(Wrap(_fullPattern)));

        Assert.Equal("XNA040", Assert.Single(result.Notes).RuleId);
    }

    [Fact]
    public void Var_loop_variable_this_receiver_and_save_state_argument_are_accepted()
    {
        var before = Wrap(
            "        this.effect.Begin(SaveStateMode.SaveState);\n" +
            "        foreach (var p in this.effect.CurrentTechnique.Passes)\n" +
            "        {\n" +
            "            p.Begin();\n" +
            "            Draw();\n" +
            "            p.End();\n" +
            "        }\n" +
            "        this.effect.End();\n");
        var after = Wrap(
            "        foreach (var p in this.effect.CurrentTechnique.Passes)\n" +
            "        {\n" +
            "            p.Apply();\n" +
            "            Draw();\n" +
            "        }\n");

        var result = _rewriter.Rewrite(Context(before));

        Assert.Equal(after, result.Text);
    }

    [Fact]
    public void Other_effect_names_and_qualified_loop_types_are_accepted()
    {
        var before = Wrap(
            "        basicEffect.Begin();\n" +
            "        foreach (Microsoft.Xna.Framework.Graphics.EffectPass pass in basicEffect.CurrentTechnique.Passes)\n" +
            "        {\n" +
            "            pass.Begin();\n" +
            "            pass.End();\n" +
            "        }\n" +
            "        basicEffect.End();\n");
        var after = Wrap(
            "        foreach (Microsoft.Xna.Framework.Graphics.EffectPass pass in basicEffect.CurrentTechnique.Passes)\n" +
            "        {\n" +
            "            pass.Apply();\n" +
            "        }\n");

        Assert.Equal(after, _rewriter.Rewrite(Context(before)).Text);
    }

    [Fact]
    public void Comments_commit_changes_and_statements_between_are_kept()
    {
        var before = Wrap(
            "        // set up\n" +
            "        effect.Begin();   // start\n" +
            "        effect.Parameters[\"World\"].SetValue(world);\n" +
            "        foreach (EffectPass pass in effect.CurrentTechnique.Passes)\n" +
            "        {\n" +
            "            pass.Begin();\n" +
            "\n" +
            "            effect.CommitChanges();\n" +
            "            Draw(); // draw\n" +
            "            pass.End();\n" +
            "        }\n" +
            "        effect.End();   // done\n" +
            "        Other();\n");
        var after = Wrap(
            "        // set up\n" +
            "        effect.Parameters[\"World\"].SetValue(world);\n" +
            "        foreach (EffectPass pass in effect.CurrentTechnique.Passes)\n" +
            "        {\n" +
            "            pass.Apply();\n" +
            "\n" +
            "            effect.CommitChanges();\n" +
            "            Draw(); // draw\n" +
            "        }\n" +
            "        Other();\n");

        var result = _rewriter.Rewrite(Context(before));

        // The two lines that carried trailing comments are not alone on their line; only the statement goes.
        var expected = after
            .Replace("        // set up\n", "        // set up\n        // start\n")
            .Replace("        Other();\n", "        // done\n        Other();\n");
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void Two_loops_can_share_one_begin_and_end()
    {
        var before = Wrap(
            "        effect.Begin();\n" +
            "        foreach (EffectPass a in effect.CurrentTechnique.Passes)\n" +
            "        {\n" +
            "            a.Begin();\n" +
            "            A();\n" +
            "            a.End();\n" +
            "        }\n" +
            "        foreach (EffectPass b in effect.CurrentTechnique.Passes)\n" +
            "        {\n" +
            "            b.Begin();\n" +
            "            B();\n" +
            "            b.End();\n" +
            "        }\n" +
            "        effect.End();\n");
        var after = Wrap(
            "        foreach (EffectPass a in effect.CurrentTechnique.Passes)\n" +
            "        {\n" +
            "            a.Apply();\n" +
            "            A();\n" +
            "        }\n" +
            "        foreach (EffectPass b in effect.CurrentTechnique.Passes)\n" +
            "        {\n" +
            "            b.Apply();\n" +
            "            B();\n" +
            "        }\n");

        var result = _rewriter.Rewrite(Context(before));

        Assert.Equal(after, result.Text);
        Assert.Equal(2, result.Notes.Count);
    }

    [Fact]
    public void Statements_on_a_shared_line_are_removed_without_touching_their_neighbours()
    {
        var before = Wrap(
            "        effect.Begin(); foreach (EffectPass pass in effect.CurrentTechnique.Passes) { pass.Begin(); Draw(); pass.End(); } effect.End(); Other();\n");
        var after = Wrap(
            "        foreach (EffectPass pass in effect.CurrentTechnique.Passes) { pass.Apply(); Draw(); } Other();\n");

        Assert.Equal(after, _rewriter.Rewrite(Context(before)).Text);
    }

    [Fact]
    public void Crlf_input_keeps_crlf()
    {
        var before = Wrap(_fullPattern).Replace("\n", "\r\n");
        var after = Wrap(_rewrittenPattern).Replace("\n", "\r\n");

        var result = _rewriter.Rewrite(Context(before));

        Assert.Equal(after, result.Text);
        Assert.DoesNotContain("\n", result.Text.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void Bom_is_kept()
    {
        var before = "\uFEFF" + Wrap(_fullPattern);

        var result = _rewriter.Rewrite(Context(before));

        Assert.Equal("\uFEFF" + Wrap(_rewrittenPattern), result.Text);
    }

    [Fact]
    public void Alias_and_qualified_imports_enable_the_rewrite()
    {
        var aliased = Wrap(_fullPattern, "using G = Microsoft.Xna.Framework.Graphics;\n");
        var qualified = Wrap(
            _fullPattern.Replace("EffectPass pass", "Microsoft.Xna.Framework.Graphics.EffectPass pass"),
            string.Empty);

        Assert.NotEqual(aliased, _rewriter.Rewrite(Context(aliased)).Text);
        Assert.NotEqual(qualified, _rewriter.Rewrite(Context(qualified)).Text);
    }

    [Theory]
    [InlineData("using System;\n")]
    [InlineData("using static Microsoft.Xna.Framework.Graphics.Color;\n")]
    [InlineData("using Microsoft.Xna.Framework.Graphics.PackedVector;\n")]
    public void Files_that_do_not_import_graphics_are_untouched(string usings)
    {
        var text = Wrap(_fullPattern, usings);
        var context = Context(text);

        var result = _rewriter.Rewrite(context);

        Assert.Same(text, result.Text);
        Assert.False(result.Changed(context));
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void Sprite_batch_begin_and_end_are_left_alone_without_a_marker()
    {
        var text = Wrap(
            "        spriteBatch.Begin();\n" +
            "        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);\n" +
            "        spriteBatch.End();\n");
        var context = Context(text);

        var result = _rewriter.Rewrite(context);

        Assert.Same(text, result.Text);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void Pass_begin_without_effect_begin_gets_a_marker_and_no_change()
    {
        var text = Wrap(
            "        foreach (EffectPass pass in effect.CurrentTechnique.Passes)\n" +
            "        {\n" +
            "            pass.Begin();\n" +
            "            Draw();\n" +
            "            pass.End();\n" +
            "        }\n" +
            "        effect.End();\n");
        var expected = Wrap(
            "        foreach (EffectPass pass in effect.CurrentTechnique.Passes)\n" +
            "        {\n" +
            "            " + _marker + "\n" +
            "            pass.Begin();\n" +
            "            Draw();\n" +
            "            pass.End();\n" +
            "        }\n" +
            "        effect.End();\n");

        var result = _rewriter.Rewrite(Context(text));

        Assert.Equal(expected, result.Text);
        var note = Assert.Single(result.Notes);
        Assert.True(note.NeedsManualWork);
        Assert.Equal("XNA039", note.RuleId);
        Assert.Contains("pass.Apply()", note.Message);
    }

    [Fact]
    public void Effect_begin_without_a_loop_gets_a_marker()
    {
        var text = Wrap("        effect.Begin();\n        Draw();\n        effect.End();\n");
        var expected = Wrap(
            "        " + _marker + "\n" +
            "        effect.Begin();\n" +
            "        Draw();\n" +
            "        effect.End();\n");

        var result = _rewriter.Rewrite(Context(text));

        Assert.Equal(expected, result.Text);
        Assert.True(Assert.Single(result.Notes).NeedsManualWork);
    }

    [Fact]
    public void Missing_effect_end_gets_markers_and_no_change()
    {
        var text = Wrap(
            _fullPattern.Replace("        effect.End();\n", string.Empty));

        var result = _rewriter.Rewrite(Context(text));

        Assert.Equal(2, result.Notes.Count);
        Assert.All(result.Notes, n => Assert.True(n.NeedsManualWork));
        Assert.Contains("pass.Begin();", result.Text);
        Assert.Contains("pass.End();", result.Text);
        Assert.Equal(text, RemoveMarkerLines(result.Text));
    }

    [Fact]
    public void Effect_begin_with_another_argument_is_not_rewritten()
    {
        var text = Wrap(_fullPattern.Replace("effect.Begin();", "effect.Begin(options);"));

        var result = _rewriter.Rewrite(Context(text));

        Assert.Equal(text, RemoveMarkerLines(result.Text));
        Assert.Contains(result.Notes, n => n.NeedsManualWork);
        Assert.Contains("effect.Begin(options);", result.Text);
    }

    [Fact]
    public void Loop_without_pass_end_or_with_calls_in_nested_code_is_not_rewritten()
    {
        var withoutEnd = Wrap(_fullPattern.Replace("            pass.End();\n", string.Empty));
        var nested = Wrap(_fullPattern.Replace(
            "            pass.End();\n",
            "            if (x) { pass.End(); }\n"));

        foreach (var text in new[] { withoutEnd, nested })
        {
            var result = _rewriter.Rewrite(Context(text));

            Assert.Equal(text, RemoveMarkerLines(result.Text));
            Assert.Contains(result.Notes, n => n.NeedsManualWork);
        }
    }

    [Fact]
    public void Begin_and_end_in_different_blocks_are_not_rewritten()
    {
        var text = Wrap(
            "        effect.Begin();\n" +
            "        if (x)\n" +
            "        {\n" +
            "            foreach (EffectPass pass in effect.CurrentTechnique.Passes)\n" +
            "            {\n" +
            "                pass.Begin();\n" +
            "                pass.End();\n" +
            "            }\n" +
            "        }\n" +
            "        effect.End();\n");

        var result = _rewriter.Rewrite(Context(text));

        Assert.Equal(text, RemoveMarkerLines(result.Text));
        Assert.Equal(2, result.Notes.Count);
    }

    [Fact]
    public void Code_inside_inactive_if_blocks_is_ignored()
    {
        var text = Wrap("#if XBOX\n" + _fullPattern + "#endif\n");
        var context = Context(text);

        var result = _rewriter.Rewrite(context);

        Assert.Same(text, result.Text);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void Text_without_the_pattern_is_returned_as_is()
    {
        var text = Wrap("        Draw();\n");
        var context = Context(text);

        var result = _rewriter.Rewrite(context);

        Assert.Same(text, result.Text);
        Assert.Empty(result.Notes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("using Microsoft.Xna.Framework.Graphics;")]
    [InlineData("class {{{ not valid C#")]
    public void Empty_and_broken_text_does_not_throw(string text)
    {
        var result = _rewriter.Rewrite(Context(text));

        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void A_second_run_changes_nothing_and_adds_no_notes()
    {
        var text = Wrap(_fullPattern + "        effect.Begin();\n        Draw();\n");

        var first = _rewriter.Rewrite(Context(text));
        var second = _rewriter.Rewrite(Context(first.Text));

        Assert.NotEqual(text, first.Text);
        Assert.Equal(2, first.Notes.Count);
        Assert.Equal(first.Text, second.Text);
        Assert.Empty(second.Notes);
    }

    [Fact]
    public void The_other_rule_id_instance_is_a_no_op_after_the_first()
    {
        var first = _rewriter.Rewrite(Context(Wrap(_fullPattern)));
        var second = new EffectPassRewriter("XNA040").Rewrite(Context(first.Text));

        Assert.Equal(first.Text, second.Text);
        Assert.Empty(second.Notes);
    }

    [Fact]
    public void Engine_runs_both_instances_and_only_the_first_changes_the_file()
    {
        var registry = new RewriterRegistry();
        registry.Register(new EffectPassRewriter("XNA040"));
        registry.Register(new EffectPassRewriter("XNA039"));
        var engine = new SourceRewriteEngine(registry);
        var text = Wrap(_fullPattern);

        var both = engine.Rewrite("Game1.cs", text, _symbols, new HashSet<string> { "XNA039", "XNA040" });
        var onlyEffect = engine.Rewrite("Game1.cs", text, _symbols, new HashSet<string> { "XNA040" });
        var off = engine.Rewrite("Game1.cs", text, _symbols, new HashSet<string>());

        Assert.True(both.Changed);
        Assert.Equal(Wrap(_rewrittenPattern), both.Text);
        Assert.Equal(["XNA039"], both.AppliedRuleIds);
        Assert.Empty(both.Failures);
        Assert.Equal(Wrap(_rewrittenPattern), onlyEffect.Text);
        Assert.Equal(["XNA040"], onlyEffect.AppliedRuleIds);
        Assert.False(off.Changed);
    }

    [Fact]
    public void Legacy_fixture_has_no_effect_pattern_and_stays_byte_for_byte_the_same()
    {
        var text = File.ReadAllText(Path.Combine(FixturePaths.Of("xna31-legacy"), "Game1.cs"));
        var context = Context(text);

        var result = _rewriter.Rewrite(context);

        Assert.Same(text, result.Text);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void Pattern_added_to_the_legacy_fixture_changes_only_that_method()
    {
        var text = File.ReadAllText(Path.Combine(FixturePaths.Of("xna31-legacy"), "Game1.cs"));
        var nl = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var anchor = "        protected override void Draw(GameTime gameTime)";
        Assert.Contains(anchor, text);

        string Method(string body) =>
            "        void DrawWithEffect(Effect effect)" + nl +
            "        {" + nl +
            body.Replace("\n", nl) +
            "        }" + nl + nl;

        var before = text.Replace(anchor, Method(
            "            effect.Begin();\n" +
            "            foreach (EffectPass pass in effect.CurrentTechnique.Passes)\n" +
            "            {\n" +
            "                pass.Begin();\n" +
            "                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 1);\n" +
            "                pass.End();\n" +
            "            }\n" +
            "            effect.End();\n") + anchor, StringComparison.Ordinal);
        var after = text.Replace(anchor, Method(
            "            foreach (EffectPass pass in effect.CurrentTechnique.Passes)\n" +
            "            {\n" +
            "                pass.Apply();\n" +
            "                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 1);\n" +
            "            }\n") + anchor, StringComparison.Ordinal);

        var result = _rewriter.Rewrite(Context(before));

        Assert.Equal(after, result.Text);
        Assert.Single(result.Notes);
    }

    private static string RemoveMarkerLines(string text) =>
        string.Concat(
            text.Split('\n', StringSplitOptions.None)
                .Where(line => !line.TrimStart().StartsWith("// XNAREBORN:", StringComparison.Ordinal))
                .Select((line, index) => (index == 0 ? string.Empty : "\n") + line));

    [Fact]
    public void Unmatched_begin_inside_if_windows_gets_a_marker_and_a_second_run_adds_nothing()
    {
        var text = Wrap("#if WINDOWS\n        effect.Begin();\n#endif\n");

        var first = _rewriter.Rewrite(Context(text));
        var second = _rewriter.Rewrite(Context(first.Text));

        Assert.Contains(_marker + "\n        effect.Begin();", first.Text);
        Assert.Single(first.Notes);
        Assert.Empty(second.Notes);
        Assert.Equal(first.Text, second.Text);
    }
}
