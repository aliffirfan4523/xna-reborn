using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XnaReborn.Migrator.Source;

namespace XnaReborn.Tests.Migrator;

/// <summary>Marker comments go above the statement or member, keep the indentation and line ending, and are idempotent.</summary>
[Trait("Category", "Migrator")]
public class MarkerInserterTests
{
    private const string _source =
        "using System;\n" +
        "\n" +
        "class Game\n" +
        "{\n" +
        "    int _count;\n" +
        "\n" +
        "    void Draw()\n" +
        "    {\n" +
        "        // a comment\n" +
        "        var s = \"text\";\n" +
        "        if (_count > 0)\n" +
        "        {\n" +
        "            batch.Begin(\n" +
        "                SpriteBlendMode.AlphaBlend);\n" +
        "        }\n" +
        "    }\n" +
        "}\n";

    private static int At(string text, string needle) => text.IndexOf(needle, StringComparison.Ordinal);

    [Fact]
    public void Marker_goes_above_the_statement_with_its_indentation()
    {
        var result = MarkerInserter.InsertMarker(_source, At(_source, "SpriteBlendMode"), "XNA010", "Check the blend mode.");

        var expected = _source.Replace(
            "            batch.Begin(\n",
            "            // XNAREBORN: XNA010: Check the blend mode.\n            batch.Begin(\n");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Marker_goes_above_the_member()
    {
        var result = MarkerInserter.InsertMarker(_source, At(_source, "int _count") + 4, "XNA020", "Field moved.");

        Assert.Equal(_source.Replace("    int _count;", "    // XNAREBORN: XNA020: Field moved.\n    int _count;"), result);
    }

    [Fact]
    public void Position_on_a_method_name_marks_the_method()
    {
        var result = MarkerInserter.InsertMarker(_source, At(_source, "Draw"), "R", "m");

        Assert.Equal(_source.Replace("    void Draw()", "    // XNAREBORN: R: m\n    void Draw()"), result);
    }

    [Fact]
    public void Position_on_a_brace_marks_the_owner_not_the_block()
    {
        var result = MarkerInserter.InsertMarker(_source, At(_source, "{\n            batch"), "R", "m");

        Assert.Contains("        // XNAREBORN: R: m\n        if (_count > 0)", result);
    }

    [Fact]
    public void Position_outside_statements_uses_its_own_line()
    {
        var result = MarkerInserter.InsertMarker(_source, At(_source, "System"), "R", "m");

        Assert.StartsWith("// XNAREBORN: R: m\nusing System;\n", result);
    }

    [Fact]
    public void Tabs_are_kept_as_indentation()
    {
        var text = "class A\n{\n\tvoid M()\n\t{\n\t\tFoo();\n\t}\n}\n";

        var result = MarkerInserter.InsertMarker(text, At(text, "Foo"), "R", "m");

        Assert.Contains("\t\t// XNAREBORN: R: m\n\t\tFoo();", result);
    }

    [Fact]
    public void Crlf_files_get_crlf_markers()
    {
        var text = _source.Replace("\n", "\r\n");

        var result = MarkerInserter.InsertMarker(text, At(text, "var s"), "R", "m");

        Assert.Equal(text.Replace("        var s", "        // XNAREBORN: R: m\r\n        var s"), result);
        Assert.DoesNotContain("\n", result.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void Lf_files_get_lf_markers()
    {
        var result = MarkerInserter.InsertMarker(_source, At(_source, "var s"), "R", "m");

        Assert.DoesNotContain('\r', result);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Inserting_twice_changes_nothing_the_second_time(string newLine)
    {
        var text = _source.Replace("\n", newLine);
        var once = MarkerInserter.InsertMarker(text, At(text, "var s"), "R", "m");

        var twice = MarkerInserter.InsertMarker(once, At(once, "var s"), "R", "m");

        Assert.Equal(once, twice);
        Assert.NotEqual(text, once);
    }

    [Fact]
    public void A_different_marker_is_added_next_to_an_existing_one()
    {
        var once = MarkerInserter.InsertMarker(_source, At(_source, "var s"), "R1", "first");

        var twice = MarkerInserter.InsertMarker(once, At(once, "var s"), "R2", "second");

        Assert.Contains("        // XNAREBORN: R1: first\n        // XNAREBORN: R2: second\n        var s", twice);
        Assert.Equal(twice, MarkerInserter.InsertMarker(twice, At(twice, "var s"), "R1", "first"));
    }

    [Fact]
    public void Newlines_in_the_message_become_spaces()
    {
        var result = MarkerInserter.InsertMarker(_source, At(_source, "var s"), "R", "line one\r\n  line two\nthree\n");

        Assert.Contains("// XNAREBORN: R: line one line two three\n", result);
        Assert.Equal(_source.Split('\n').Length + 1, result.Split('\n').Length);
    }

    [Theory]
    [InlineData("a comment")]
    [InlineData("\"text\"")]
    public void Position_inside_a_comment_or_string_changes_nothing(string needle)
    {
        var result = MarkerInserter.InsertMarker(_source, At(_source, needle) + 2, "R", "m");

        Assert.Equal(_source, result);
    }

    [Fact]
    public void Position_inside_a_block_comment_or_doc_comment_changes_nothing()
    {
        var text = "class A\n{\n    /// <summary>Doc.</summary>\n    /* block */\n    void M() { }\n}\n";

        Assert.Equal(text, MarkerInserter.InsertMarker(text, At(text, "Doc"), "R", "m"));
        Assert.Equal(text, MarkerInserter.InsertMarker(text, At(text, "block"), "R", "m"));
    }

    [Fact]
    public void Position_in_inactive_code_changes_nothing()
    {
        var text = "class A\n{\n#if NEVER\n    void M() { }\n#endif\n}\n";

        Assert.Equal(text, MarkerInserter.InsertMarker(text, At(text, "void M"), "R", "m"));
    }

    [Fact]
    public void Position_inside_an_interpolation_hole_marks_the_statement()
    {
        var text = "class A\n{\n    void M()\n    {\n        var s = $\"a {Foo()} b\";\n    }\n}\n";

        var result = MarkerInserter.InsertMarker(text, At(text, "Foo"), "R", "m");

        Assert.Contains("        // XNAREBORN: R: m\n        var s", result);
    }

    [Fact]
    public void Empty_text_and_end_of_text_are_safe()
    {
        Assert.Equal(string.Empty, MarkerInserter.InsertMarker(string.Empty, 0, "R", "m"));
        Assert.Contains("// XNAREBORN: R: m", MarkerInserter.InsertMarker(_source, _source.Length, "R", "m"));
    }

    [Fact]
    public void Bad_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => MarkerInserter.InsertMarker(null!, 0, "R", "m"));
        Assert.Throws<ArgumentNullException>(() => MarkerInserter.InsertMarker("x", 0, null!, "m"));
        Assert.Throws<ArgumentNullException>(() => MarkerInserter.InsertMarker("x", 0, "R", null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkerInserter.InsertMarker("x", -1, "R", "m"));
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkerInserter.InsertMarker("x", 2, "R", "m"));
    }

    [Fact]
    public void Node_helper_adds_an_indented_marker_line_and_is_idempotent()
    {
        var root = CSharpSyntaxTree.ParseText(_source).GetRoot();
        var statement = root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Single();

        var marked = MarkerInserter.WithMarker(statement, "R", "m");

        Assert.EndsWith("        // XNAREBORN: R: m\n        var s = \"text\";", marked.ToFullString().TrimEnd('\n'));
        Assert.Same(marked, MarkerInserter.WithMarker(marked, "R", "m"));
    }

    [Fact]
    public void Node_helper_uses_the_file_line_ending()
    {
        var text = _source.Replace("\n", "\r\n");
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        var statement = root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Single();

        var newRoot = root.ReplaceNode(statement, MarkerInserter.WithMarker(statement, "R", "m"));

        Assert.Equal(text.Replace("        var s", "        // XNAREBORN: R: m\r\n        var s"), newRoot.ToFullString());
    }
}
