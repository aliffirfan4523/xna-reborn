namespace XnaReborn.Flash.Analyzer.As3;

/// <summary>What kind of text a <see cref="Trivia"/> holds.</summary>
public enum TriviaKind
{
    /// <summary>Spaces, tabs and line breaks.</summary>
    Whitespace,

    /// <summary>A comment that starts with two slashes and ends at the line break (the break is not included).</summary>
    LineComment,

    /// <summary>A comment between slash-star and star-slash.</summary>
    BlockComment,
}

/// <summary>Text between tokens that the parser ignores but the converter keeps.</summary>
/// <param name="Kind">Whitespace or comment.</param>
/// <param name="Text">The exact source text.</param>
public sealed record Trivia(TriviaKind Kind, string Text);

/// <summary>The kinds of token the lexer produces.</summary>
public enum As3TokenKind
{
    /// <summary>A name that is not a reserved word.</summary>
    Identifier,

    /// <summary>A reserved word such as <c>class</c> or <c>if</c>.</summary>
    Keyword,

    /// <summary>A decimal, hexadecimal or exponent number.</summary>
    Number,

    /// <summary>A quoted string, including its quotes.</summary>
    String,

    /// <summary>A regular expression literal, including slashes and flags.</summary>
    Regex,

    /// <summary>An XML (E4X) literal, kept as one opaque run of text.</summary>
    Xml,

    /// <summary>An operator or punctuation mark.</summary>
    Punctuator,

    /// <summary>Text the lexer could not read, such as an unterminated string.</summary>
    Error,

    /// <summary>The end of the file. It carries the trailing trivia.</summary>
    Eof,
}

/// <summary>One token with the trivia in front of it.</summary>
/// <param name="Kind">The token kind.</param>
/// <param name="Text">The exact source text of the token.</param>
/// <param name="Span">Where the token is in the source.</param>
/// <param name="LeadingTrivia">Whitespace and comments between the previous token and this one.</param>
/// <param name="NewlineBefore">True when a line break sits in the leading trivia. Used for automatic semicolon insertion.</param>
public sealed record As3Token(
    As3TokenKind Kind,
    string Text,
    SourceSpan Span,
    IReadOnlyList<Trivia> LeadingTrivia,
    bool NewlineBefore);
