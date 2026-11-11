namespace XnaReborn.Flash.Analyzer.As3;

/// <summary>
/// A hand-written recursive-descent parser for ActionScript 3. It never throws on bad input: a syntax problem becomes an
/// <see cref="As3Diagnostic"/> and the parser skips to the next statement or member. Constructs the converter does not support
/// (E4X, <c>with</c>, namespaces, <c>include</c>) become unsupported nodes that keep their original text.
/// </summary>
public sealed class As3Parser
{
    private const int _maxDepth = 200;

    private static readonly string[] _assignmentOperators =
    {
        "=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=", ">>>=", "&&=", "||=",
    };

    private static readonly Dictionary<string, int> _binaryPrecedence = new(StringComparer.Ordinal)
    {
        ["||"] = 1,
        ["&&"] = 2,
        ["|"] = 3,
        ["^"] = 4,
        ["&"] = 5,
        ["=="] = 6,
        ["!="] = 6,
        ["==="] = 6,
        ["!=="] = 6,
        ["<"] = 7,
        [">"] = 7,
        ["<="] = 7,
        [">="] = 7,
        ["as"] = 7,
        ["is"] = 7,
        ["instanceof"] = 7,
        ["in"] = 7,
        ["<<"] = 8,
        [">>"] = 8,
        [">>>"] = 8,
        ["+"] = 9,
        ["-"] = 9,
        ["*"] = 10,
        ["/"] = 10,
        ["%"] = 10,
    };

    private readonly string _source;
    private readonly List<As3Token> _tokens;
    private readonly List<As3Diagnostic> _diagnostics = new();
    private readonly List<As3Import> _imports = new();
    private int _i;
    private int _depth;

    private As3Parser(string source)
    {
        _source = source;
        _tokens = new List<As3Token>(As3Lexer.Tokenize(source));
    }

    private sealed class ParseFailure : Exception
    {
    }

    /// <summary>Parses one ActionScript 3 file.</summary>
    /// <param name="source">The file text.</param>
    /// <returns>The syntax tree, with diagnostics for every problem found.</returns>
    public static As3CompilationUnit Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new As3Parser(source).ParseUnit();
    }

    private As3Token Cur => _tokens[_i];

    private As3Token PeekToken(int n = 1) => _tokens[Math.Min(_i + n, _tokens.Count - 1)];

    private bool AtEof => Cur.Kind == As3TokenKind.Eof;

    private As3Token Next()
    {
        var t = _tokens[_i];
        if (_i < _tokens.Count - 1)
        {
            _i++;
        }

        return t;
    }

    private bool IsPunct(string text) => Cur.Kind == As3TokenKind.Punctuator && Cur.Text == text;

    private bool IsKw(string text) => Cur.Kind == As3TokenKind.Keyword && Cur.Text == text;

    private bool IsContextual(string text) => Cur.Kind == As3TokenKind.Identifier && Cur.Text == text;

    private static bool IsNameLike(As3Token t) => t.Kind is As3TokenKind.Identifier or As3TokenKind.Keyword;

    private bool AcceptPunct(string text)
    {
        if (!IsPunct(text))
        {
            return false;
        }

        Next();
        return true;
    }

    private void ExpectPunct(string text)
    {
        if (!AcceptPunct(text))
        {
            Fail($"Expected '{text}' but found '{Describe(Cur)}'.");
        }
    }

    private void ExpectKw(string text)
    {
        if (!IsKw(text))
        {
            Fail($"Expected '{text}' but found '{Describe(Cur)}'.");
        }

        Next();
    }

    private static string Describe(As3Token t) => t.Kind == As3TokenKind.Eof ? "end of file" : t.Text;

    private string ExpectName()
    {
        if (!IsNameLike(Cur))
        {
            Fail($"Expected a name but found '{Describe(Cur)}'.");
        }

        return Next().Text;
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private void Fail(string message)
    {
        _diagnostics.Add(new As3Diagnostic(message, Cur.Span));
        throw new ParseFailure();
    }

    private SourceSpan SpanFrom(As3Token start)
    {
        var last = _i > 0 ? _tokens[_i - 1] : start;
        return last.Span.End < start.Span.Start ? start.Span : SourceSpan.Between(start.Span, last.Span);
    }

    private string RawFrom(As3Token start)
    {
        var span = SpanFrom(start);
        return _source.Substring(span.Start, Math.Min(span.Length, _source.Length - span.Start));
    }

    private void ConsumeSemicolon()
    {
        if (AcceptPunct(";") || IsPunct("}") || AtEof || Cur.NewlineBefore)
        {
            return;
        }

        Fail($"Expected ';' but found '{Describe(Cur)}'.");
    }

    private void Enter()
    {
        if (++_depth > _maxDepth)
        {
            Fail("Nesting is too deep.");
        }
    }

    private void Leave() => _depth--;

    // ---------------------------------------------------------------- declarations

    private As3CompilationUnit ParseUnit()
    {
        var start = Cur;
        var members = new List<As3Member>();
        var package = string.Empty;

        if (IsKw("package"))
        {
            try
            {
                Next();
                if (IsNameLike(Cur))
                {
                    package = ParseDottedName();
                }

                ExpectPunct("{");
                ParseMembers(members, untilBrace: true, className: null);
                ExpectPunct("}");
            }
            catch (ParseFailure)
            {
                SkipToEndOfBlock();
            }
        }

        ParseMembers(members, untilBrace: false, className: null);
        var span = new SourceSpan(0, _source.Length, 1, 1);
        return new As3CompilationUnit(span, package, _imports, members, _diagnostics);
    }

    private string ParseDottedName()
    {
        var name = ExpectName();
        while (IsPunct(".") && IsNameLike(PeekToken()))
        {
            Next();
            name += "." + Next().Text;
        }

        return name;
    }

    private void ParseMembers(List<As3Member> output, bool untilBrace, string? className)
    {
        while (!AtEof && !(untilBrace && IsPunct("}")))
        {
            var before = _i;
            try
            {
                ParseMember(output, className, inInterface: false);
            }
            catch (ParseFailure)
            {
                SkipMember();
            }

            if (_i == before)
            {
                Next();
            }
        }
    }

    private void SkipMember()
    {
        var depth = 0;
        while (!AtEof)
        {
            if (IsPunct("{"))
            {
                depth++;
            }
            else if (IsPunct("}"))
            {
                if (depth == 0)
                {
                    return;
                }

                depth--;
                Next();
                if (depth == 0)
                {
                    return;
                }

                continue;
            }
            else if (IsPunct(";") && depth == 0)
            {
                Next();
                return;
            }

            Next();
        }
    }

    private void SkipToEndOfBlock()
    {
        var depth = 1;
        while (!AtEof && depth > 0)
        {
            if (IsPunct("{"))
            {
                depth++;
            }
            else if (IsPunct("}"))
            {
                depth--;
            }

            Next();
        }
    }

    private List<As3Metadata> ParseMetadata()
    {
        var list = new List<As3Metadata>();
        while (IsPunct("[") && IsNameLike(PeekToken()))
        {
            var start = Next();
            var name = Next().Text;
            var args = new List<As3MetadataArgument>();
            if (AcceptPunct("("))
            {
                while (!IsPunct(")"))
                {
                    var argStart = Cur;
                    string? key = null;
                    if (IsNameLike(Cur) && PeekToken().Kind == As3TokenKind.Punctuator && PeekToken().Text == "=")
                    {
                        key = Next().Text;
                        Next();
                    }

                    if (AtEof || IsPunct("]"))
                    {
                        Fail("Unterminated metadata tag.");
                    }

                    var valueToken = Next();
                    args.Add(new As3MetadataArgument(SpanFrom(argStart), key, Unquote(valueToken)));
                    if (!AcceptPunct(","))
                    {
                        break;
                    }
                }

                ExpectPunct(")");
            }

            ExpectPunct("]");
            list.Add(new As3Metadata(SpanFrom(start), name, args));
        }

        return list;
    }

    private static string Unquote(As3Token t)
    {
        if (t.Kind == As3TokenKind.String && t.Text.Length >= 2)
        {
            return t.Text[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\'", "'", StringComparison.Ordinal);
        }

        return t.Text;
    }

    private As3Modifier ParseModifiers()
    {
        var mods = As3Modifier.None;
        while (true)
        {
            if (Cur.Kind == As3TokenKind.Keyword && Cur.Text is "public" or "private" or "protected" or "internal")
            {
                mods |= Cur.Text switch
                {
                    "public" => As3Modifier.Public,
                    "private" => As3Modifier.Private,
                    "protected" => As3Modifier.Protected,
                    _ => As3Modifier.Internal,
                };
                Next();
            }
            else if (Cur.Kind == As3TokenKind.Identifier && IsNameLike(PeekToken()))
            {
                var m = Cur.Text switch
                {
                    "static" => As3Modifier.Static,
                    "override" => As3Modifier.Override,
                    "final" => As3Modifier.Final,
                    "dynamic" => As3Modifier.Dynamic,
                    "native" => As3Modifier.Native,
                    _ => As3Modifier.None,
                };
                if (m == As3Modifier.None)
                {
                    // shortcut: a custom namespace modifier (such as mx_internal) is skipped, not recorded; add a flag if a rule needs it.
                    if (PeekToken().Kind == As3TokenKind.Keyword && PeekToken().Text is "var" or "const" or "function")
                    {
                        Next();
                        continue;
                    }

                    break;
                }

                mods |= m;
                Next();
            }
            else
            {
                break;
            }
        }

        return mods;
    }

    private void ParseMember(List<As3Member> output, string? className, bool inInterface)
    {
        var start = Cur;
        var metadata = ParseMetadata();
        var mods = ParseModifiers();

        if (Cur.Kind == As3TokenKind.Keyword)
        {
            switch (Cur.Text)
            {
                case "import" when className is null:
                    ParseImport();
                    return;
                case "class":
                case "interface":
                    output.Add(ParseType(start, metadata, mods));
                    return;
                case "var":
                case "const":
                    ParseFieldMember(output, start, metadata, mods);
                    return;
                case "function":
                    output.Add(ParseFunctionMember(start, metadata, mods, className, inInterface));
                    return;
                case "use":
                    SkipStatementRaw();
                    output.Add(new As3UnsupportedMember(SpanFrom(start), metadata, mods, "namespace", RawFrom(start)));
                    return;
            }
        }

        if (IsContextual("namespace") || IsContextual("include"))
        {
            var reason = Cur.Text;
            SkipStatementRaw();
            output.Add(new As3UnsupportedMember(SpanFrom(start), metadata, mods, reason, RawFrom(start)));
            return;
        }

        if (IsPunct("{") && className is not null)
        {
            SkipStatementRaw();
            output.Add(new As3UnsupportedMember(SpanFrom(start), metadata, mods, "static-initializer", RawFrom(start)));
            return;
        }

        Fail($"Unexpected '{Describe(Cur)}' in declaration.");
    }

    private void SkipStatementRaw()
    {
        var depth = 0;
        while (!AtEof)
        {
            if (IsPunct("{"))
            {
                depth++;
            }
            else if (IsPunct("}"))
            {
                if (depth == 0)
                {
                    return;
                }

                depth--;
                Next();
                if (depth == 0)
                {
                    return;
                }

                continue;
            }
            else if (IsPunct(";") && depth == 0)
            {
                Next();
                return;
            }

            Next();
        }
    }

    private void ParseImport()
    {
        var start = Next();
        var name = ExpectName();
        var wildcard = false;
        while (IsPunct("."))
        {
            Next();
            if (AcceptPunct("*"))
            {
                wildcard = true;
                break;
            }

            name += "." + ExpectName();
        }

        ConsumeSemicolon();
        _imports.Add(new As3Import(SpanFrom(start), name, wildcard));
    }

    private As3TypeDeclaration ParseType(As3Token start, List<As3Metadata> metadata, As3Modifier mods)
    {
        var kind = Next().Text == "class" ? As3TypeKind.Class : As3TypeKind.Interface;
        var name = ExpectName();
        As3TypeRef? baseType = null;
        var interfaces = new List<As3TypeRef>();
        if (IsKw("extends"))
        {
            Next();
            if (kind == As3TypeKind.Class)
            {
                baseType = ParseTypeRef();
            }
            else
            {
                interfaces.Add(ParseTypeRef());
                while (AcceptPunct(","))
                {
                    interfaces.Add(ParseTypeRef());
                }
            }
        }

        if (IsKw("implements"))
        {
            Next();
            interfaces.Add(ParseTypeRef());
            while (AcceptPunct(","))
            {
                interfaces.Add(ParseTypeRef());
            }
        }

        ExpectPunct("{");
        var members = new List<As3Member>();
        while (!AtEof && !IsPunct("}"))
        {
            var before = _i;
            try
            {
                ParseMember(members, name, inInterface: kind == As3TypeKind.Interface);
            }
            catch (ParseFailure)
            {
                SkipMember();
            }

            if (_i == before)
            {
                Next();
            }
        }

        ExpectPunct("}");
        return new As3TypeDeclaration(SpanFrom(start), metadata, mods, kind, name, baseType, interfaces, members);
    }

    private void ParseFieldMember(List<As3Member> output, As3Token start, List<As3Metadata> metadata, As3Modifier mods)
    {
        var isConst = Next().Text == "const";
        do
        {
            var d = ParseDeclarator(noIn: false);
            output.Add(new As3Field(SpanFrom(start), metadata, mods, isConst, d.Name, d.Type, d.Initializer));
        }
        while (AcceptPunct(","));

        ConsumeSemicolon();
    }

    private As3Method ParseFunctionMember(As3Token start, List<As3Metadata> metadata, As3Modifier mods, string? className, bool inInterface)
    {
        ExpectKw("function");
        var kind = As3MethodKind.Normal;
        if (Cur.Kind == As3TokenKind.Identifier && Cur.Text is "get" or "set" && IsNameLike(PeekToken()))
        {
            kind = Next().Text == "get" ? As3MethodKind.Getter : As3MethodKind.Setter;
        }

        var name = ExpectName();
        if (kind == As3MethodKind.Normal && className is not null && name == className && (mods & As3Modifier.Static) == 0)
        {
            kind = As3MethodKind.Constructor;
        }

        var parameters = ParseParameters();
        As3TypeRef? returnType = null;
        if (AcceptPunct(":"))
        {
            returnType = ParseTypeRef();
        }

        As3Block? body = null;
        if (IsPunct("{") && !inInterface)
        {
            body = ParseBlock();
        }
        else
        {
            ConsumeSemicolon();
        }

        return new As3Method(SpanFrom(start), metadata, mods, kind, name, parameters, returnType, body);
    }

    private List<As3Parameter> ParseParameters()
    {
        var list = new List<As3Parameter>();
        ExpectPunct("(");
        while (!IsPunct(")"))
        {
            var start = Cur;
            var isRest = AcceptPunct("...");
            var name = ExpectName();
            As3TypeRef? type = null;
            As3Expression? def = null;
            if (AcceptPunct(":"))
            {
                type = ParseTypeRef();
            }

            if (AcceptPunct("="))
            {
                def = ParseAssignment(noIn: false);
            }

            list.Add(new As3Parameter(SpanFrom(start), name, type, def, isRest));
            if (!AcceptPunct(","))
            {
                break;
            }
        }

        ExpectPunct(")");
        return list;
    }

    private As3TypeRef ParseTypeRef()
    {
        var start = Cur;
        if (AcceptPunct("*"))
        {
            return new As3TypeRef(SpanFrom(start), "*", Array.Empty<As3TypeRef>());
        }

        var name = ParseDottedName();
        var args = new List<As3TypeRef>();
        if (IsPunct(".<"))
        {
            Next();
            args.Add(ParseTypeRef());
            while (AcceptPunct(","))
            {
                args.Add(ParseTypeRef());
            }

            ExpectCloseAngle();
        }

        return new As3TypeRef(SpanFrom(start), name, args);
    }

    private void ExpectCloseAngle()
    {
        if (IsPunct(">"))
        {
            Next();
            return;
        }

        if (Cur.Kind == As3TokenKind.Punctuator && Cur.Text.Length > 1 && Cur.Text[0] == '>')
        {
            var rest = Cur.Text[1..];
            var span = new SourceSpan(Cur.Span.Start + 1, rest.Length, Cur.Span.Line, Cur.Span.Column + 1);
            _tokens[_i] = new As3Token(As3TokenKind.Punctuator, rest, span, Array.Empty<Trivia>(), false);
            return;
        }

        Fail($"Expected '>' but found '{Describe(Cur)}'.");
    }

    private As3Declarator ParseDeclarator(bool noIn)
    {
        var start = Cur;
        var name = ExpectName();
        As3TypeRef? type = null;
        As3Expression? init = null;
        if (AcceptPunct(":"))
        {
            type = ParseTypeRef();
        }

        if (AcceptPunct("="))
        {
            init = ParseAssignment(noIn);
        }

        return new As3Declarator(SpanFrom(start), name, type, init);
    }

    // ---------------------------------------------------------------- statements

    private As3Block ParseBlock()
    {
        var start = Cur;
        ExpectPunct("{");
        var statements = ParseStatements(() => IsPunct("}"));
        ExpectPunct("}");
        return new As3Block(SpanFrom(start), statements);
    }

    private List<As3Statement> ParseStatements(Func<bool> stop)
    {
        var list = new List<As3Statement>();
        while (!AtEof && !stop())
        {
            var before = _i;
            var start = Cur;
            try
            {
                list.Add(ParseStatement());
            }
            catch (ParseFailure)
            {
                SkipStatementRaw();
                list.Add(new As3UnsupportedStatement(SpanFrom(start), "syntax-error", RawFrom(start)));
            }

            if (_i == before)
            {
                Next();
            }
        }

        return list;
    }

    private As3Statement ParseStatement()
    {
        Enter();
        try
        {
            return ParseStatementCore();
        }
        finally
        {
            Leave();
        }
    }

    private As3Statement ParseStatementCore()
    {
        var start = Cur;
        if (IsPunct("{"))
        {
            return ParseBlock();
        }

        if (AcceptPunct(";"))
        {
            return new As3EmptyStatement(SpanFrom(start));
        }

        if (start.Kind == As3TokenKind.Keyword)
        {
            switch (start.Text)
            {
                case "if":
                    return ParseIf();
                case "for":
                    return ParseFor();
                case "while":
                    {
                        Next();
                        var cond = ParseParenExpression();
                        return new As3WhileStatement(SpanFrom(start), cond, ParseStatement());
                    }

                case "do":
                    {
                        Next();
                        var body = ParseStatement();
                        ExpectKw("while");
                        var cond = ParseParenExpression();
                        ConsumeSemicolon();
                        return new As3DoStatement(SpanFrom(start), body, cond);
                    }

                case "switch":
                    return ParseSwitch();
                case "try":
                    return ParseTry();
                case "return":
                    {
                        Next();
                        As3Expression? value = null;
                        if (!IsPunct(";") && !IsPunct("}") && !AtEof && !Cur.NewlineBefore)
                        {
                            value = ParseExpression(noIn: false);
                        }

                        ConsumeSemicolon();
                        return new As3ReturnStatement(SpanFrom(start), value);
                    }

                case "throw":
                    {
                        Next();
                        var value = ParseExpression(noIn: false);
                        ConsumeSemicolon();
                        return new As3ThrowStatement(SpanFrom(start), value);
                    }

                case "break":
                case "continue":
                    {
                        Next();
                        string? label = null;
                        if (Cur.Kind == As3TokenKind.Identifier && !Cur.NewlineBefore)
                        {
                            label = Next().Text;
                        }

                        ConsumeSemicolon();
                        return start.Text == "break"
                            ? new As3BreakStatement(SpanFrom(start), label)
                            : new As3ContinueStatement(SpanFrom(start), label);
                    }

                case "var":
                case "const":
                    return ParseVarStatement();
                case "with":
                    {
                        Next();
                        ParseParenExpression();
                        ParseStatement();
                        return new As3UnsupportedStatement(SpanFrom(start), "with", RawFrom(start));
                    }

                case "use":
                    SkipStatementRaw();
                    return new As3UnsupportedStatement(SpanFrom(start), "namespace", RawFrom(start));
            }
        }

        if (start.Kind == As3TokenKind.Identifier)
        {
            if (start.Text == "include" && PeekToken().Kind == As3TokenKind.String)
            {
                SkipStatementRaw();
                return new As3UnsupportedStatement(SpanFrom(start), "include", RawFrom(start));
            }

            if (PeekToken().Kind == As3TokenKind.Punctuator && PeekToken().Text == ":")
            {
                Next();
                Next();
                return new As3LabeledStatement(SpanFrom(start), start.Text, ParseStatement());
            }
        }

        var expr = ParseExpression(noIn: false);
        ConsumeSemicolon();
        return new As3ExpressionStatement(SpanFrom(start), expr);
    }

    private As3Expression ParseParenExpression()
    {
        ExpectPunct("(");
        var e = ParseExpression(noIn: false);
        ExpectPunct(")");
        return e;
    }

    private As3IfStatement ParseIf()
    {
        var start = Next();
        var cond = ParseParenExpression();
        var then = ParseStatement();
        As3Statement? otherwise = null;
        if (IsKw("else"))
        {
            Next();
            otherwise = ParseStatement();
        }

        return new As3IfStatement(SpanFrom(start), cond, then, otherwise);
    }

    private As3VarStatement ParseVarStatement()
    {
        var start = Cur;
        var isConst = Next().Text == "const";
        var list = new List<As3Declarator> { ParseDeclarator(noIn: false) };
        while (AcceptPunct(","))
        {
            list.Add(ParseDeclarator(noIn: false));
        }

        ConsumeSemicolon();
        return new As3VarStatement(SpanFrom(start), isConst, list);
    }

    private As3Statement ParseFor()
    {
        var start = Next();
        var isEach = false;
        if (IsContextual("each"))
        {
            isEach = true;
            Next();
        }

        ExpectPunct("(");
        As3Statement? init = null;
        if (IsKw("var") || IsKw("const"))
        {
            var initStart = Cur;
            var isConst = Next().Text == "const";
            var first = ParseDeclarator(noIn: true);
            if (IsKw("in") && first.Initializer is null)
            {
                Next();
                var collection = ParseExpression(noIn: false);
                ExpectPunct(")");
                return new As3ForInStatement(SpanFrom(start), isEach, first, null, collection, ParseStatement());
            }

            var list = new List<As3Declarator> { first };
            while (AcceptPunct(","))
            {
                list.Add(ParseDeclarator(noIn: true));
            }

            init = new As3VarStatement(SpanFrom(initStart), isConst, list);
        }
        else if (!IsPunct(";"))
        {
            var initStart = Cur;
            var expr = ParseExpression(noIn: true);
            if (IsKw("in"))
            {
                Next();
                var collection = ParseExpression(noIn: false);
                ExpectPunct(")");
                return new As3ForInStatement(SpanFrom(start), isEach, null, expr, collection, ParseStatement());
            }

            init = new As3ExpressionStatement(SpanFrom(initStart), expr);
        }

        ExpectPunct(";");
        var condition = IsPunct(";") ? null : ParseExpression(noIn: false);
        ExpectPunct(";");
        var increment = IsPunct(")") ? null : ParseExpression(noIn: false);
        ExpectPunct(")");
        return new As3ForStatement(SpanFrom(start), init, condition, increment, ParseStatement());
    }

    private As3SwitchStatement ParseSwitch()
    {
        var start = Next();
        var discriminant = ParseParenExpression();
        ExpectPunct("{");
        var cases = new List<As3SwitchCase>();
        while (!AtEof && !IsPunct("}"))
        {
            var caseStart = Cur;
            As3Expression? test = null;
            if (IsKw("case"))
            {
                Next();
                test = ParseExpression(noIn: false);
            }
            else if (IsKw("default"))
            {
                Next();
            }
            else
            {
                Fail($"Expected 'case' or 'default' but found '{Describe(Cur)}'.");
            }

            ExpectPunct(":");
            var body = ParseStatements(() => IsKw("case") || IsKw("default") || IsPunct("}"));
            cases.Add(new As3SwitchCase(SpanFrom(caseStart), test, body));
        }

        ExpectPunct("}");
        return new As3SwitchStatement(SpanFrom(start), discriminant, cases);
    }

    private As3TryStatement ParseTry()
    {
        var start = Next();
        var body = ParseBlock();
        var catches = new List<As3CatchClause>();
        while (IsKw("catch"))
        {
            var catchStart = Next();
            ExpectPunct("(");
            var name = ExpectName();
            As3TypeRef? type = null;
            if (AcceptPunct(":"))
            {
                type = ParseTypeRef();
            }

            ExpectPunct(")");
            catches.Add(new As3CatchClause(SpanFrom(catchStart), name, type, ParseBlock()));
        }

        As3Block? fin = null;
        if (IsKw("finally"))
        {
            Next();
            fin = ParseBlock();
        }

        return new As3TryStatement(SpanFrom(start), body, catches, fin);
    }

    // ---------------------------------------------------------------- expressions

    private As3Expression ParseExpression(bool noIn)
    {
        var start = Cur;
        var left = ParseAssignment(noIn);
        while (IsPunct(","))
        {
            Next();
            var right = ParseAssignment(noIn);
            left = new As3Binary(SpanFrom(start), ",", left, right);
        }

        return left;
    }

    private As3Expression ParseAssignment(bool noIn)
    {
        var start = Cur;
        var left = ParseConditional(noIn);
        if (Cur.Kind == As3TokenKind.Punctuator && Array.IndexOf(_assignmentOperators, Cur.Text) >= 0)
        {
            var op = Next().Text;
            var value = ParseAssignment(noIn);
            return new As3Assignment(SpanFrom(start), op, left, value);
        }

        return left;
    }

    private As3Expression ParseConditional(bool noIn)
    {
        var start = Cur;
        var cond = ParseBinary(0, noIn);
        if (!IsPunct("?"))
        {
            return cond;
        }

        Next();
        var a = ParseAssignment(noIn: false);
        ExpectPunct(":");
        var b = ParseAssignment(noIn);
        return new As3Conditional(SpanFrom(start), cond, a, b);
    }

    private int BinaryPrecedence(bool noIn)
    {
        var isOp = Cur.Kind == As3TokenKind.Punctuator
            || (Cur.Kind == As3TokenKind.Keyword && Cur.Text is "as" or "is" or "instanceof" or "in");
        if (!isOp || (noIn && IsKw("in")))
        {
            return -1;
        }

        return _binaryPrecedence.TryGetValue(Cur.Text, out var p) ? p : -1;
    }

    private As3Expression ParseBinary(int minPrecedence, bool noIn)
    {
        var start = Cur;
        var left = ParseUnary();
        while (true)
        {
            var prec = BinaryPrecedence(noIn);
            if (prec <= minPrecedence)
            {
                return left;
            }

            var op = Next().Text;
            var right = ParseBinary(prec, noIn);
            left = new As3Binary(SpanFrom(start), op, left, right);
        }
    }

    private As3Expression ParseUnary()
    {
        Enter();
        try
        {
            var start = Cur;
            if (Cur.Kind == As3TokenKind.Punctuator && Cur.Text is "!" or "~" or "+" or "-" or "++" or "--")
            {
                var op = Next().Text;
                return new As3Unary(SpanFrom(start), op, ParseUnary(), true);
            }

            if (Cur.Kind == As3TokenKind.Keyword && Cur.Text is "typeof" or "delete" or "void")
            {
                var op = Next().Text;
                return new As3Unary(SpanFrom(start), op, ParseUnary(), true);
            }

            var e = ParseCallMember();
            if (Cur.Kind == As3TokenKind.Punctuator && Cur.Text is "++" or "--" && !Cur.NewlineBefore)
            {
                var op = Next().Text;
                return new As3Unary(SpanFrom(start), op, e, false);
            }

            return e;
        }
        finally
        {
            Leave();
        }
    }

    private As3Expression ParseCallMember()
    {
        var start = Cur;
        var expr = IsKw("new") ? ParseNew() : ParsePrimary();
        while (true)
        {
            if (IsPunct("."))
            {
                Next();
                if (IsPunct("("))
                {
                    SkipBalancedParens();
                    expr = new As3UnsupportedExpression(SpanFrom(start), "e4x", RawFrom(start));
                }
                else if (IsPunct("@") || IsPunct("*"))
                {
                    Next();
                    if (IsPunct("["))
                    {
                        Next();
                        ParseExpression(noIn: false);
                        ExpectPunct("]");
                    }
                    else if (IsNameLike(Cur))
                    {
                        Next();
                    }

                    expr = new As3UnsupportedExpression(SpanFrom(start), "e4x", RawFrom(start));
                }
                else
                {
                    expr = new As3MemberAccess(SpanFrom(start), expr, ExpectName());
                }
            }
            else if (IsPunct(".."))
            {
                Next();
                ExpectName();
                expr = new As3UnsupportedExpression(SpanFrom(start), "e4x", RawFrom(start));
            }
            else if (IsPunct("::"))
            {
                Next();
                ExpectName();
                expr = new As3UnsupportedExpression(SpanFrom(start), "namespace", RawFrom(start));
            }
            else if (IsPunct(".<"))
            {
                expr = ParseGenericSuffix(start, expr);
            }
            else if (IsPunct("["))
            {
                Next();
                var index = ParseExpression(noIn: false);
                ExpectPunct("]");
                expr = new As3IndexAccess(SpanFrom(start), expr, index);
            }
            else if (IsPunct("("))
            {
                expr = new As3Call(SpanFrom(start), expr, ParseArguments());
            }
            else
            {
                return expr;
            }
        }
    }

    // The body of an E4X filter such as "(@id == 1)" is not valid AS3 expression syntax, so it is skipped, not parsed.
    private void SkipBalancedParens()
    {
        var depth = 0;
        do
        {
            if (AtEof)
            {
                Fail("Unterminated parenthesis.");
            }

            depth += IsPunct("(") ? 1 : IsPunct(")") ? -1 : 0;
            Next();
        }
        while (depth > 0);
    }

    private As3Expression ParseGenericSuffix(As3Token start, As3Expression target)
    {
        Next();
        var args = new List<As3TypeRef> { ParseTypeRef() };
        while (AcceptPunct(","))
        {
            args.Add(ParseTypeRef());
        }

        ExpectCloseAngle();
        return new As3GenericExpression(SpanFrom(start), target, args);
    }

    private List<As3Expression> ParseArguments()
    {
        var list = new List<As3Expression>();
        ExpectPunct("(");
        while (!IsPunct(")"))
        {
            list.Add(ParseAssignment(noIn: false));
            if (!AcceptPunct(","))
            {
                break;
            }
        }

        ExpectPunct(")");
        return list;
    }

    private As3Expression ParseNew()
    {
        var start = Next();
        var targetStart = Cur;
        var target = ParsePrimary();
        while (true)
        {
            if (IsPunct(".") && IsNameLike(PeekToken()))
            {
                Next();
                target = new As3MemberAccess(SpanFrom(targetStart), target, Next().Text);
            }
            else if (IsPunct(".<"))
            {
                target = ParseGenericSuffix(targetStart, target);
            }
            else if (IsPunct("["))
            {
                Next();
                var index = ParseExpression(noIn: false);
                ExpectPunct("]");
                target = new As3IndexAccess(SpanFrom(targetStart), target, index);
            }
            else
            {
                break;
            }
        }

        var args = IsPunct("(") ? ParseArguments() : new List<As3Expression>();
        return new As3New(SpanFrom(start), target, args);
    }

    private As3Expression ParsePrimary()
    {
        Enter();
        try
        {
            return ParsePrimaryCore();
        }
        finally
        {
            Leave();
        }
    }

    private As3Expression ParsePrimaryCore()
    {
        var t = Cur;
        switch (t.Kind)
        {
            case As3TokenKind.Number:
                Next();
                return new As3Literal(t.Span, As3LiteralKind.Number, t.Text);
            case As3TokenKind.String:
                Next();
                return new As3Literal(t.Span, As3LiteralKind.String, t.Text);
            case As3TokenKind.Regex:
                Next();
                return new As3Literal(t.Span, As3LiteralKind.Regex, t.Text);
            case As3TokenKind.Xml:
                Next();
                return new As3UnsupportedExpression(t.Span, "e4x", t.Text);
            case As3TokenKind.Identifier:
                Next();
                return new As3Identifier(t.Span, t.Text);
            case As3TokenKind.Keyword:
                switch (t.Text)
                {
                    case "true":
                    case "false":
                        Next();
                        return new As3Literal(t.Span, As3LiteralKind.Boolean, t.Text);
                    case "null":
                        Next();
                        return new As3Literal(t.Span, As3LiteralKind.Null, t.Text);
                    case "this":
                    case "super":
                        Next();
                        return new As3Identifier(t.Span, t.Text);
                    case "function":
                        return ParseFunctionExpression();
                }

                break;
            case As3TokenKind.Punctuator:
                switch (t.Text)
                {
                    case "(":
                        {
                            Next();
                            var inner = ParseExpression(noIn: false);
                            ExpectPunct(")");
                            return new As3Parenthesized(SpanFrom(t), inner);
                        }

                    case "[":
                        return ParseArrayLiteral();
                    case "{":
                        return ParseObjectLiteral();
                }

                break;
        }

        Fail($"Unexpected '{Describe(t)}' in expression.");
        return null!;
    }

    private As3FunctionExpression ParseFunctionExpression()
    {
        var start = Next();
        string? name = null;
        if (Cur.Kind == As3TokenKind.Identifier)
        {
            name = Next().Text;
        }

        var parameters = ParseParameters();
        As3TypeRef? returnType = null;
        if (AcceptPunct(":"))
        {
            returnType = ParseTypeRef();
        }

        var body = ParseBlock();
        return new As3FunctionExpression(SpanFrom(start), name, parameters, returnType, body);
    }

    private As3ArrayLiteral ParseArrayLiteral()
    {
        var start = Next();
        var items = new List<As3Expression>();
        while (!IsPunct("]"))
        {
            if (AcceptPunct(","))
            {
                continue;
            }

            items.Add(ParseAssignment(noIn: false));
            if (!AcceptPunct(","))
            {
                break;
            }
        }

        ExpectPunct("]");
        return new As3ArrayLiteral(SpanFrom(start), items);
    }

    private As3ObjectLiteral ParseObjectLiteral()
    {
        var start = Next();
        var props = new List<As3ObjectProperty>();
        while (!IsPunct("}"))
        {
            var propStart = Cur;
            if (Cur.Kind is not (As3TokenKind.Identifier or As3TokenKind.Keyword or As3TokenKind.String or As3TokenKind.Number))
            {
                Fail($"Expected a property name but found '{Describe(Cur)}'.");
            }

            var key = Unquote(Next());
            ExpectPunct(":");
            props.Add(new As3ObjectProperty(SpanFrom(propStart), key, ParseAssignment(noIn: false)));
            if (!AcceptPunct(","))
            {
                break;
            }
        }

        ExpectPunct("}");
        return new As3ObjectLiteral(SpanFrom(start), props);
    }
}
