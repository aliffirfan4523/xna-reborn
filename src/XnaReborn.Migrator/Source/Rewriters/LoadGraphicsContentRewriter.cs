using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Rewrites the XNA 1.0 overrides <c>LoadGraphicsContent(bool)</c> (XNA102) and <c>UnloadGraphicsContent(bool)</c> (XNA103) to the
/// 2.0 and later <c>LoadContent()</c> and <c>UnloadContent()</c>. The method is renamed, its parameter is dropped, and
/// <c>const bool name = true;</c> becomes the first statement, so a body that tests or passes on the old flag still compiles.
/// A method that is called by name, has another parameter list, has no block body, or clashes with an existing method of the new name
/// is left alone and gets a marker.
/// </summary>
public sealed class LoadGraphicsContentRewriter : ISourceRewriter
{
    /// <summary>The rule ID of the <c>LoadGraphicsContent</c> rewriter.</summary>
    public const string LoadId = "XNA102";

    /// <summary>The rule ID of the <c>UnloadGraphicsContent</c> rewriter.</summary>
    public const string UnloadId = "XNA103";

    private const int _indentWidth = 4;

    private readonly string _oldName;
    private readonly string _newName;

    private LoadGraphicsContentRewriter(string ruleId, string oldName, string newName)
    {
        RuleId = ruleId;
        _oldName = oldName;
        _newName = newName;
    }

    /// <inheritdoc />
    public string RuleId { get; }

    /// <summary>Creates the rewriter for <c>LoadGraphicsContent</c> (XNA102).</summary>
    /// <returns>A new rewriter.</returns>
    public static LoadGraphicsContentRewriter ForLoad() => new(LoadId, "LoadGraphicsContent", "LoadContent");

    /// <summary>Creates the rewriter for <c>UnloadGraphicsContent</c> (XNA103).</summary>
    /// <returns>A new rewriter.</returns>
    public static LoadGraphicsContentRewriter ForUnload() => new(UnloadId, "UnloadGraphicsContent", "UnloadContent");

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (firstEdits, marked, _) = Analyze(context.Text, context.OriginalSymbols);
        if (marked.Count == 0 && firstEdits.Count == 0)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        // Markers go in first, from the end. The edits are then worked out again on the new text, so they never use stale offsets.
        var text = context.Text;
        var message = $"{_oldName} does not exist in MonoGame. Move this code into {_newName}() by hand.";
        foreach (var position in marked.OrderByDescending(p => p))
        {
            text = MarkerInserter.InsertMarker(text, position, RuleId, message, context.OriginalSymbols);
        }

        var (edits, _, changed) = Analyze(text, context.OriginalSymbols);
        foreach (var edit in edits.OrderByDescending(e => e.Start))
        {
            text = text.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Text);
        }

        var notes = new List<RewriteNote>();
        if (changed > 0)
        {
            notes.Add(new RewriteNote(RuleId, $"{changed} {_oldName} override(s) renamed to {_newName}. The old flag is now a constant set to true.", NeedsManualWork: false));
        }

        if (marked.Count > 0)
        {
            notes.Add(new RewriteNote(RuleId, $"{marked.Count} {_oldName} override(s) left as they are with a marker (called by name, other parameters, or {_newName} already exists).", NeedsManualWork: true));
        }

        return new SourceRewriteResult(text, notes);
    }

    // Works out which overrides can be rewritten (as text edits) and where the others are (for markers).
    private (List<(int Start, int Length, string Text)> Edits, List<int> Marked, int Changed) Analyze(string text, IReadOnlyCollection<string> symbols)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: symbols);
        var root = CSharpSyntaxTree.ParseText(text, options).GetRoot();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == _oldName && m.Modifiers.Any(SyntaxKind.OverrideKeyword))
            .ToList();
        var edits = new List<(int Start, int Length, string Text)>();
        var marked = new List<int>();
        var called = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(i => CalledName(i) == _oldName);
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var changed = 0;
        foreach (var method in methods)
        {
            if (called || !TryGetFlag(method, out var flag) || HasMethod(method.Parent, _newName))
            {
                // A method that already carries this rule's marker above it is not marked a second time.
                if (!HasMarkerAbove(text, method.Identifier.SpanStart))
                {
                    marked.Add(method.Identifier.SpanStart);
                }

                continue;
            }

            var body = method.Body!;
            edits.Add((method.Identifier.SpanStart, method.Identifier.Span.Length, _newName));
            edits.Add((method.ParameterList.OpenParenToken.Span.End, method.ParameterList.Parameters.Span.Length, string.Empty));
            edits.Add((body.OpenBraceToken.Span.End, 0, newline + BodyIndent(text, body) + $"const bool {flag} = true;"));
            changed++;
        }

        return (edits, marked, changed);
    }

    private bool HasMarkerAbove(string text, int position)
    {
        var lineStart = position;
        while (lineStart > 0 && text[lineStart - 1] is not ('\n' or '\r'))
        {
            lineStart--;
        }

        var previousEnd = lineStart;
        while (previousEnd > 0 && text[previousEnd - 1] is '\n' or '\r')
        {
            previousEnd--;
        }

        var previousStart = previousEnd;
        while (previousStart > 0 && text[previousStart - 1] is not ('\n' or '\r'))
        {
            previousStart--;
        }

        return text[previousStart..previousEnd].TrimStart().StartsWith(MarkerInserter.Prefix + RuleId + ":", StringComparison.Ordinal);
    }
    private static string? CalledName(InvocationExpressionSyntax call) => call.Expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        _ => null,
    };

    private static bool HasMethod(SyntaxNode? owner, string name) =>
        owner is not null && owner.ChildNodes().OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.ValueText == name);

    // The only accepted shape is one plain bool parameter and a block body.
    private static bool TryGetFlag(MethodDeclarationSyntax method, out string flag)
    {
        flag = string.Empty;
        if (method.Body is null || method.ParameterList.Parameters.Count != 1)
        {
            return false;
        }

        var parameter = method.ParameterList.Parameters[0];
        if (parameter.Modifiers.Count > 0 || parameter.Default is not null || parameter.Type?.ToString() is not ("bool" or "Boolean" or "System.Boolean"))
        {
            return false;
        }

        flag = parameter.Identifier.ValueText;
        return true;
    }

    private static string BodyIndent(string text, BlockSyntax body)
    {
        var first = body.Statements.FirstOrDefault();
        if (first is not null)
        {
            return LineIndent(text, first.SpanStart);
        }

        return LineIndent(text, body.OpenBraceToken.SpanStart) + new string(' ', _indentWidth);
    }

    private static string LineIndent(string text, int position)
    {
        var start = position;
        while (start > 0 && text[start - 1] is not ('\n' or '\r'))
        {
            start--;
        }

        var end = start;
        while (end < text.Length && text[end] is ' ' or '\t')
        {
            end++;
        }

        return text[start..end];
    }
}
