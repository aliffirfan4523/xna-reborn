using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;

namespace XnaReborn.Tests.Sdx;

/// <summary>Task 6.2: graphics device calls with a MonoGame form are rewritten. Names that do not map get a marker.</summary>
[Trait("Category", "Sdx")]
public sealed class ToolkitGraphicsRewriterTests
{
    private static readonly string[] _symbols = ["DEBUG", "TRACE"];

    private static string Wrap(string body, string usings = "using Microsoft.Xna.Framework.Graphics;\n") =>
        usings + "\nclass C\n{\n    GraphicsDevice GraphicsDevice;\n\n    void M()\n    {\n        " + body + "\n    }\n}\n";

    private static SourceRewriteResult Run(string ruleId, string text) =>
        new ToolkitGraphicsRewriter(ruleId).Rewrite(new SourceRewriteContext("Game1.cs", text, _symbols));

    // ---------- SDX040 state collections ----------
    [Theory]
    [InlineData("GraphicsDevice.BlendStates.Opaque", "BlendState.Opaque")]
    [InlineData("GraphicsDevice.BlendStates.AlphaBlend", "BlendState.AlphaBlend")]
    [InlineData("GraphicsDevice.BlendStates.Additive", "BlendState.Additive")]
    [InlineData("GraphicsDevice.BlendStates.NonPremultiplied", "BlendState.NonPremultiplied")]
    [InlineData("GraphicsDevice.SamplerStates.PointClamp", "SamplerState.PointClamp")]
    [InlineData("GraphicsDevice.SamplerStates.LinearWrap", "SamplerState.LinearWrap")]
    [InlineData("GraphicsDevice.SamplerStates.AnisotropicClamp", "SamplerState.AnisotropicClamp")]
    [InlineData("GraphicsDevice.DepthStencilStates.Default", "DepthStencilState.Default")]
    [InlineData("GraphicsDevice.DepthStencilStates.DepthRead", "DepthStencilState.DepthRead")]
    [InlineData("GraphicsDevice.DepthStencilStates.None", "DepthStencilState.None")]
    [InlineData("this.GraphicsDevice.BlendStates.Opaque", "BlendState.Opaque")]
    [InlineData("game.GraphicsDevice.SamplerStates.PointWrap", "SamplerState.PointWrap")]
    public void Known_state_names_are_rewritten(string before, string after)
    {
        var result = Run("SDX040", Wrap($"var s = {before};"));

        Assert.Equal(Wrap($"var s = {after};"), result.Text);
        Assert.False(Assert.Single(result.Notes).NeedsManualWork);
    }

    [Fact]
    public void The_full_name_is_used_when_the_graphics_namespace_is_not_imported()
    {
        var result = Run("SDX040", Wrap("var s = GraphicsDevice.BlendStates.Opaque;", usings: "using Microsoft.Xna.Framework;\n"));

        Assert.Contains("var s = Microsoft.Xna.Framework.Graphics.BlendState.Opaque;", result.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GraphicsDevice.BlendStates.Default")]
    [InlineData("GraphicsDevice.SamplerStates.Default")]
    [InlineData("GraphicsDevice.SamplerStates.LinearMirror")]
    [InlineData("GraphicsDevice.RasterizerStates.CullBack")]
    [InlineData("GraphicsDevice.RasterizerStates.WireFrame")]
    [InlineData("GraphicsDevice.BlendStates")]
    public void Names_that_do_not_map_get_a_marker_and_stay(string expression)
    {
        var result = Run("SDX040", Wrap($"var s = {expression};"));

        Assert.Equal(Wrap($"// XNAREBORN: SDX040: {ToolkitGraphicsRewriter.StateMarker}\n        var s = {expression};"), result.Text);
        Assert.True(Assert.Single(result.Notes).NeedsManualWork);
    }

    [Fact]
    public void State_collections_of_something_that_is_not_a_graphics_device_are_not_touched()
    {
        var text = Wrap("var s = device.BlendStates.Opaque; var t = BlendState.Opaque;");

        Assert.Equal(text, Run("SDX040", text).Text);
    }

    // ---------- SDX041 Set*State ----------
    [Theory]
    [InlineData("GraphicsDevice.SetBlendState(BlendState.AlphaBlend);", "GraphicsDevice.BlendState = BlendState.AlphaBlend;")]
    [InlineData("GraphicsDevice.SetDepthStencilState(DepthStencilState.None);", "GraphicsDevice.DepthStencilState = DepthStencilState.None;")]
    [InlineData("GraphicsDevice.SetRasterizerState(rasterizer);", "GraphicsDevice.RasterizerState = rasterizer;")]
    [InlineData("this.GraphicsDevice.SetBlendState(state);", "this.GraphicsDevice.BlendState = state;")]
    [InlineData("GraphicsDevice.SetBlendState(cond ? a : b);", "GraphicsDevice.BlendState = cond ? a : b;")]
    public void One_argument_set_calls_become_assignments(string before, string after)
    {
        var result = Run("SDX041", Wrap(before));

        Assert.Equal(Wrap(after), result.Text);
        Assert.False(Assert.Single(result.Notes).NeedsManualWork);
    }

    [Theory]
    [InlineData("GraphicsDevice.SetBlendState(state, 1f);")]
    [InlineData("GraphicsDevice.SetBlendState();")]
    [InlineData("GraphicsDevice.SetBlendState(state: state);")]
    [InlineData("device.SetBlendState(state);")]
    [InlineData("GraphicsDevice.SetSomething(state);")]
    public void Other_shapes_of_set_calls_are_not_touched(string call)
    {
        var text = Wrap(call);

        Assert.Equal(text, Run("SDX041", text).Text);
    }

    [Fact]
    public void A_set_call_whose_value_is_used_gets_a_marker()
    {
        var result = Run("SDX041", Wrap("var r = GraphicsDevice.SetBlendState(state);"));

        Assert.Contains("// XNAREBORN: SDX041:", result.Text, StringComparison.Ordinal);
        Assert.Contains("GraphicsDevice.SetBlendState(state)", result.Text, StringComparison.Ordinal);
    }

    // ---------- SDX042 render targets ----------
    [Fact]
    public void A_single_render_target_call_is_renamed()
    {
        var result = Run("SDX042", Wrap("GraphicsDevice.SetRenderTargets(target);"));

        Assert.Equal(Wrap("GraphicsDevice.SetRenderTarget(target);"), result.Text);
    }

    [Theory]
    [InlineData("GraphicsDevice.SetRenderTargets(depth, target);")]
    [InlineData("GraphicsDevice.SetRenderTargets();")]
    [InlineData("device.SetRenderTargets(target);")]
    public void Other_render_target_calls_are_not_touched(string call)
    {
        var text = Wrap(call);

        Assert.Equal(text, Run("SDX042", text).Text);
    }

    // ---------- SDX044 back buffer ----------
    [Theory]
    [InlineData("GraphicsDevice.BackBuffer.Width", "GraphicsDevice.PresentationParameters.BackBufferWidth")]
    [InlineData("GraphicsDevice.BackBuffer.Height", "GraphicsDevice.PresentationParameters.BackBufferHeight")]
    [InlineData("this.GraphicsDevice.BackBuffer.Width", "this.GraphicsDevice.PresentationParameters.BackBufferWidth")]
    public void Back_buffer_size_is_read_from_the_presentation_parameters(string before, string after)
    {
        var result = Run("SDX044", Wrap($"var w = {before};"));

        Assert.Equal(Wrap($"var w = {after};"), result.Text);
    }

    [Theory]
    [InlineData("GraphicsDevice.BackBuffer")]
    [InlineData("GraphicsDevice.BackBuffer.Format")]
    public void Other_uses_of_the_back_buffer_get_a_marker(string expression)
    {
        var result = Run("SDX044", Wrap($"var b = {expression};"));

        Assert.Contains("// XNAREBORN: SDX044:", result.Text, StringComparison.Ordinal);
        Assert.Contains($"var b = {expression};", result.Text, StringComparison.Ordinal);
    }

    // ---------- SDX060 ToDisposeContent ----------
    [Theory]
    [InlineData("var b = ToDisposeContent(new SpriteBatch(GraphicsDevice));", "var b = new SpriteBatch(GraphicsDevice);")]
    [InlineData("var b = this.ToDisposeContent(texture);", "var b = texture;")]
    [InlineData("ToDisposeContent(new Foo());", "new Foo();")]
    [InlineData("var b = ToDisposeContent<Texture2D>(Load());", "var b = Load();")]
    [InlineData("ToDisposeContent(new Foo()).Run();", "new Foo().Run();")]
    [InlineData("ToDisposeContent(a ?? b).Run();", "(a ?? b).Run();")]
    [InlineData("var b = ToDisposeContent(a ?? b);", "var b = a ?? b;")]
    public void ToDisposeContent_is_unwrapped(string before, string after)
    {
        var result = Run("SDX060", Wrap(before));

        Assert.Equal(Wrap(after), result.Text);
        var note = Assert.Single(result.Notes);
        Assert.False(note.NeedsManualWork);
        Assert.Contains("MonoGame does not", note.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_bare_ToDisposeContent_statement_of_a_variable_is_removed()
    {
        var result = Run("SDX060", Wrap("ToDisposeContent(texture);\n        var next = 1;"));

        Assert.Equal(Wrap("var next = 1;"), result.Text);
        Assert.Contains("MonoGame does not", Assert.Single(result.Notes).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("var b = ToDisposeContent(a, b);")]
    [InlineData("var b = ToDisposeContent();")]
    [InlineData("var b = other.ToDisposeContent(a);")]
    [InlineData("var b = DisposeContent(a);")]
    public void Other_shapes_of_ToDisposeContent_are_not_touched(string call)
    {
        var text = Wrap(call);

        Assert.Equal(text, Run("SDX060", text).Text);
    }

    // ---------- shared ----------
    [Fact]
    public void A_file_with_no_framework_context_is_not_touched()
    {
        var text = "class C\n{\n    void M() { GraphicsDevice.SetBlendState(s); var w = GraphicsDevice.BackBuffer.Width; ToDisposeContent(x); }\n}\n";

        foreach (var ruleId in ToolkitGraphicsRewriter.RuleIds)
        {
            Assert.Equal(text, Run(ruleId, text).Text);
        }
    }

    [Fact]
    public void A_second_run_changes_nothing()
    {
        var text = Wrap("GraphicsDevice.SetBlendState(GraphicsDevice.BlendStates.Default); var w = GraphicsDevice.BackBuffer;");
        var once = Run("SDX040", text);
        var twice = Run("SDX040", once.Text);

        Assert.Equal(once.Text, twice.Text);
    }

    [Fact]
    public void Comments_and_line_endings_are_kept()
    {
        var text = "\uFEFFusing Microsoft.Xna.Framework.Graphics;\r\nclass C\r\n{\r\n    GraphicsDevice GraphicsDevice;\r\n    void M()\r\n    {\r\n        // blend\r\n        GraphicsDevice.SetBlendState(s); /* now */\r\n    }\r\n}\r\n";

        var result = Run("SDX041", text);

        Assert.Equal(text.Replace("GraphicsDevice.SetBlendState(s);", "GraphicsDevice.BlendState = s;"), result.Text);
    }

    [Fact]
    public void An_unknown_rule_id_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new ToolkitGraphicsRewriter("SDX043"));
    }
}
