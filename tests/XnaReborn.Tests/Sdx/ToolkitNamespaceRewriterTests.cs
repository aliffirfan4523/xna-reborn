using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;

namespace XnaReborn.Tests.Sdx;

/// <summary>Task 5.2: the namespace rewriter changes usings, aliases and qualified names, merges duplicates and leaves other SharpDX namespaces alone.</summary>
[Trait("Category", "Sdx")]
public sealed class ToolkitNamespaceRewriterTests
{
    private static readonly string[] _symbols = ["DEBUG", "TRACE"];

    private static SourceRewriteResult Run(string ruleId, string text) =>
        new ToolkitNamespaceRewriter(ruleId).Rewrite(new SourceRewriteContext("Game1.cs", text, _symbols));

    [Theory]
    [InlineData("SDX020", "SharpDX.Toolkit", "Microsoft.Xna.Framework")]
    [InlineData("SDX021", "SharpDX.Toolkit.Graphics", "Microsoft.Xna.Framework.Graphics")]
    [InlineData("SDX022", "SharpDX.Toolkit.Content", "Microsoft.Xna.Framework.Content")]
    [InlineData("SDX023", "SharpDX", "Microsoft.Xna.Framework")]
    [InlineData("SDX024", "SharpDX.Toolkit.Input", "Microsoft.Xna.Framework.Input")]
    public void A_using_directive_is_rewritten(string ruleId, string from, string to)
    {
        var result = Run(ruleId, $"using System;\nusing {from};\n\nclass C\n{{\n}}\n");

        Assert.Equal($"using System;\nusing {to};\n\nclass C\n{{\n}}\n", result.Text);
        var note = Assert.Single(result.Notes);
        Assert.Equal(ruleId, note.RuleId);
        Assert.False(note.NeedsManualWork);
    }

    [Fact]
    public void A_using_alias_to_a_namespace_or_a_type_is_rewritten()
    {
        var result = Run("SDX021", "using Gfx = SharpDX.Toolkit.Graphics;\nusing Sprite = SharpDX.Toolkit.Graphics.SpriteBatch;\nclass C { }\n");

        Assert.Equal("using Gfx = Microsoft.Xna.Framework.Graphics;\nusing Sprite = Microsoft.Xna.Framework.Graphics.SpriteBatch;\nclass C { }\n", result.Text);
    }

    [Fact]
    public void Fully_qualified_type_names_are_rewritten_in_code()
    {
        var code = "class C\n{\n    SharpDX.Toolkit.Graphics.SpriteBatch batch = new SharpDX.Toolkit.Graphics.SpriteBatch(null);\n    int Size() => SharpDX.Toolkit.Graphics.SpriteSortMode.Deferred.GetHashCode();\n}\n";

        var result = Run("SDX021", code);

        Assert.Equal(code.Replace("SharpDX.Toolkit.Graphics", "Microsoft.Xna.Framework.Graphics"), result.Text);
    }

    [Fact]
    public void Qualified_math_types_are_rewritten_and_other_sharpdx_children_are_not()
    {
        var result = Run("SDX023", "class C\n{\n    SharpDX.Vector3 v = SharpDX.Vector3.Zero;\n    SharpDX.Direct3D11.Device d;\n    SharpDX.MathUtil m;\n}\n");

        Assert.Equal("class C\n{\n    Microsoft.Xna.Framework.Vector3 v = Microsoft.Xna.Framework.Vector3.Zero;\n    SharpDX.Direct3D11.Device d;\n    SharpDX.MathUtil m;\n}\n", result.Text);
    }

    [Fact]
    public void Sdx020_leaves_the_graphics_namespace_to_sdx021()
    {
        var text = "using SharpDX.Toolkit.Graphics;\nclass C { SharpDX.Toolkit.Graphics.SpriteBatch b; }\n";

        Assert.Equal(text, Run("SDX020", text).Text);
    }

    [Fact]
    public void Other_sharpdx_namespaces_are_left_alone()
    {
        var text = "using SharpDX.Direct3D11;\nusing SharpDX.DXGI;\nusing SharpDX.Direct2D1;\nusing SharpDX.XAudio2;\nclass C { }\n";

        foreach (var ruleId in ToolkitNamespaceRewriter.RuleIds())
        {
            var result = Run(ruleId, text);
            Assert.Equal(text, result.Text);
            Assert.Empty(result.Notes);
        }
    }

    [Fact]
    public void A_namespace_declared_by_the_user_is_left_alone()
    {
        var text = "namespace SharpDX.Toolkit\n{\n    class C { }\n}\n";

        Assert.Equal(text, Run("SDX020", text).Text);
    }

    [Fact]
    public void A_name_that_only_looks_like_the_namespace_is_left_alone()
    {
        var text = "using SharpDX.ToolkitExtras;\nclass C { }\n";

        Assert.Equal(text, Run("SDX020", text).Text);
    }

    [Fact]
    public void Two_usings_that_map_to_one_namespace_become_one()
    {
        var first = Run("SDX020", "using SharpDX;\nusing SharpDX.Toolkit;\n\nclass C { }\n");
        var second = Run("SDX023", first.Text);

        Assert.Equal("using Microsoft.Xna.Framework;\n\nclass C { }\n", second.Text);
        Assert.Contains("removed 1 using(s) that became duplicates", Assert.Single(second.Notes).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_using_that_was_already_imported_is_not_added_twice_whatever_the_order()
    {
        var before = Run("SDX020", "using Microsoft.Xna.Framework;\nusing SharpDX.Toolkit;\nclass C { }\n");
        var after = Run("SDX020", "using SharpDX.Toolkit;\nusing Microsoft.Xna.Framework;\nclass C { }\n");

        Assert.Equal("using Microsoft.Xna.Framework;\nclass C { }\n", before.Text);
        Assert.Equal("using Microsoft.Xna.Framework;\nclass C { }\n", after.Text);
    }

    [Fact]
    public void A_duplicate_the_author_already_had_is_not_removed()
    {
        var text = "using System;\nusing System;\nusing SharpDX.Toolkit;\nclass C { }\n";

        Assert.Equal("using System;\nusing System;\nusing Microsoft.Xna.Framework;\nclass C { }\n", Run("SDX020", text).Text);
    }

    [Fact]
    public void Usings_inside_a_namespace_are_handled_in_their_own_scope()
    {
        var text = "using Microsoft.Xna.Framework;\nnamespace App\n{\n    using SharpDX.Toolkit;\n    class C { }\n}\n";

        Assert.Equal("using Microsoft.Xna.Framework;\nnamespace App\n{\n    using Microsoft.Xna.Framework;\n    class C { }\n}\n", Run("SDX020", text).Text);
    }

    [Fact]
    public void Comments_line_endings_and_indentation_are_kept()
    {
        var text = "\uFEFF// header\r\nusing System; // system\r\nusing SharpDX.Toolkit; // the game\r\n\r\n#if DEBUG\r\n    // debug only\r\n#endif\r\nclass C\r\n{\r\n    SharpDX.Toolkit.GameTime t; /* time */\r\n}\r\n";

        var result = Run("SDX020", text);

        Assert.Equal(text.Replace("SharpDX.Toolkit", "Microsoft.Xna.Framework"), result.Text);
    }

    [Fact]
    public void A_second_run_changes_nothing()
    {
        var once = Run("SDX021", "using SharpDX.Toolkit.Graphics;\nclass C { SharpDX.Toolkit.Graphics.SpriteBatch b; }\n");

        var twice = Run("SDX021", once.Text);

        Assert.Equal(once.Text, twice.Text);
        Assert.Empty(twice.Notes);
    }

    [Fact]
    public void Code_is_read_with_the_original_symbols_so_an_active_branch_is_rewritten_and_an_inactive_one_is_not()
    {
        var text = "#if DEBUG\nusing SharpDX.Toolkit;\n#endif\n#if NETFX_CORE\nusing SharpDX.Toolkit;\n#endif\nclass C { }\n";

        Assert.Equal("#if DEBUG\nusing Microsoft.Xna.Framework;\n#endif\n#if NETFX_CORE\nusing SharpDX.Toolkit;\n#endif\nclass C { }\n", Run("SDX020", text).Text);
    }

    [Fact]
    public void An_unknown_rule_id_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new ToolkitNamespaceRewriter("SDX099"));
    }
}
