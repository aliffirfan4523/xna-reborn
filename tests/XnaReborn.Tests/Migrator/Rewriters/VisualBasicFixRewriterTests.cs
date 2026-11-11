using XnaReborn.Core.Models;
using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;

namespace XnaReborn.Tests.Migrator.Rewriters;

/// <summary>Older-and-vb task 24: the Visual Basic ports of the render state and effect pass rewriters.</summary>
[Trait("Category", "Migrator")]
public sealed class VisualBasicFixRewriterTests
{
    private const string _imports = "Imports Microsoft.Xna.Framework.Graphics\n";

    private static SourceRewriteContext Vb(string text, IReadOnlyList<string>? implicitImports = null) =>
        new("Game1.vb", text, ["WINDOWS"], XnaPlatform.Windows, SourceLanguage.VisualBasic, implicitImports);

    private static string Body(string lines) => _imports + "Class C\n    Sub M()\n" + lines + "    End Sub\nEnd Class\n";

    private static string Run(ISourceRewriter rewriter, string text, IReadOnlyList<string>? implicitImports = null) =>
        rewriter.Rewrite(Vb(text, implicitImports)).Text;

    // ---------- render state ----------
    [Theory]
    [InlineData("device.RenderState.AlphaBlendEnable = True", "device.BlendState = BlendState.AlphaBlend")]
    [InlineData("device.RenderState.alphablendenable = false", "device.BlendState = BlendState.Opaque")]
    [InlineData("device.RenderState.DepthBufferEnable = True", "device.DepthStencilState = DepthStencilState.Default")]
    [InlineData("device.RenderState.DepthBufferEnable = False", "device.DepthStencilState = DepthStencilState.None")]
    [InlineData("device.RenderState.CullMode = CullMode.None", "device.RasterizerState = RasterizerState.CullNone")]
    [InlineData("GraphicsDevice.RenderState.CullMode = CullMode.CullClockwiseFace", "GraphicsDevice.RasterizerState = RasterizerState.CullClockwise")]
    [InlineData("device.RenderState.CullMode = CullMode.CullCounterClockwiseFace", "device.RasterizerState = RasterizerState.CullCounterClockwise")]
    public void Render_state_assignments_become_state_objects(string before, string after)
    {
        var result = new VisualBasicRenderStateRewriter().Rewrite(Vb(Body("        " + before + "\n")));

        Assert.Equal(Body("        " + after + "\n"), result.Text);
        Assert.DoesNotContain(result.Notes, n => n.NeedsManualWork);
        Assert.Single(result.Notes);
    }

    [Fact]
    public void Render_state_with_block_and_other_properties_only_get_a_marker()
    {
        var text = Body("        With device.RenderState\n            .CullMode = CullMode.None\n        End With\n        device.RenderState.FillMode = FillMode.WireFrame\n        device.RenderState.AlphaBlendEnable = flag\n");

        var result = new VisualBasicRenderStateRewriter().Rewrite(Vb(text));

        Assert.Equal(3, result.Text.Split('\n').Count(l => l.Contains("' XNAREBORN: XNA024:", StringComparison.Ordinal)));
        Assert.Equal(text, string.Join("\n", result.Text.Split('\n').Where(l => !l.Contains("XNAREBORN", StringComparison.Ordinal))));
        Assert.All(result.Notes, n => Assert.True(n.NeedsManualWork));
    }

    [Fact]
    public void Render_state_edit_and_marker_work_in_one_file_and_a_second_run_changes_nothing()
    {
        var text = Body("        device.RenderState.DepthBufferEnable = True\n        device.RenderState.PointSize = 2\n");
        var rewriter = new VisualBasicRenderStateRewriter();

        var once = Run(rewriter, text);

        Assert.Contains("        device.DepthStencilState = DepthStencilState.Default\n", once, StringComparison.Ordinal);
        Assert.Contains("device.RenderState.PointSize = 2", once, StringComparison.Ordinal);
        Assert.Equal(once, Run(rewriter, once));
    }

    [Fact]
    public void Render_state_with_a_comment_or_without_the_graphics_import_is_not_edited()
    {
        var comment = Body("        device.RenderState.CullMode = _\n            CullMode.None\n");
        var noImport = "Class C\n    Sub M()\n        device.RenderState.DepthBufferEnable = True\n    End Sub\nEnd Class\n";

        Assert.Contains("device.RenderState.CullMode", Run(new VisualBasicRenderStateRewriter(), comment), StringComparison.Ordinal);
        Assert.Equal(noImport, Run(new VisualBasicRenderStateRewriter(), noImport));
    }

    [Fact]
    public void Render_state_uses_the_project_level_import()
    {
        const string text = "Class C\n    Sub M()\n        device.RenderState.DepthBufferEnable = True\n    End Sub\nEnd Class\n";

        var result = Run(new VisualBasicRenderStateRewriter(), text, ["Microsoft.Xna.Framework.Graphics"]);

        Assert.Contains("device.DepthStencilState = DepthStencilState.Default", result, StringComparison.Ordinal);
    }

    [Fact]
    public void A_user_type_with_a_render_state_member_and_no_xna_import_is_left_alone()
    {
        const string text = "Class C\n    Sub M()\n        mine.RenderState.DepthBufferEnable = True\n    End Sub\nEnd Class\n";

        Assert.Equal(text, Run(new VisualBasicRenderStateRewriter(), text));
    }

    // ---------- effect pass ----------
    private const string _pattern =
        "        effect.Begin\n        For Each pass As EffectPass In effect.CurrentTechnique.Passes\n            pass.Begin()\n            DrawStuff()\n            pass.End()\n        Next\n        effect.End()\n";

    private const string _fixed =
        "        For Each pass As EffectPass In effect.CurrentTechnique.Passes\n            pass.Apply()\n            DrawStuff()\n        Next\n";

    [Theory]
    [InlineData("XNA039")]
    [InlineData("XNA040")]
    public void The_full_pattern_becomes_apply_for_both_rule_ids(string ruleId)
    {
        var result = new VisualBasicEffectPassRewriter(ruleId).Rewrite(Vb(Body(_pattern)));

        Assert.Equal(Body(_fixed), result.Text);
        Assert.Single(result.Notes);
        Assert.False(result.Notes[0].NeedsManualWork);
    }

    [Fact]
    public void Parentheses_case_save_state_and_inferred_loop_variable_are_accepted()
    {
        var text = Body("        Me.Effect.Begin(SaveStateMode.None)\n        For Each p In me.effect.currenttechnique.passes\n            p.begin\n            p.end()\n        Next\n        me.effect.end\n");

        var result = Run(new VisualBasicEffectPassRewriter("XNA040"), text);

        Assert.Equal(Body("        For Each p In me.effect.currenttechnique.passes\n            p.Apply()\n        Next\n"), result);
    }

    [Fact]
    public void Crlf_line_endings_are_kept()
    {
        var text = Body(_pattern).Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.Equal(Body(_fixed).Replace("\n", "\r\n", StringComparison.Ordinal), Run(new VisualBasicEffectPassRewriter("XNA040"), text));
    }

    [Fact]
    public void A_second_run_changes_nothing_and_the_two_rule_ids_do_not_double_edit()
    {
        var once = Run(new VisualBasicEffectPassRewriter("XNA039"), Body(_pattern));

        Assert.Equal(once, Run(new VisualBasicEffectPassRewriter("XNA040"), once));
        Assert.Equal(once, Run(new VisualBasicEffectPassRewriter("XNA039"), once));
    }

    [Theory]
    [InlineData("        effect.Begin\n        For Each pass As EffectPass In effect.CurrentTechnique.Passes\n            pass.Begin()\n            pass.End()\n        Next\n")]
    [InlineData("        effect.Begin() : effect.End()\n")]
    [InlineData("        effect.Begin\n        For Each pass As EffectPass In effect.CurrentTechnique.Passes\n            pass.Begin()\n            If x Then pass.End()\n        Next\n        effect.End()\n")]
    [InlineData("        effect.Begin\n        For Each pass As EffectPass In effect.CurrentTechnique.Passes\n            pass.Begin()\n            pass.End() ' done\n        Next\n        effect.End()\n")]
    [InlineData("        effect.Begin(1)\n        For Each pass As EffectPass In effect.CurrentTechnique.Passes\n            pass.Begin()\n            pass.End()\n        Next\n        effect.End()\n")]
    [InlineData("        effect.Begin\n        For Each pass As EffectPass In other.CurrentTechnique.Passes\n            pass.Begin()\n            pass.End()\n        Next\n        effect.End()\n")]
    public void Patterns_that_do_not_fit_keep_their_code_and_get_a_marker(string lines)
    {
        var text = Body(lines);

        var result = new VisualBasicEffectPassRewriter("XNA040").Rewrite(Vb(text));

        Assert.Contains("' XNAREBORN: XNA040:", result.Text, StringComparison.Ordinal);
        Assert.Equal(text, string.Join("\n", result.Text.Split('\n').Where(l => !l.Contains("XNAREBORN", StringComparison.Ordinal))));
    }

    [Fact]
    public void Begin_and_end_on_user_types_without_the_graphics_import_are_left_alone()
    {
        var text = "Class C\n    Sub M()\n" + _pattern + "    End Sub\nEnd Class\n";

        Assert.Equal(text, Run(new VisualBasicEffectPassRewriter("XNA040"), text));
    }

    [Fact]
    public void An_unrelated_begin_call_is_not_touched()
    {
        var text = Body("        timer.Begin()\n        timer.End()\n");

        Assert.Equal(text, Run(new VisualBasicEffectPassRewriter("XNA039"), text));
    }

    [Fact]
    public void An_unknown_rule_id_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new VisualBasicEffectPassRewriter("XNA041"));
    }
}
