using System.Text;

namespace XnaReborn.Flash.Analyzer.As3;

/// <summary>
/// Turns ActionScript 3 source text into tokens. It never throws: text it cannot read becomes an
/// <see cref="As3TokenKind.Error"/> token. Concatenating every token's trivia and text reproduces the input exactly.
/// </summary>
public sealed class As3Lexer
{
    private static readonly HashSet<string> _keywords = new(StringComparer.Ordinal)
    {
        "as", "break", "case", "catch", "class", "const", "continue", "default", "delete", "do", "else", "extends",
        "false", "finally", "for", "function", "if", "implements", "import", "in", "instanceof", "interface",
        "internal", "is", "new", "null", "package", "private", "protected", "public", "return", "super", "switch",
        "this", "throw", "true", "try", "typeof", "use", "var", "void", "while", "with",
    };

    // Longest first, so ">>>=" wins over ">>>" and ">".
    private static readonly string[] _punctuators =
    {
        ">>>=", "===", "!==", ">>>", "<<=", ">>=", "...", "&&=", "||=", "==", "!=", "<=", ">=", "&&", "||", "++", "--",
        "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<", ">>", "::", "..", ".<",
        "{", "}", "(", ")", "[", "]", ";", ",", ".", "<", ">", "+", "-", "*", "/", "%", "&", "|", "^", "!", "~", "?", ":", "=", "@",
    };

    // Keywords after which a "/" starts a regular expression.
    private static readonly HashSet<string> _regexAfterKeyword = new(StringComparer.Ordinal)
    {
        "return", "typeof", "in", "is", "as", "case", "delete", "new", "throw", "void", "else", "do", "instanceof",
    };

    private readonly string _text;
    private int _pos;
    private int _line = 1;
    private int _column = 1;

    private As3Lexer(string text) => _text = text;

    /// <summary>Reads all tokens of <paramref name="source"/>. The last token is always <see cref="As3TokenKind.Eof"/>.</summary>
    /// <param name="source">ActionScript 3 source text.</param>
    /// <returns>The tokens in source order.</returns>
    public static IReadOnlyList<As3Token> Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new As3Lexer(source).Run();
    }

    /// <summary>True for reserved words of the language.</summary>
    /// <param name="word">An identifier-like word.</param>
    public static bool IsKeyword(string word) => _keywords.Contains(word);

    private List<As3Token> Run()
    {
        var tokens = new List<As3Token>();
        As3Token? previous = null;
        while (true)
        {
            var trivia = ReadTrivia();
            var newline = trivia.Any(t => t.Text.Contains('\n') || t.Text.Contains('\r'));
            var startPos = _pos;
            var line = _line;
            var column = _column;
            if (_pos >= _text.Length)
            {
                tokens.Add(new As3Token(As3TokenKind.Eof, string.Empty, new SourceSpan(_pos, 0, line, column), trivia, newline));
                return tokens;
            }

            var kind = ReadToken(RegexAllowed(previous));
            var text = _text.Substring(startPos, _pos - startPos);
            var token = new As3Token(kind, text, new SourceSpan(startPos, text.Length, line, column), trivia, newline);
            tokens.Add(token);
            previous = token;
        }
    }

    private static bool RegexAllowed(As3Token? previous)
    {
        if (previous is null)
        {
            return true;
        }

        return previous.Kind switch
        {
            As3TokenKind.Identifier or As3TokenKind.Number or As3TokenKind.String or As3TokenKind.Regex or As3TokenKind.Xml => false,
            As3TokenKind.Keyword => _regexAfterKeyword.Contains(previous.Text),
            As3TokenKind.Punctuator => previous.Text is not (")" or "]" or "}" or "++" or "--"),
            _ => true,
        };
    }

    private List<Trivia> ReadTrivia()
    {
        var list = new List<Trivia>();
        while (_pos < _text.Length)
        {
            var c = _text[_pos];
            var start = _pos;
            if (char.IsWhiteSpace(c) || c == '\uFEFF')
            {
                while (_pos < _text.Length && (char.IsWhiteSpace(_text[_pos]) || _text[_pos] == '\uFEFF'))
                {
                    Advance();
                }

                list.Add(new Trivia(TriviaKind.Whitespace, _text[start.._pos]));
            }
            else if (c == '/' && Peek(1) == '/')
            {
                while (_pos < _text.Length && _text[_pos] is not ('\n' or '\r'))
                {
                    Advance();
                }

                list.Add(new Trivia(TriviaKind.LineComment, _text[start.._pos]));
            }
            else if (c == '/' && Peek(1) == '*')
            {
                Advance();
                Advance();
                while (_pos < _text.Length && !(_text[_pos] == '*' && Peek(1) == '/'))
                {
                    Advance();
                }

                if (_pos < _text.Length)
                {
                    Advance();
                    Advance();
                }

                list.Add(new Trivia(TriviaKind.BlockComment, _text[start.._pos]));
            }
            else
            {
                break;
            }
        }

        return list;
    }

    private As3TokenKind ReadToken(bool regexAllowed)
    {
        var c = _text[_pos];
        if (IsIdentStart(c))
        {
            var start = _pos;
            while (_pos < _text.Length && IsIdentPart(_text[_pos]))
            {
                Advance();
            }

            return _keywords.Contains(_text[start.._pos]) ? As3TokenKind.Keyword : As3TokenKind.Identifier;
        }

        if (char.IsAsciiDigit(c) || (c == '.' && char.IsAsciiDigit(Peek(1))))
        {
            ReadNumber();
            return As3TokenKind.Number;
        }

        if (c is '"' or '\'')
        {
            return ReadString(c);
        }

        if (c == '/' && regexAllowed)
        {
            var kind = TryReadRegex();
            if (kind is not null)
            {
                return kind.Value;
            }
        }

        if (c == '<' && regexAllowed && (char.IsAsciiLetter(Peek(1)) || Peek(1) is '!' or '?' or '>'))
        {
            var kind = TryReadXml();
            if (kind is not null)
            {
                return kind.Value;
            }
        }

        foreach (var p in _punctuators)
        {
            if (string.CompareOrdinal(_text, _pos, p, 0, p.Length) == 0)
            {
                for (var i = 0; i < p.Length; i++)
                {
                    Advance();
                }

                return As3TokenKind.Punctuator;
            }
        }

        Advance();
        return As3TokenKind.Error;
    }

    private void ReadNumber()
    {
        if (_text[_pos] == '0' && Peek(1) is 'x' or 'X')
        {
            Advance();
            Advance();
            while (_pos < _text.Length && char.IsAsciiHexDigit(_text[_pos]))
            {
                Advance();
            }

            return;
        }

        while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
        {
            Advance();
        }

        if (_pos < _text.Length && _text[_pos] == '.' && Peek(1) != '.' && Peek(1) != '<' && !IsIdentStart(Peek(1)))
        {
            Advance();
            while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
            {
                Advance();
            }
        }

        if (_pos < _text.Length && _text[_pos] is 'e' or 'E')
        {
            var save = (_pos, _line, _column);
            Advance();
            if (_pos < _text.Length && _text[_pos] is '+' or '-')
            {
                Advance();
            }

            if (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
            {
                while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
                {
                    Advance();
                }
            }
            else
            {
                (_pos, _line, _column) = save;
            }
        }
    }

    private As3TokenKind ReadString(char quote)
    {
        Advance();
        while (_pos < _text.Length)
        {
            var c = _text[_pos];
            if (c == quote)
            {
                Advance();
                return As3TokenKind.String;
            }

            if (c is '\n' or '\r')
            {
                return As3TokenKind.Error;
            }

            if (c == '\\' && _pos + 1 < _text.Length)
            {
                Advance();
            }

            Advance();
        }

        return As3TokenKind.Error;
    }

    private As3TokenKind? TryReadRegex()
    {
        var i = _pos + 1;
        var inClass = false;
        while (i < _text.Length)
        {
            var c = _text[i];
            if (c is '\n' or '\r')
            {
                return null;
            }

            if (c == '\\')
            {
                i += 2;
                continue;
            }

            if (c == '[')
            {
                inClass = true;
            }
            else if (c == ']')
            {
                inClass = false;
            }
            else if (c == '/' && !inClass)
            {
                i++;
                while (i < _text.Length && char.IsAsciiLetter(_text[i]))
                {
                    i++;
                }

                AdvanceTo(i);
                return As3TokenKind.Regex;
            }

            i++;
        }

        return null;
    }

    private As3TokenKind? TryReadXml()
    {
        var i = _pos;
        var depth = 0;
        while (i < _text.Length)
        {
            if (_text[i] != '<')
            {
                i++;
                continue;
            }

            if (string.CompareOrdinal(_text, i, "<!--", 0, 4) == 0)
            {
                var end = _text.IndexOf("-->", i, StringComparison.Ordinal);
                if (end < 0)
                {
                    return null;
                }

                i = end + 3;
            }
            else if (string.CompareOrdinal(_text, i, "<![CDATA[", 0, 9) == 0)
            {
                var end = _text.IndexOf("]]>", i, StringComparison.Ordinal);
                if (end < 0)
                {
                    return null;
                }

                i = end + 3;
            }
            else if (string.CompareOrdinal(_text, i, "<?", 0, 2) == 0)
            {
                var end = _text.IndexOf("?>", i, StringComparison.Ordinal);
                if (end < 0)
                {
                    return null;
                }

                i = end + 2;
            }
            else
            {
                var closing = i + 1 < _text.Length && _text[i + 1] == '/';
                var j = i + 1;
                char quote = '\0';
                while (j < _text.Length && (quote != '\0' || _text[j] != '>'))
                {
                    if (quote != '\0')
                    {
                        quote = _text[j] == quote ? '\0' : quote;
                    }
                    else if (_text[j] is '"' or '\'')
                    {
                        quote = _text[j];
                    }

                    j++;
                }

                if (j >= _text.Length)
                {
                    return null;
                }

                var selfClosing = _text[j - 1] == '/';
                depth += closing ? -1 : (selfClosing ? 0 : 1);
                i = j + 1;
            }

            if (depth <= 0)
            {
                AdvanceTo(i);
                return As3TokenKind.Xml;
            }
        }

        return null;
    }

    private void AdvanceTo(int target)
    {
        while (_pos < target)
        {
            Advance();
        }
    }

    private void Advance()
    {
        if (_text[_pos] == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }

        _pos++;
    }

    private char Peek(int offset) => _pos + offset < _text.Length ? _text[_pos + offset] : '\0';

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c is '_' or '$';

    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';

    /// <summary>Joins tokens back into the source text they were read from.</summary>
    /// <param name="tokens">Tokens from <see cref="Tokenize"/>.</param>
    public static string Reassemble(IEnumerable<As3Token> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var sb = new StringBuilder();
        foreach (var token in tokens)
        {
            foreach (var t in token.LeadingTrivia)
            {
                sb.Append(t.Text);
            }

            sb.Append(token.Text);
        }

        return sb.ToString();
    }
}
