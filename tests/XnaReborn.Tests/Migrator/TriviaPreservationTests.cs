using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XnaReborn.Migrator.Source;

namespace XnaReborn.Tests.Migrator;

/// <summary>A syntax rewriter that changes one identifier leaves every other line exactly as it was.</summary>
[Trait("Category", "Migrator")]
public class TriviaPreservationTests
{
    private const string _source =
        "// Copyright header\n" +
        "using System;\n" +
        "using Microsoft.Xna.Framework;\n" +
        "\n" +
        "namespace MyGame\n" +
        "{\n" +
        "    #region Fields\n" +
        "\n" +
        "    /// <summary>Main game.</summary>\n" +
        "    public class Game1   :   Game\n" +
        "    {\n" +
        "        int   OldName = 1;    // trailing comment\n" +
        "\n" +
        "\n" +
        "        /* block\n" +
        "           comment */\n" +
        "        void Update()\n" +
        "        {\n" +
        "#if DEBUG\n" +
        "            Console.WriteLine(\"debug\");\n" +
        "#endif\n" +
        "\tvar    x = OldName ;\t// tabs and odd spacing\n" +
        "            var s = @\"verbatim\n" +
        "   OldName\n" +
        "\";\n" +
        "        }\n" +
        "    }\n" +
        "\n" +
        "    #endregion\n" +
        "}\n";

    private sealed class RenameRewriter : CSharpSyntaxRewriter, ISourceRewriter
    {
        public string RuleId => "RENAME";

        public SourceRewriteResult Rewrite(SourceRewriteContext context)
        {
            var options = new CSharpParseOptions(preprocessorSymbols: context.OriginalSymbols);
            var root = CSharpSyntaxTree.ParseText(context.Text, options).GetRoot();
            return new SourceRewriteResult(Visit(root)!.ToFullString(), []);
        }

        public override SyntaxToken VisitToken(SyntaxToken token) =>
            token.IsKind(SyntaxKind.IdentifierToken) && token.ValueText == "OldName"
                ? SyntaxFactory.Identifier(token.LeadingTrivia, "NewName", token.TrailingTrivia)
                : token;
    }

    private static FileRewriteResult Run(string text, params string[] enabled)
    {
        var registry = new RewriterRegistry();
        registry.Register(new RenameRewriter());
        return new SourceRewriteEngine(registry).Rewrite("Game1.cs", text, ["DEBUG"], new HashSet<string>(enabled));
    }

    [Fact]
    public void Only_the_renamed_identifier_differs_line_by_line()
    {
        var result = Run(_source, "RENAME");

        Assert.True(result.Changed);
        var before = _source.Split('\n');
        var after = result.Text.Split('\n');
        Assert.Equal(before.Length, after.Length);
        var differing = Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();
        Assert.Equal(2, differing.Count);
        foreach (var i in differing)
        {
            Assert.Equal(before[i].Replace("OldName", "NewName"), after[i]);
        }
    }

    [Fact]
    public void Comments_blank_lines_directives_and_verbatim_strings_are_untouched()
    {
        var after = Run(_source, "RENAME").Text.Split('\n');

        Assert.Contains("// Copyright header", after);
        Assert.Contains("    #region Fields", after);
        Assert.Contains("    #endregion", after);
        Assert.Contains("#if DEBUG", after);
        Assert.Contains("#endif", after);
        Assert.Contains("        /* block", after);
        Assert.Contains("           comment */", after);
        Assert.Contains("   OldName", after);
        Assert.Contains("        int   NewName = 1;    // trailing comment", after);
        Assert.Contains("\tvar    x = NewName ;\t// tabs and odd spacing", after);
        Assert.Equal(_source.Split('\n').Count(l => l.Length == 0), after.Count(l => l.Length == 0));
    }

    [Fact]
    public void Crlf_text_keeps_every_line_ending()
    {
        var crlf = _source.Replace("\n", "\r\n");

        var result = Run(crlf, "RENAME");

        Assert.Equal(crlf.Replace("int   OldName", "int   NewName").Replace("x = OldName", "x = NewName"), result.Text);
    }

    [Fact]
    public void Mixed_line_endings_are_kept()
    {
        var mixed = "class A\r\n{\n    int OldName;\r\n\n}\r";

        var result = Run(mixed, "RENAME");

        Assert.Equal(mixed.Replace("OldName", "NewName"), result.Text);
    }

    [Fact]
    public void Text_without_a_final_newline_stays_without_one()
    {
        var text = "class A { int OldName; } // end";

        Assert.Equal("class A { int NewName; } // end", Run(text, "RENAME").Text);
    }

    [Fact]
    public void Untouched_file_survives_the_engine_unchanged()
    {
        var untouched = _source.Replace("OldName", "Other");

        var result = Run(untouched, "RENAME");

        Assert.False(result.Changed);
        Assert.Same(untouched, result.Text);
        Assert.Empty(result.AppliedRuleIds);
    }

    [Fact]
    public void Disabled_rewriter_leaves_the_file_alone()
    {
        var result = Run(_source);

        Assert.False(result.Changed);
        Assert.Same(_source, result.Text);
    }
}
