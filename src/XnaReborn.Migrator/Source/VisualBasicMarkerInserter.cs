using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace XnaReborn.Migrator.Source;

/// <summary>
/// Adds <c>' XNAREBORN: {rule}: {message}</c> comment lines above Visual Basic statements that need a person's attention.
/// Only whole lines are inserted, so every other byte of the file stays as it was.
/// </summary>
public sealed partial class VisualBasicMarkerInserter : IMarkerInserter
{
    /// <summary>The text every marker line starts with.</summary>
    public const string Prefix = MarkerPrefixes.VisualBasic + " ";

    /// <inheritdoc />
    public string InsertMarker(string text, int position, string ruleId, string message, IReadOnlyCollection<string>? preprocessorSymbols = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(ruleId);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, text.Length);

        var symbols = (preprocessorSymbols ?? []).Select(s => new KeyValuePair<string, object>(s, true)).ToList();
        var root = VisualBasicSyntaxTree.ParseText(text, new VisualBasicParseOptions(preprocessorSymbols: symbols)).GetRoot();
        var probe = Math.Min(position, Math.Max(0, text.Length - 1));
        if (text.Length == 0 || IsInsideCommentOrString(root, probe))
        {
            return text;
        }

        var token = root.FindToken(probe);
        var statement = token.Parent?.AncestorsAndSelf().OfType<StatementSyntax>().FirstOrDefault();
        var anchor = statement is null ? token.SpanStart : statement.GetFirstToken().SpanStart;
        var lineStart = LineStart(text, anchor);
        var marker = Prefix + ruleId + ": " + LineBreaks().Replace(message, " ").Trim();
        if (MarkerAlreadyAbove(text, lineStart, marker))
        {
            return text;
        }

        var indent = LeadingWhitespace(text, lineStart);
        return text.Insert(lineStart, indent + marker + (text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n"));
    }

    private static bool IsInsideCommentOrString(SyntaxNode root, int position)
    {
        var trivia = root.FindTrivia(position);
        if (trivia.Span.Contains(position) && (trivia.IsKind(SyntaxKind.CommentTrivia) || trivia.IsKind(SyntaxKind.DocumentationCommentTrivia) || trivia.IsKind(SyntaxKind.DisabledTextTrivia)))
        {
            return true;
        }

        var token = root.FindToken(position);
        return token.Span.Contains(position) && (token.IsKind(SyntaxKind.StringLiteralToken) || token.IsKind(SyntaxKind.InterpolatedStringTextToken));
    }

    private static int LineStart(string text, int position)
    {
        var start = position;
        while (start > 0 && text[start - 1] != '\n')
        {
            start--;
        }

        return start;
    }

    private static string LeadingWhitespace(string text, int lineStart)
    {
        var end = lineStart;
        while (end < text.Length && text[end] is ' ' or '\t')
        {
            end++;
        }

        return text[lineStart..end];
    }

    private static bool MarkerAlreadyAbove(string text, int lineStart, string marker)
    {
        if (lineStart == 0)
        {
            return false;
        }

        var previousEnd = lineStart - 1;
        if (previousEnd > 0 && text[previousEnd - 1] == '\r')
        {
            previousEnd--;
        }

        var previousStart = LineStart(text, previousEnd);
        return text[previousStart..previousEnd].Trim() == marker;
    }

    [GeneratedRegex(@"\s*[\r\n]+\s*", RegexOptions.CultureInvariant)]
    private static partial Regex LineBreaks();
}
