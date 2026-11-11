using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;

namespace XnaReborn.Tests.Sdx;

/// <summary>Task 7.1: the KeyboardManager is replaced by Keyboard.GetState(), or left with a marker when it is used in another way.</summary>
[Trait("Category", "Sdx")]
public sealed class ToolkitInputRewriterTests
{
    private static readonly string[] _symbols = ["DEBUG", "TRACE"];

    private static SourceRewriteResult Run(string text) =>
        new ToolkitInputRewriter().Rewrite(new SourceRewriteContext("Game1.cs", text, _symbols));

    private const string _before = """
        using Microsoft.Xna.Framework;
        using Microsoft.Xna.Framework.Input;

        class Game1 : Game
        {
            // Reads the keys.
            private KeyboardManager keyboard;
            private int score;

            public Game1()
            {
                keyboard = new KeyboardManager(this);
                score = 1;
            }

            protected override void Update(GameTime gameTime)
            {
                var state = keyboard.GetState();
                if (state.IsKeyDown(Keys.Escape))
                {
                    Exit();
                }
            }
        }
        """;

    private const string _after = """
        using Microsoft.Xna.Framework;
        using Microsoft.Xna.Framework.Input;

        class Game1 : Game
        {
            private int score;

            public Game1()
            {
                score = 1;
            }

            protected override void Update(GameTime gameTime)
            {
                var state = Keyboard.GetState();
                if (state.IsKeyDown(Keys.Escape))
                {
                    Exit();
                }
            }
        }
        """;

    [Fact]
    public void The_manager_field_its_construction_and_its_comment_go_and_GetState_becomes_static()
    {
        var result = Run(_before + "\n");

        Assert.Equal(_after + "\n", result.Text);
        var note = Assert.Single(result.Notes);
        Assert.Equal("SDX025", note.RuleId);
        Assert.False(note.NeedsManualWork);
    }

    [Fact]
    public void This_qualified_uses_and_a_field_initializer_are_handled()
    {
        var text = "using Microsoft.Xna.Framework.Input;\nclass G\n{\n    KeyboardManager keys = new KeyboardManager(null);\n    void U()\n    {\n        var a = this.keys.GetState();\n    }\n}\n";

        Assert.Equal("using Microsoft.Xna.Framework.Input;\nclass G\n{\n    void U()\n    {\n        var a = Keyboard.GetState();\n    }\n}\n", Run(text).Text);
    }

    [Fact]
    public void The_full_name_is_used_when_the_input_namespace_is_not_imported()
    {
        var text = "using Microsoft.Xna.Framework;\nclass G\n{\n    SharpDX.Toolkit.Input.KeyboardManager k;\n    void U() { var a = k.GetState(); }\n}\n";

        Assert.Equal("using Microsoft.Xna.Framework;\nclass G\n{\n    void U() { var a = Microsoft.Xna.Framework.Input.Keyboard.GetState(); }\n}\n", Run(text).Text);
    }

    [Theory]
    [InlineData("Use(keyboard);")]
    [InlineData("var k = keyboard;")]
    [InlineData("list.Add(keyboard);")]
    [InlineData("var s = keyboard.GetState(1);")]
    [InlineData("var s = keyboard.Other;")]
    [InlineData("return keyboard;")]
    public void A_manager_used_in_another_way_is_left_in_place_with_a_marker(string use)
    {
        var text = $"using Microsoft.Xna.Framework.Input;\nclass G\n{{\n    KeyboardManager keyboard;\n    void U()\n    {{\n        {use}\n    }}\n}}\n";

        var result = Run(text);

        Assert.Equal(text.Replace("    KeyboardManager keyboard;", "    // XNAREBORN: SDX025: " + ToolkitInputRewriter.MarkerMessage + "\n    KeyboardManager keyboard;"), result.Text);
        Assert.True(Assert.Single(result.Notes).NeedsManualWork);
    }

    [Fact]
    public void A_local_variable_or_parameter_of_the_same_name_is_not_a_use_of_the_field()
    {
        var text = "using Microsoft.Xna.Framework.Input;\nclass G\n{\n    KeyboardManager keyboard;\n    void U(object keyboard) { Use(keyboard); }\n    void V() { var keyboard = 1; Use(keyboard); var s = this.keyboard.GetState(); }\n}\n";

        var result = Run(text);

        Assert.DoesNotContain("KeyboardManager keyboard;", result.Text, StringComparison.Ordinal);
        Assert.Contains("void U(object keyboard) { Use(keyboard); }", result.Text, StringComparison.Ordinal);
        Assert.Contains("var s = Keyboard.GetState();", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_member_of_another_object_with_the_same_name_is_not_a_use_of_the_field()
    {
        var text = "using Microsoft.Xna.Framework.Input;\nclass G\n{\n    KeyboardManager keyboard;\n    void U(G other) { var a = other.keyboard; var s = keyboard.GetState(); }\n}\n";

        Assert.Contains("var s = Keyboard.GetState();", Run(text).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_field_of_another_type_is_not_touched()
    {
        var text = "using Microsoft.Xna.Framework.Input;\nclass KeyboardHelper { public int GetState() => 0; }\nclass G\n{\n    KeyboardHelper keyboard;\n    void U() { var s = keyboard.GetState(); }\n}\n";

        var result = Run(text);

        Assert.Equal(text, result.Text);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void Mouse_and_pointer_managers_are_not_rewritten()
    {
        var text = "using Microsoft.Xna.Framework.Input;\nclass G\n{\n    MouseManager mouse;\n    PointerManager pointer;\n    void U() { var m = mouse.GetState(); var p = pointer.GetState(); }\n}\n";

        Assert.Equal(text, Run(text).Text);
    }

    [Fact]
    public void Two_managers_in_one_class_are_handled_one_by_one()
    {
        var text = "using Microsoft.Xna.Framework.Input;\nclass G\n{\n    KeyboardManager a;\n    KeyboardManager b;\n    void U() { var s = a.GetState(); Use(b); }\n}\n";

        var result = Run(text);

        Assert.Contains("var s = Keyboard.GetState();", result.Text, StringComparison.Ordinal);
        Assert.Contains("    KeyboardManager b;", result.Text, StringComparison.Ordinal);
        Assert.Contains("// XNAREBORN: SDX025:", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyboardManager a;", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Line_endings_and_the_byte_order_mark_are_kept_and_a_second_run_changes_nothing()
    {
        var text = "\uFEFF" + _before.Replace("\n", "\r\n") + "\r\n";

        var once = Run(text);
        var twice = Run(once.Text);

        Assert.StartsWith("\uFEFFusing Microsoft.Xna.Framework;\r\n", once.Text, StringComparison.Ordinal);
        Assert.Equal((_after + "\n").Replace("\n", "\r\n"), once.Text.TrimStart('\uFEFF'));
        Assert.Equal(once.Text, twice.Text);
        Assert.Empty(twice.Notes);
    }
}
