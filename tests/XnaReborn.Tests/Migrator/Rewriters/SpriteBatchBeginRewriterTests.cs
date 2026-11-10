using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator.Rewriters;

/// <summary>XNA022 rewrites SpriteBatch.Begin(SpriteBlendMode, ...) to the 4.0 overloads and marks what it cannot decide.</summary>
[Trait("Category", "Migrator")]
public class SpriteBatchBeginRewriterTests
{
    private static readonly string[] _symbols = ["WINDOWS"];

    private const string _stateMarker = "// XNAREBORN: XNA023: SpriteBatch no longer restores device state; set the states you need after End().";

    private const string _manualMarker =
        "// XNAREBORN: XNA022: Replace this SpriteBlendMode use with a BlendState; SpriteBatch.Begin now takes SpriteSortMode and BlendState.";

    private readonly SpriteBatchBeginRewriter _rewriter = new();

    private static string File(string body, string usings = "using Microsoft.Xna.Framework.Graphics;\n") =>
        usings + "\nclass G\n{\n    void Draw()\n    {\n" + body + "    }\n}\n";

    private static SourceRewriteContext Context(string text) => new("Game1.cs", text, _symbols);

    private SourceRewriteResult Run(string text) => _rewriter.Rewrite(Context(text));

    [Fact]
    public void Rule_id_is_xna022()
    {
        Assert.Equal("XNA022", _rewriter.RuleId);
    }

    [Theory]
    [InlineData("AlphaBlend", "AlphaBlend")]
    [InlineData("Additive", "Additive")]
    [InlineData("None", "Opaque")]
    public void One_argument_overload_maps_the_blend_mode(string member, string state)
    {
        var context = Context(File($"        batch.Begin(SpriteBlendMode.{member});\n"));

        var result = _rewriter.Rewrite(context);

        Assert.Equal(File($"        batch.Begin(SpriteSortMode.Deferred, BlendState.{state});\n"), result.Text);
        var note = Assert.Single(result.Notes);
        Assert.Equal("XNA022", note.RuleId);
        Assert.False(note.NeedsManualWork);
        Assert.Contains($"SpriteBlendMode.{member}", note.Message);
        Assert.Contains($"BlendState.{state}", note.Message);
        Assert.Contains("line 7", note.Message);
    }

    [Fact]
    public void Alpha_blend_note_explains_premultiplied_alpha()
    {
        var result = Run(File("        batch.Begin(SpriteBlendMode.AlphaBlend);\n"));

        var note = Assert.Single(result.Notes);
        Assert.Contains("premultiply alpha", note.Message);
        Assert.Contains("BlendState.NonPremultiplied", note.Message);
        Assert.False(note.NeedsManualWork);
    }

    [Fact]
    public void Additive_note_has_no_premultiplied_alpha_advice()
    {
        var result = Run(File("        batch.Begin(SpriteBlendMode.Additive);\n"));

        Assert.DoesNotContain("premultiply", Assert.Single(result.Notes).Message);
    }

    [Fact]
    public void Three_argument_overload_with_save_state_none_drops_the_state_silently()
    {
        var result = Run(File("        batch.Begin(SpriteBlendMode.Additive, SpriteSortMode.Deferred, SaveStateMode.None);\n"));

        Assert.Equal(File("        batch.Begin(SpriteSortMode.Deferred, BlendState.Additive);\n"), result.Text);
        var note = Assert.Single(result.Notes);
        Assert.False(note.NeedsManualWork);
        Assert.DoesNotContain("XNAREBORN", result.Text);
    }

    [Fact]
    public void Three_argument_overload_with_save_state_adds_the_xna023_marker()
    {
        var result = Run(File("        batch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.BackToFront, SaveStateMode.SaveState);\n"));

        var expected = File(
            "        " + _stateMarker + "\n" +
            "        batch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend);\n");
        Assert.Equal(expected, result.Text);
        Assert.Equal(2, result.Notes.Count);
        Assert.False(result.Notes[0].NeedsManualWork);
        Assert.Equal("XNA022", result.Notes[0].RuleId);
        Assert.True(result.Notes[1].NeedsManualWork);
        Assert.Equal("XNA023", result.Notes[1].RuleId);
    }

    [Fact]
    public void Four_argument_overload_keeps_the_transform_and_fills_the_middle_with_nulls()
    {
        var result = Run(File("        batch.Begin(SpriteBlendMode.None, SpriteSortMode.Immediate, SaveStateMode.None, camera.GetMatrix());\n"));

        Assert.Equal(
            File("        batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, null, null, null, null, camera.GetMatrix());\n"),
            result.Text);
        Assert.False(Assert.Single(result.Notes).NeedsManualWork);
    }

    [Fact]
    public void Four_argument_overload_with_save_state_is_rewritten_and_marked()
    {
        var result = Run(File("        batch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Deferred, SaveStateMode.SaveState, transform);\n"));

        var expected = File(
            "        " + _stateMarker + "\n" +
            "        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, null, transform);\n");
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void Multi_line_call_keeps_its_layout()
    {
        var before = File(
            "        batch.Begin(\n" +
            "            SpriteBlendMode.Additive,\n" +
            "            SpriteSortMode.Deferred,\n" +
            "            SaveStateMode.SaveState);\n");
        var after = File(
            "        " + _stateMarker + "\n" +
            "        batch.Begin(\n" +
            "            SpriteSortMode.Deferred,\n" +
            "            BlendState.Additive);\n");

        Assert.Equal(after, Run(before).Text);
    }

    [Fact]
    public void Fully_qualified_names_keep_their_qualification()
    {
        var before = File(
            "        batch.Begin(Microsoft.Xna.Framework.Graphics.SpriteBlendMode.AlphaBlend);\n",
            usings: string.Empty);
        var after = File(
            "        batch.Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred, Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend);\n",
            usings: string.Empty);

        Assert.Equal(after, Run(before).Text);
    }

    [Fact]
    public void Namespace_alias_is_accepted_and_kept()
    {
        var before = File("        batch.Begin(Gfx.SpriteBlendMode.Additive);\n", "using Gfx = Microsoft.Xna.Framework.Graphics;\n");
        var after = File("        batch.Begin(Gfx.SpriteSortMode.Deferred, Gfx.BlendState.Additive);\n", "using Gfx = Microsoft.Xna.Framework.Graphics;\n");

        Assert.Equal(after, Run(before).Text);
    }

    [Fact]
    public void Several_calls_in_one_file_are_all_rewritten()
    {
        var before = File(
            "        a.Begin(SpriteBlendMode.AlphaBlend);\n" +
            "        a.End();\n" +
            "        b.Begin(SpriteBlendMode.None);\n" +
            "        b.End();\n");
        var after = File(
            "        a.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);\n" +
            "        a.End();\n" +
            "        b.Begin(SpriteSortMode.Deferred, BlendState.Opaque);\n" +
            "        b.End();\n");

        var result = Run(before);

        Assert.Equal(after, result.Text);
        Assert.Equal(2, result.Notes.Count);
    }

    [Fact]
    public void Comments_and_spacing_around_the_call_are_preserved()
    {
        var before = File("        batch.Begin( /* blend */ SpriteBlendMode.AlphaBlend ); // draw sprites\n\n");
        var after = File("        batch.Begin( /* blend */ SpriteSortMode.Deferred, BlendState.AlphaBlend ); // draw sprites\n\n");

        Assert.Equal(after, Run(before).Text);
    }

    [Fact]
    public void Comment_between_the_removed_arguments_is_not_dropped()
    {
        var before = File("        batch.Begin(SpriteBlendMode.Additive, SpriteSortMode.Deferred, /* keep */ SaveStateMode.None);\n");

        var result = Run(before);

        Assert.Contains("/* keep */", result.Text);
        Assert.Contains("SpriteBlendMode.Additive", result.Text);
        Assert.Contains(_manualMarker, result.Text);
    }

    [Fact]
    public void Crlf_input_keeps_crlf_everywhere()
    {
        var before = File(
            "        batch.Begin(SpriteBlendMode.Additive, SpriteSortMode.Deferred, SaveStateMode.SaveState);\n" +
            "        batch.Begin(SpriteBlendMode.AlphaBlend);\n").Replace("\n", "\r\n");
        var after = File(
            "        " + _stateMarker + "\n" +
            "        batch.Begin(SpriteSortMode.Deferred, BlendState.Additive);\n" +
            "        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);\n").Replace("\n", "\r\n");

        var result = Run(before);

        Assert.Equal(after, result.Text);
        Assert.DoesNotContain("\n", result.Text.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void Byte_order_mark_is_kept()
    {
        var before = "\uFEFF" + File("        batch.Begin(SpriteBlendMode.None);\n");

        var result = Run(before);

        Assert.Equal("\uFEFF" + File("        batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);\n"), result.Text);
    }

    [Fact]
    public void Two_calls_on_one_line_are_both_rewritten_and_marked()
    {
        var before = File(
            "        a.Begin(SpriteBlendMode.None, SpriteSortMode.Deferred, SaveStateMode.SaveState); b.Begin(SpriteBlendMode.None, SpriteSortMode.Deferred, SaveStateMode.SaveState);\n");

        var result = Run(before);

        Assert.Equal(
            File(
                "        " + _stateMarker + "\n" +
                "        a.Begin(SpriteSortMode.Deferred, BlendState.Opaque); b.Begin(SpriteSortMode.Deferred, BlendState.Opaque);\n"),
            result.Text);
    }

    [Fact]
    public void Variable_as_first_argument_gets_a_marker_and_no_change()
    {
        var before = File(
            "        SpriteBlendMode mode = Pick();\n" +
            "        batch.Begin(mode);\n");

        var result = Run(before);

        var expected = File(
            "        " + _manualMarker + "\n" +
            "        SpriteBlendMode mode = Pick();\n" +
            "        " + _manualMarker + "\n" +
            "        batch.Begin(mode);\n");
        Assert.Equal(expected, result.Text);
        Assert.All(result.Notes, n => Assert.True(n.NeedsManualWork));
        Assert.Equal(2, result.Notes.Count);
    }

    [Fact]
    public void Unknown_blend_member_is_marked_and_left_alone()
    {
        var before = File("        batch.Begin(SpriteBlendMode.AlphaBlend | SpriteBlendMode.Additive);\n");

        var result = Run(before);

        Assert.Equal(File("        " + _manualMarker + "\n        batch.Begin(SpriteBlendMode.AlphaBlend | SpriteBlendMode.Additive);\n"), result.Text);
        Assert.True(Assert.Single(result.Notes).NeedsManualWork);
    }

    [Theory]
    [InlineData("        batch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Deferred);\n")]
    [InlineData("        batch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Deferred, mode);\n")]
    [InlineData("        batch.Begin(blendMode: SpriteBlendMode.AlphaBlend);\n")]
    [InlineData("        batch.Begin(SpriteBlendMode.Subtract);\n")]
    public void Unsupported_shapes_get_a_marker_and_no_change(string statement)
    {
        var before = File(statement);

        var result = Run(before);

        Assert.Equal(File("        " + _manualMarker + "\n" + statement), result.Text);
        Assert.True(Assert.Single(result.Notes).NeedsManualWork);
    }

    [Fact]
    public void Other_remaining_use_of_the_type_is_marked()
    {
        var before = File(
            "        SpriteBlendMode current = SpriteBlendMode.None;\n" +
            "        batch.Begin(SpriteBlendMode.AlphaBlend);\n");

        var result = Run(before);

        var expected = File(
            "        " + _manualMarker + "\n" +
            "        SpriteBlendMode current = SpriteBlendMode.None;\n" +
            "        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);\n");
        Assert.Equal(expected, result.Text);
        Assert.Equal(2, result.Notes.Count);
    }

    [Fact]
    public void Four_oh_style_begin_calls_are_left_alone_even_when_the_type_is_mentioned()
    {
        var before = File(
            "        // SpriteBlendMode used to live here\n" +
            "        batch.Begin();\n" +
            "        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);\n" +
            "        effect.Begin(SaveStateMode.None);\n",
            "using Microsoft.Xna.Framework.Graphics;\nusing Alias = Microsoft.Xna.Framework.Graphics.SpriteBlendMode;\n");

        var result = Run(before);

        Assert.DoesNotContain("batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);\n" + "        // XNAREBORN", result.Text);
        Assert.Contains("batch.Begin();", result.Text);
        Assert.Contains("effect.Begin(SaveStateMode.None);", result.Text);
        Assert.DoesNotContain("SpriteSortMode.Deferred, BlendState.AlphaBlend, null", result.Text);
        Assert.Single(result.Notes);
    }

    [Theory]
    [InlineData("class A { }\n")]
    [InlineData("using Microsoft.Xna.Framework.Graphics;\nclass A { void M(SpriteBatch b) { b.Begin(); } }\n")]
    [InlineData("using Microsoft.Xna.Framework.Graphics;\nclass A { void M(SpriteBatch b) { b.Begin(SpriteSortMode.Deferred, BlendState.Opaque); } }\n")]
    [InlineData("class A { void M(Other b) { b.Begin(SpriteBlendMode.AlphaBlend); } }\n")]
    [InlineData("using Some.Other.Graphics;\nclass A { void M(Other b) { b.Begin(SpriteBlendMode.AlphaBlend); } }\n")]
    [InlineData("using Microsoft.Xna.Framework.Graphics;\nclass A { string s = \"b.Begin(SpriteBlendMode.None)\"; }\n")]
    public void No_op_returns_the_input_instance_and_no_notes(string text)
    {
        var context = Context(text);

        var result = _rewriter.Rewrite(context);

        Assert.Same(text, result.Text);
        Assert.False(result.Changed(context));
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void Code_in_inactive_conditional_is_not_touched()
    {
        var text = File("#if XBOX360\n        batch.Begin(SpriteBlendMode.None);\n#endif\n");

        var result = Run(text);

        Assert.Same(text, result.Text);
    }

    [Fact]
    public void Second_run_changes_nothing_and_adds_no_notes()
    {
        var before = File(
            "        a.Begin(SpriteBlendMode.Additive, SpriteSortMode.Deferred, SaveStateMode.SaveState);\n" +
            "        b.Begin(SpriteBlendMode.AlphaBlend);\n" +
            "        c.Begin(mode);\n" +
            "        SpriteBlendMode mode;\n");

        var first = Run(before);
        var secondContext = Context(first.Text);
        var second = _rewriter.Rewrite(secondContext);

        Assert.NotEqual(before, first.Text);
        Assert.Same(first.Text, second.Text);
        Assert.False(second.Changed(secondContext));
        Assert.Empty(second.Notes);
    }

    [Fact]
    public void Engine_runs_the_rewriter_only_when_its_rule_is_enabled()
    {
        var registry = new RewriterRegistry();
        registry.Register(_rewriter);
        var engine = new SourceRewriteEngine(registry);
        var text = File("        batch.Begin(SpriteBlendMode.None);\n");

        var off = engine.Rewrite("Game1.cs", text, _symbols, new HashSet<string>());
        var on = engine.Rewrite("Game1.cs", text, _symbols, new HashSet<string> { "XNA022" });

        Assert.False(off.Changed);
        Assert.Same(text, off.Text);
        Assert.True(on.Changed);
        Assert.Equal(File("        batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);\n"), on.Text);
        Assert.Equal(["XNA022"], on.AppliedRuleIds);
        Assert.Empty(on.Failures);
    }

    [Fact]
    public void Legacy_fixture_changes_only_the_two_begin_calls()
    {
        var path = Path.Combine(FixturePaths.Of("xna31-legacy"), "Game1.cs");
        var original = System.IO.File.ReadAllText(path);
        Assert.DoesNotContain('\r', original);

        var result = Run(original);

        var expected = original
            .Replace("spriteBatch.Begin(SpriteBlendMode.AlphaBlend);", "spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);", StringComparison.Ordinal)
            .Replace(
                "            spriteBatch.Begin(\n                SpriteBlendMode.Additive,\n                SpriteSortMode.Deferred,\n                SaveStateMode.SaveState);",
                "            " + _stateMarker + "\n            spriteBatch.Begin(\n                SpriteSortMode.Deferred,\n                BlendState.Additive);",
                StringComparison.Ordinal);
        Assert.NotEqual(original, expected);
        Assert.Equal(expected, result.Text);
        Assert.DoesNotContain("SpriteBlendMode", result.Text);
        Assert.Equal(["XNA022", "XNA022", "XNA023"], result.Notes.Select(n => n.RuleId));
    }

    [Fact]
    public void Null_context_throws()
    {
        Assert.Throws<ArgumentNullException>(() => _rewriter.Rewrite(null!));
    }
}
