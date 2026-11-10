using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source;

/// <summary>
/// Adds <c>// XNAREBORN: {rule}: {message}</c> comment lines above code that needs a person's attention.
/// Only whole lines are inserted, so every other byte of the file stays as it was.
/// </summary>
public static partial class MarkerInserter
{
    /// <summary>The text every marker line starts with.</summary>
    public const string Prefix = "// XNAREBORN: ";

    /// <summary>Adds a marker line above the statement or member that contains <paramref name="position"/>.</summary>
    /// <param name="text">Text of one .cs file.</param>
    /// <param name="position">Character offset into <paramref name="text"/>.</param>
    /// <param name="ruleId">The rule that needs attention.</param>
    /// <param name="message">What the person has to do. Line breaks become spaces.</param>
    /// <param name="preprocessorSymbols">Symbols defined when parsing, such as WINDOWS. Null means none.</param>
    /// <returns>
    /// The new text. The input text is returned when the same marker is already there, or when
    /// <paramref name="position"/> is inside a comment, a string or inactive <c>#if</c> code.
    /// The marker uses the indentation of the target line and the file's own line ending.
    /// A position outside any statement or member (for example a <c>using</c> directive) uses its own line.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is outside the text.</exception>
    public static string InsertMarker(string text, int position, string ruleId, string message, IReadOnlyCollection<string>? preprocessorSymbols = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(ruleId);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, text.Length);

        var marker = MarkerLine(ruleId, message);
        var root = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: preprocessorSymbols)).GetRoot();
        var probe = Math.Min(position, Math.Max(0, text.Length - 1));
        if (text.Length == 0 || IsInsideCommentOrString(root, probe))
        {
            return text;
        }

        var target = FindTarget(root, probe);
        var lineStart = LineStart(text, target?.SpanStart ?? probe);
        if (MarkerAlreadyAbove(text, lineStart, marker))
        {
            return text;
        }

        var indent = LeadingWhitespace(text, lineStart);
        return text.Insert(lineStart, indent + marker + DetectNewLine(text));
    }

    /// <summary>Adds a marker line above a node, for use inside a <see cref="CSharpSyntaxRewriter"/>.</summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <param name="node">The statement or member to mark.</param>
    /// <param name="ruleId">The rule that needs attention.</param>
    /// <param name="message">What the person has to do. Line breaks become spaces.</param>
    /// <returns>The node with the marker in its leading trivia, or the same node when it already has that marker.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TNode WithMarker<TNode>(TNode node, string ruleId, string message)
        where TNode : SyntaxNode
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(ruleId);
        ArgumentNullException.ThrowIfNull(message);

        var marker = MarkerLine(ruleId, message);
        var leading = node.GetLeadingTrivia();
        if (leading.Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) && t.ToString() == marker))
        {
            return node;
        }

        var newLine = DetectNewLine(node.ToFullString());
        if (newLine == "\n" && !node.ToFullString().Contains('\n'))
        {
            newLine = DetectNewLine(node.SyntaxTree?.GetText().ToString() ?? string.Empty);
        }

        var indent = leading.Count > 0 && leading[^1].IsKind(SyntaxKind.WhitespaceTrivia) ? leading[^1] : default;
        var kept = indent == default ? leading : leading.RemoveAt(leading.Count - 1);
        var added = kept.ToList();
        if (indent != default)
        {
            added.Add(indent);
        }

        added.Add(SyntaxFactory.Comment(marker));
        added.Add(SyntaxFactory.EndOfLine(newLine));
        if (indent != default)
        {
            added.Add(indent);
        }

        return node.WithLeadingTrivia(SyntaxFactory.TriviaList(added));
    }

    private static string MarkerLine(string ruleId, string message) =>
        Prefix + ruleId + ": " + LineBreaks().Replace(message, " ").Trim();

    private static bool IsInsideCommentOrString(SyntaxNode root, int position)
    {
        var trivia = root.FindTrivia(position);
        if (trivia.Span.Contains(position)
            && (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.DisabledTextTrivia)))
        {
            return true;
        }

        var token = root.FindToken(position);
        return token.Span.Contains(position)
            && (token.IsKind(SyntaxKind.StringLiteralToken)
                || token.IsKind(SyntaxKind.CharacterLiteralToken)
                || token.IsKind(SyntaxKind.InterpolatedStringTextToken)
                || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
                || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)
                || token.IsKind(SyntaxKind.Utf8StringLiteralToken)
                || token.IsKind(SyntaxKind.InterpolatedStringStartToken)
                || token.IsKind(SyntaxKind.InterpolatedVerbatimStringStartToken)
                || token.IsKind(SyntaxKind.InterpolatedStringEndToken));
    }

    private static SyntaxNode? FindTarget(SyntaxNode root, int position)
    {
        var token = root.FindToken(position);
        for (var node = token.Parent; node is not null; node = node.Parent)
        {
            if (node is BlockSyntax)
            {
                continue;
            }

            if (node is StatementSyntax or MemberDeclarationSyntax)
            {
                return node;
            }
        }

        return null;
    }

    private static bool MarkerAlreadyAbove(string text, int lineStart, string marker)
    {
        var end = lineStart;
        while (end > 0)
        {
            var contentEnd = end;
            if (text[contentEnd - 1] == '\n')
            {
                contentEnd--;
            }

            if (contentEnd > 0 && text[contentEnd - 1] == '\r')
            {
                contentEnd--;
            }

            var previousStart = LineStart(text, contentEnd);
            var line = text[previousStart..contentEnd].Trim();
            if (!line.StartsWith(Prefix.TrimEnd(), StringComparison.Ordinal))
            {
                return false;
            }

            if (line == marker)
            {
                return true;
            }

            end = previousStart;
        }

        return false;
    }

    private static int LineStart(string text, int position)
    {
        var i = Math.Min(position, text.Length);
        while (i > 0 && text[i - 1] != '\n' && text[i - 1] != '\r')
        {
            i--;
        }

        return i;
    }

    private static string LeadingWhitespace(string text, int lineStart)
    {
        var end = lineStart;
        while (end < text.Length && (text[end] == ' ' || text[end] == '\t'))
        {
            end++;
        }

        return text[lineStart..end];
    }

    private static string DetectNewLine(string text)
    {
        var index = text.IndexOfAny(['\r', '\n']);
        if (index < 0 || text[index] == '\n')
        {
            return "\n";
        }

        return index + 1 < text.Length && text[index + 1] == '\n' ? "\r\n" : "\r";
    }

    [GeneratedRegex(@"\s*[\r\n]+\s*")]
    private static partial Regex LineBreaks();
}
