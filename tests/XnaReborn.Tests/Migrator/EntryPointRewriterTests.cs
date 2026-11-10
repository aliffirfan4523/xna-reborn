using XnaReborn.Migrator.Source;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator;

/// <summary>Platform conditionals around the entry point are removed, everything else is left alone.</summary>
[Trait("Category", "Migrator")]
public class EntryPointRewriterTests
{
    private static readonly string[] _windows = ["WINDOWS", "DEBUG", "TRACE"];

    private readonly EntryPointRewriter _rewriter = new();

    private static string Template(string condition) =>
        "using System;\n" +
        "\n" +
        "namespace MyGame\n" +
        "{\n" +
        $"#if {condition}\n" +
        "    static class Program\n" +
        "    {\n" +
        "        static void Main(string[] args)\n" +
        "        {\n" +
        "            using (Game1 game = new Game1())\n" +
        "            {\n" +
        "                game.Run();\n" +
        "            }\n" +
        "        }\n" +
        "    }\n" +
        "#endif\n" +
        "}\n";

    private const string _unwrappedTemplate =
        "using System;\n" +
        "\n" +
        "namespace MyGame\n" +
        "{\n" +
        "    static class Program\n" +
        "    {\n" +
        "        static void Main(string[] args)\n" +
        "        {\n" +
        "            using (Game1 game = new Game1())\n" +
        "            {\n" +
        "                game.Run();\n" +
        "            }\n" +
        "        }\n" +
        "    }\n" +
        "}\n";

    [Fact]
    public void Xna_template_is_unwrapped_exactly()
    {
        var result = _rewriter.Rewrite(Template("WINDOWS || XBOX"), _windows);

        Assert.Equal(EntryPointStatus.Unwrapped, result.Status);
        Assert.Equal(_unwrappedTemplate, result.Text);
        Assert.Equal("Removed '#if WINDOWS || XBOX' around the entry point.", result.Note);
    }

    [Theory]
    [InlineData("WINDOWS")]
    [InlineData("XBOX || WINDOWS")]
    [InlineData("!XBOX360")]
    [InlineData("(WINDOWS || XBOX)")]
    [InlineData("WINDOWS && !XBOX360")]
    [InlineData("WINDOWS_PHONE || XBOX360 || WINDOWS")]
    public void Platform_only_conditions_are_unwrapped(string condition)
    {
        var result = _rewriter.Rewrite(Template(condition), ["WINDOWS", "XBOX", "ZUNE", "WINDOWS_PHONE"]);

        Assert.Equal(EntryPointStatus.Unwrapped, result.Status);
        Assert.Equal(_unwrappedTemplate, result.Text);
        Assert.Contains($"'#if {condition}'", result.Note);
    }

    [Fact]
    public void Crlf_line_endings_are_kept()
    {
        var input = Template("WINDOWS || XBOX").Replace("\n", "\r\n");

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Unwrapped, result.Status);
        Assert.Equal(_unwrappedTemplate.Replace("\n", "\r\n"), result.Text);
    }

    [Fact]
    public void Lf_line_endings_are_kept()
    {
        var result = _rewriter.Rewrite(Template("WINDOWS"), _windows);

        Assert.DoesNotContain('\r', result.Text);
    }

    [Fact]
    public void Other_conditionals_elsewhere_are_untouched()
    {
        var input =
            "#if DEBUG\n" +
            "// debug only\n" +
            "#endif\n" +
            Template("WINDOWS") +
            "#if TRACE\n" +
            "// trace only\n" +
            "#endif\n";
        var expected =
            "#if DEBUG\n" +
            "// debug only\n" +
            "#endif\n" +
            _unwrappedTemplate +
            "#if TRACE\n" +
            "// trace only\n" +
            "#endif\n";

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Unwrapped, result.Status);
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void Entry_point_hidden_by_symbols_is_no_entry_point()
    {
        var result = _rewriter.Rewrite(Template("XBOX"), ["WINDOWS"]);

        Assert.Equal(EntryPointStatus.NoEntryPoint, result.Status);
        Assert.Equal(Template("XBOX"), result.Text);
    }

    [Fact]
    public void Debug_guard_is_skipped_with_a_note()
    {
        var input = Template("DEBUG");

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Skipped, result.Status);
        Assert.Equal(input, result.Text);
        Assert.Equal("The entry point is inside '#if DEBUG'. Check the conditional by hand.", result.Note);
    }

    [Fact]
    public void Mixed_platform_and_custom_symbol_is_skipped()
    {
        var input = Template("WINDOWS && MYSYMBOL");

        var result = _rewriter.Rewrite(input, ["WINDOWS", "MYSYMBOL"]);

        Assert.Equal(EntryPointStatus.Skipped, result.Status);
        Assert.Equal(input, result.Text);
    }

    [Fact]
    public void Unparsable_condition_is_skipped()
    {
        var input = Template("WINDOWS ||");

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Skipped, result.Status);
        Assert.Equal(input, result.Text);
    }

    [Fact]
    public void Else_branch_is_skipped()
    {
        var input =
            "class Program\n" +
            "{\n" +
            "#if WINDOWS\n" +
            "    static void Main() { }\n" +
            "#else\n" +
            "    static void Main() { System.Console.WriteLine(); }\n" +
            "#endif\n" +
            "}\n";

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Skipped, result.Status);
        Assert.Equal(input, result.Text);
        Assert.Contains("'#if WINDOWS'", result.Note);
        Assert.Contains("by hand", result.Note);
    }

    [Fact]
    public void Elif_branch_is_skipped()
    {
        var input =
            "class Program\n" +
            "{\n" +
            "#if WINDOWS\n" +
            "    static void Main() { }\n" +
            "#elif XBOX\n" +
            "    static void Other() { }\n" +
            "#endif\n" +
            "}\n";

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Skipped, result.Status);
        Assert.Equal(input, result.Text);
    }

    [Fact]
    public void Nested_qualifying_regions_are_both_unwrapped()
    {
        var input =
            "#if WINDOWS || XBOX\n" +
            "class Program\n" +
            "{\n" +
            "#if !XBOX360\n" +
            "    static void Main() { }\n" +
            "#endif\n" +
            "}\n" +
            "#endif\n";
        var expected =
            "class Program\n" +
            "{\n" +
            "    static void Main() { }\n" +
            "}\n";

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Unwrapped, result.Status);
        Assert.Equal(expected, result.Text);
        Assert.Contains("'#if WINDOWS || XBOX'", result.Note);
        Assert.Contains("'#if !XBOX360'", result.Note);
    }

    [Fact]
    public void Nested_region_with_a_non_platform_condition_leaves_everything_unchanged()
    {
        var input =
            "#if WINDOWS\n" +
            "class Program\n" +
            "{\n" +
            "#if DEBUG\n" +
            "    static void Main() { }\n" +
            "#endif\n" +
            "}\n" +
            "#endif\n";

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Skipped, result.Status);
        Assert.Equal(input, result.Text);
        Assert.Contains("'#if DEBUG'", result.Note);
    }

    [Fact]
    public void Unguarded_main_is_unchanged()
    {
        var input = "class Program\n{\n    static void Main(string[] args) { }\n}\n";

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Unchanged, result.Status);
        Assert.Equal(input, result.Text);
        Assert.Null(result.Note);
    }

    [Fact]
    public void Main_with_an_unrelated_conditional_elsewhere_is_unchanged()
    {
        var input =
            "#if DEBUG\n" +
            "class Debugging { }\n" +
            "#endif\n" +
            "class Program\n{\n    static void Main() { }\n}\n";

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Unchanged, result.Status);
        Assert.Equal(input, result.Text);
    }

    [Fact]
    public void Top_level_statements_are_unchanged()
    {
        var input = "using System;\n\nConsole.WriteLine(\"hi\");\n";

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Unchanged, result.Status);
        Assert.Equal(input, result.Text);
    }

    [Fact]
    public void Game_class_has_no_entry_point()
    {
        var input =
            "using Microsoft.Xna.Framework;\n" +
            "\n" +
            "public class Game1 : Game\n" +
            "{\n" +
            "    protected override void Update(GameTime gameTime) { }\n" +
            "}\n";

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.NoEntryPoint, result.Status);
        Assert.Equal(input, result.Text);
    }

    [Theory]
    [InlineData("class Program { void Main() { } }")]
    [InlineData("class Program { public void Main(string[] args) { } }")]
    public void Instance_main_is_not_an_entry_point(string input)
    {
        var result = _rewriter.Rewrite("#if WINDOWS\n" + input + "\n#endif\n", _windows);

        Assert.Equal(EntryPointStatus.NoEntryPoint, result.Status);
    }

    [Fact]
    public void Struct_entry_point_is_found()
    {
        var input = "#if WINDOWS\nstruct Program { static void Main() { } }\n#endif\n";

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Unwrapped, result.Status);
        Assert.Equal("struct Program { static void Main() { } }\n", result.Text);
    }

    [Fact]
    public void Rewriting_twice_changes_nothing_more()
    {
        var first = _rewriter.Rewrite(Template("WINDOWS || XBOX"), _windows);

        var second = _rewriter.Rewrite(first.Text, _windows);

        Assert.Equal(EntryPointStatus.Unchanged, second.Status);
        Assert.Equal(first.Text, second.Text);
    }

    [Fact]
    public void Byte_order_mark_is_kept()
    {
        var input = "\uFEFF" + Template("WINDOWS");

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Unwrapped, result.Status);
        Assert.Equal("\uFEFF" + _unwrappedTemplate, result.Text);
    }

    [Fact]
    public void Last_line_without_newline_is_handled()
    {
        var input = "#if WINDOWS\nclass P { static void Main() { } }\n#endif";

        var result = _rewriter.Rewrite(input, _windows);

        Assert.Equal(EntryPointStatus.Unwrapped, result.Status);
        Assert.Equal("class P { static void Main() { } }\n", result.Text);
    }

    [Fact]
    public void Empty_text_is_no_entry_point()
    {
        var result = _rewriter.Rewrite(string.Empty, _windows);

        Assert.Equal(EntryPointStatus.NoEntryPoint, result.Status);
        Assert.Equal(string.Empty, result.Text);
    }

    [Fact]
    public void Null_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => _rewriter.Rewrite(null!, _windows));
        Assert.Throws<ArgumentNullException>(() => _rewriter.Rewrite("class A { }", null!));
    }

    [Theory]
    [MemberData(nameof(FixturePaths.NameRows), MemberType = typeof(FixturePaths))]
    public void Fixture_program_files_do_not_change(string fixtureName)
    {
        var files = Directory.GetFiles(FixturePaths.Of(fixtureName), "Program.cs", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);

            var result = _rewriter.Rewrite(text, _windows);

            if (fixtureName == "xna31-rewritable")
            {
                // The demo fixture keeps the #if WINDOWS || XBOX wrapper on purpose, so it is unwrapped.
                Assert.Equal(EntryPointStatus.Unwrapped, result.Status);
                continue;
            }

            Assert.Equal(text, result.Text);
            Assert.NotEqual(EntryPointStatus.Unwrapped, result.Status);
        }
    }
}
