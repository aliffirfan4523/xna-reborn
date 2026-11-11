using XnaReborn.Flash.Analyzer.As3;

namespace XnaReborn.Flash.Tests;

public class As3LexerTests
{
    private static List<(As3TokenKind Kind, string Text)> Lex(string s) =>
        As3Lexer.Tokenize(s).Where(t => t.Kind != As3TokenKind.Eof).Select(t => (t.Kind, t.Text)).ToList();

    [Theory]
    [MemberData(nameof(FlashFixtures.AsFileRows), MemberType = typeof(FlashFixtures))]
    public void Fixture_files_round_trip_exactly(string relative)
    {
        var text = File.ReadAllText(Path.Combine(FlashFixtures.Root, relative));
        Assert.Equal(text, As3Lexer.Reassemble(As3Lexer.Tokenize(text)));
    }

    [Fact]
    public void Operators_use_the_longest_match()
    {
        var kinds = Lex("a >>>= b === c !== d ... e :: f .. g .< h @").Select(t => t.Text).ToList();
        Assert.Equal(["a", ">>>=", "b", "===", "c", "!==", "d", "...", "e", "::", "f", "..", "g", ".<", "h", "@"], kinds);
    }

    [Fact]
    public void Numbers_cover_hex_exponent_and_leading_dot()
    {
        var numbers = Lex("0xFF 1.5e3 .5 10 2E-2").Where(t => t.Kind == As3TokenKind.Number).Select(t => t.Text);
        Assert.Equal(["0xFF", "1.5e3", ".5", "10", "2E-2"], numbers);
    }

    [Fact]
    public void Slash_after_an_operand_is_division_and_after_an_operator_is_a_regex()
    {
        Assert.Equal(As3TokenKind.Punctuator, Lex("a / b")[1].Kind);
        Assert.Equal(As3TokenKind.Punctuator, Lex("(a) / b")[3].Kind);
        Assert.Equal(As3TokenKind.Regex, Lex("x = /ab+c/gi;")[2].Kind);
        Assert.Equal(As3TokenKind.Regex, Lex("return /a[/]b/;")[1].Kind);
    }

    [Fact]
    public void Comments_are_trivia_and_set_the_newline_flag()
    {
        var tokens = As3Lexer.Tokenize("a // note\nb /* x */ c");
        Assert.True(tokens[1].NewlineBefore);
        Assert.False(tokens[2].NewlineBefore);
        Assert.Contains(tokens[1].LeadingTrivia, t => t.Kind == TriviaKind.LineComment);
        Assert.Contains(tokens[2].LeadingTrivia, t => t.Kind == TriviaKind.BlockComment);
    }

    [Fact]
    public void Unterminated_string_becomes_an_error_token()
    {
        var tokens = Lex("var s = \"abc\nvar t");
        Assert.Contains(tokens, t => t.Kind == As3TokenKind.Error);
    }

    [Fact]
    public void Xml_literal_is_one_token()
    {
        var tokens = Lex("var x = <a b=\"1\"><c/>text</a>;");
        Assert.Contains(tokens, t => t.Kind == As3TokenKind.Xml && t.Text == "<a b=\"1\"><c/>text</a>");
    }

    [Fact]
    public void Less_than_after_an_operand_is_not_xml()
    {
        Assert.DoesNotContain(Lex("if (i <n) {}"), t => t.Kind == As3TokenKind.Xml);
    }

    [Fact]
    public void Random_text_never_throws_and_still_round_trips()
    {
        var rng = new Random(12345);
        var alphabet = "abc012 \t\n\r\"'/*<>{}()[]\\.:;,+-=!&|@#é";
        for (var i = 0; i < 500; i++)
        {
            var s = new string(Enumerable.Range(0, rng.Next(0, 80)).Select(_ => alphabet[rng.Next(alphabet.Length)]).ToArray());
            var tokens = As3Lexer.Tokenize(s);
            Assert.Equal(s, As3Lexer.Reassemble(tokens));
            Assert.Equal(As3TokenKind.Eof, tokens[^1].Kind);
        }
    }
}
