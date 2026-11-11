using System.Text.RegularExpressions;

namespace XnaReborn.Migrator.Effects;

/// <summary>
/// Finds the parts of an HLSL effect file that the rewriters care about. It is a tokenizer with a brace-depth tracker, not a parser:
/// comments never match, and only top-level shapes (techniques, passes, sampler states, top-level variables) are understood.
/// </summary>
/// <remarks>
/// shortcut: not a full HLSL parser; top-level shapes only. Upgrade if marker false positives show up in the candidate runs (task 19).
/// </remarks>
internal static partial class FxScanner
{
    private static readonly string[] _setShaderCalls = ["SetVertexShader", "SetPixelShader", "SetGeometryShader"];
    private static readonly string[] _nonVariableStarts =
        ["struct", "cbuffer", "tbuffer", "typedef", "sampler", "sampler1D", "sampler2D", "sampler3D", "samplerCUBE", "SamplerState", "SamplerComparisonState", "technique", "technique10", "technique11"];
    private static readonly string[] _genericTypes = ["vector", "matrix"];
    private static readonly string[] _genericTypePrefixes = ["Texture", "RWTexture", "Buffer", "StructuredBuffer", "RWStructuredBuffer", "ConstantBuffer"];

    [GeneratedRegex(@"^#\s*(?<d>[A-Za-z_]+)", RegexOptions.CultureInvariant)]
    private static partial Regex Directive();

    [GeneratedRegex(@"^#\s*define\s+(?<n>[A-Za-z_][A-Za-z0-9_]*)(?<v>[^\r\n]*)", RegexOptions.CultureInvariant)]
    private static partial Regex Define();

    [GeneratedRegex(@"^#\s*include\s*[""<](?<p>[^"">]+)["">]", RegexOptions.CultureInvariant)]
    private static partial Regex Include();

    [GeneratedRegex(@"//.*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingComment();

    private enum Context
    {
        Top,
        Technique,
        Pass,
        Sampler,
        Other,
    }

    /// <summary>Scans one effect file.</summary>
    /// <param name="text">The file text.</param>
    public static FxDocument Scan(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = Tokenize(text);
        var compiles = new List<FxCompile>();
        var defines = new List<FxDefine>();
        var includes = new List<FxInclude>();
        var samplerStates = new List<FxStatement>();
        var passStates = new List<FxStatement>();
        var annotations = new List<FxSpot>();
        var setCalls = new List<FxSpot>();
        var vpos = new List<FxSpot>();
        var declarations = new List<(string Name, bool HasDefault, int From, int To, int Line)>();
        var techniques = 0;
        var passes = 0;
        var conditional = 0;
        var firstCode = text.Length;

        var stack = new Stack<Context>();
        stack.Push(Context.Top);
        var statementStart = false;
        var topFrom = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (firstCode == text.Length && !(token.Kind == FxTokenKind.Preprocessor && Directive().Match(token.Text) is { Success: true } first && first.Groups["d"].Value == "include"))
            {
                firstCode = LineStart(text, token.Start);
            }

            if (token.Kind == FxTokenKind.Preprocessor)
            {
                ReadDirective(token, text, ref conditional, defines, includes);
                if (stack.Peek() == Context.Top)
                {
                    topFrom = i + 1;
                }

                continue;
            }

            var context = stack.Peek();
            if (token.Kind == FxTokenKind.Identifier && token.Text.Equals("VPOS", StringComparison.OrdinalIgnoreCase) && i > 0 && tokens[i - 1].Text == ":")
            {
                vpos.Add(new FxSpot("VPOS", token.Line, LineStart(text, token.Start)));
            }

            if (token.Text == "{" && token.Kind == FxTokenKind.Punctuation)
            {
                var next = Classify(tokens, i);
                techniques += next == Context.Technique ? 1 : 0;
                passes += next == Context.Pass ? 1 : 0;
                stack.Push(next);
                statementStart = next is Context.Pass or Context.Sampler;
                continue;
            }

            if (token.Text == "}" && token.Kind == FxTokenKind.Punctuation)
            {
                if (stack.Count > 1)
                {
                    stack.Pop();
                }

                statementStart = stack.Peek() is Context.Pass or Context.Sampler;
                if (stack.Peek() == Context.Top)
                {
                    if (i + 1 < tokens.Count && tokens[i + 1].Text == ";")
                    {
                        i++;
                    }

                    topFrom = i + 1;
                }

                continue;
            }

            if (statementStart && token.Kind == FxTokenKind.Identifier && context is Context.Pass or Context.Sampler)
            {
                i = ReadStatement(tokens, i, text, context, compiles, passStates, samplerStates, setCalls);
                continue;
            }

            if (context is Context.Top or Context.Technique or Context.Pass
                && token is { Text: "<", Kind: FxTokenKind.Punctuation } && i > 0 && tokens[i - 1].Kind == FxTokenKind.Identifier
                && !IsGenericType(tokens[i - 1].Text))
            {
                annotations.Add(new FxSpot("<", token.Line, LineStart(text, token.Start)));
            }

            if (context == Context.Top && token is { Text: ";", Kind: FxTokenKind.Punctuation })
            {
                AddDeclaration(tokens, topFrom, i, declarations);
                topFrom = i + 1;
            }
        }

        return new FxDocument
        {
            Text = text,
            Compiles = compiles,
            Defines = defines,
            Includes = includes,
            SamplerStates = samplerStates,
            PassStates = passStates,
            Annotations = annotations,
            SetShaderCalls = setCalls,
            Vpos = vpos,
            Globals = declarations.Select(d => new FxGlobal(d.Name, d.HasDefault, IsUsed(tokens, d.Name, d.From, d.To), d.Line)).ToList(),
            TechniqueCount = techniques,
            PassCount = passes,
            FirstCodeOffset = firstCode,
            NewLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n",
        };
    }

    /// <summary>Returns the offset of the first character of the line that holds <paramref name="offset"/>.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">An offset in the text.</param>
    internal static int LineStart(string text, int offset)
    {
        var i = Math.Min(offset, text.Length);
        while (i > 0 && text[i - 1] != '\n')
        {
            i--;
        }

        return i;
    }

    private static bool IsGenericType(string name) =>
        _genericTypes.Contains(name, StringComparer.Ordinal) || _genericTypePrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));

    private static Context Classify(List<FxToken> tokens, int brace)
    {
        FxToken? Before(int distance) => brace - distance >= 0 ? tokens[brace - distance] : null;

        bool IsWord(FxToken? t, params string[] words) =>
            t is { Kind: FxTokenKind.Identifier } && words.Contains(t.Text, StringComparer.OrdinalIgnoreCase);

        if (IsWord(Before(1), "sampler_state") || (IsWord(Before(2), "SamplerState", "SamplerComparisonState") && Before(1)?.Kind == FxTokenKind.Identifier))
        {
            return Context.Sampler;
        }

        if (IsWord(Before(1), "technique", "technique10", "technique11") || IsWord(Before(2), "technique", "technique10", "technique11"))
        {
            return Context.Technique;
        }

        if (IsWord(Before(1), "pass") || IsWord(Before(2), "pass"))
        {
            return Context.Pass;
        }

        return Context.Other;
    }

    private static void ReadDirective(FxToken token, string text, ref int conditional, List<FxDefine> defines, List<FxInclude> includes)
    {
        var directive = Directive().Match(token.Text);
        if (!directive.Success)
        {
            return;
        }

        switch (directive.Groups["d"].Value)
        {
            case "if":
            case "ifdef":
            case "ifndef":
                conditional++;
                break;
            case "endif":
                conditional = Math.Max(0, conditional - 1);
                break;
            case "define":
                var define = Define().Match(token.Text);
                if (define.Success)
                {
                    var value = TrailingComment().Replace(define.Groups["v"].Value, string.Empty).Trim();
                    defines.Add(new FxDefine(define.Groups["n"].Value, value, token.Start, token.Length, conditional, token.Line));
                }

                break;
            case "include":
                var include = Include().Match(token.Text);
                if (include.Success)
                {
                    includes.Add(new FxInclude(include.Groups["p"].Value, token.Start, token.Length, token.Line));
                }

                break;
        }
    }

    // Reads "Key = value;" or "SetPixelShader(...);" starting at tokens[start]. Returns the index of the closing semicolon (or the last token).
    private static int ReadStatement(
        List<FxToken> tokens,
        int start,
        string text,
        Context context,
        List<FxCompile> compiles,
        List<FxStatement> passStates,
        List<FxStatement> samplerStates,
        List<FxSpot> setCalls)
    {
        var end = start;
        var depth = 0;
        while (end < tokens.Count)
        {
            var t = tokens[end];
            if (t.Kind == FxTokenKind.Punctuation)
            {
                if (t.Text is "(" or "[")
                {
                    depth++;
                }
                else if (t.Text is ")" or "]")
                {
                    depth--;
                }
                else if (t.Text == ";" && depth <= 0)
                {
                    break;
                }
                else if (t.Text == "}" && depth <= 0)
                {
                    return end - 1;
                }
            }

            end++;
        }

        end = Math.Min(end, tokens.Count - 1);
        var key = tokens[start];
        var lineStart = LineStart(text, key.Start);
        if (context == Context.Pass && _setShaderCalls.Contains(key.Text, StringComparer.OrdinalIgnoreCase))
        {
            setCalls.Add(new FxSpot(key.Text, key.Line, lineStart));
            return end;
        }

        if (start + 1 >= tokens.Count || tokens[start + 1].Text != "=")
        {
            return end;
        }

        var valueTokens = tokens.Skip(start + 2).Take(Math.Max(0, end - (start + 2))).ToList();
        if (context == Context.Pass && valueTokens.Count >= 3 && valueTokens[0].Text.Equals("compile", StringComparison.OrdinalIgnoreCase))
        {
            var arguments = valueTokens.Count > 4 && valueTokens[3].Text == "(" && valueTokens[4].Text != ")";
            compiles.Add(new FxCompile(valueTokens[1].Text, valueTokens[1].Start, valueTokens[1].Length, valueTokens[2].Text, arguments, key.Line, lineStart));
            return end;
        }

        var valueStart = valueTokens.Count > 0 ? valueTokens[0].Start : key.End;
        var valueLength = valueTokens.Count > 0 ? valueTokens[^1].End - valueStart : 0;
        var statement = new FxStatement(key.Text, key.Start, text.Substring(valueStart, valueLength), valueStart, valueLength, key.Line, lineStart);
        (context == Context.Pass ? passStates : samplerStates).Add(statement);
        return end;
    }

    private static void AddDeclaration(List<FxToken> tokens, int from, int semicolon, List<(string Name, bool HasDefault, int From, int To, int Line)> declarations)
    {
        var words = new List<FxToken>();
        for (var i = from; i < semicolon; i++)
        {
            if (tokens[i].Kind != FxTokenKind.Preprocessor)
            {
                words.Add(tokens[i]);
            }
        }

        if (words.Count < 2 || words[0].Kind != FxTokenKind.Identifier)
        {
            return;
        }

        if (words.Any(w => w.Kind == FxTokenKind.Identifier && w.Text is "static" or "const" or "typedef" or "sampler_state")
            || _nonVariableStarts.Contains(words[0].Text, StringComparer.Ordinal))
        {
            return;
        }

        // The name is the last identifier before the first ':' '=' '[' or '<' at depth 0 (or the last identifier).
        FxToken? last = null;
        foreach (var w in words)
        {
            if (w.Kind == FxTokenKind.Punctuation && w.Text is ":" or "=" or "[" or "<")
            {
                break;
            }

            if (w.Kind == FxTokenKind.Identifier)
            {
                last = w;
            }
        }

        var name = last;
        var hasDefault = words.Any(w => w is { Kind: FxTokenKind.Punctuation, Text: "=" });
        if (name is null || name == words[0])
        {
            return;
        }

        declarations.Add((name.Text, hasDefault, from, semicolon, name.Line));
    }

    private static bool IsUsed(List<FxToken> tokens, string name, int from, int to)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if ((i < from || i > to) && tokens[i].Kind == FxTokenKind.Identifier && string.Equals(tokens[i].Text, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static List<FxToken> Tokenize(string text)
    {
        var tokens = new List<FxToken>();
        var line = 1;
        var i = 0;
        var atLineStart = true;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\n')
            {
                line++;
                i++;
                atLineStart = true;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2;
                while (i < text.Length && !(text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/'))
                {
                    if (text[i] == '\n')
                    {
                        line++;
                    }

                    i++;
                }

                i = Math.Min(text.Length, i + 2);
                continue;
            }

            var start = i;
            var startLine = line;
            if (c == '#' && atLineStart)
            {
                while (i < text.Length && !(text[i] == '\n' && !EndsWithContinuation(text, i)))
                {
                    if (text[i] == '\n')
                    {
                        line++;
                    }

                    i++;
                }

                var end = i;
                if (end > start && text[end - 1] == '\r')
                {
                    end--;
                }

                tokens.Add(new FxToken(FxTokenKind.Preprocessor, start, end - start, startLine, text[start..end]));
                continue;
            }

            atLineStart = false;
            if (c == '"')
            {
                i++;
                while (i < text.Length && text[i] != '"' && text[i] != '\n')
                {
                    i++;
                }

                i = Math.Min(text.Length, i + 1);
                tokens.Add(new FxToken(FxTokenKind.String, start, i - start, startLine, text[start..i]));
            }
            else if (char.IsLetter(c) || c == '_')
            {
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                {
                    i++;
                }

                tokens.Add(new FxToken(FxTokenKind.Identifier, start, i - start, startLine, text[start..i]));
            }
            else if (char.IsDigit(c) || (c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
            {
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '.' || text[i] == '_'
                    || ((text[i] == '+' || text[i] == '-') && i > start && (text[i - 1] == 'e' || text[i - 1] == 'E'))))
                {
                    i++;
                }

                tokens.Add(new FxToken(FxTokenKind.Number, start, i - start, startLine, text[start..i]));
            }
            else
            {
                i++;
                tokens.Add(new FxToken(FxTokenKind.Punctuation, start, 1, startLine, text[start..i]));
            }
        }

        return tokens;
    }

    // True when the line break at index i is preceded by a backslash (a continued preprocessor line).
    private static bool EndsWithContinuation(string text, int newLineIndex)
    {
        var j = newLineIndex - 1;
        if (j >= 0 && text[j] == '\r')
        {
            j--;
        }

        return j >= 0 && text[j] == '\\';
    }
}
