using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Rewrites the XNA 3.1 <c>Effect.Begin</c> / <c>EffectPass.Begin</c> pattern to the XNA 4.0 <c>EffectPass.Apply</c> pattern.
/// <code>
/// effect.Begin();
/// foreach (EffectPass pass in effect.CurrentTechnique.Passes)
/// {
///     pass.Begin();
///     ...
///     pass.End();
/// }
/// effect.End();
/// </code>
/// becomes a loop that calls <c>pass.Apply()</c>. Calls that do not fit the whole pattern are left alone and get a marker comment.
/// Both rule IDs run the same full rewrite, so registering both is safe: the second run finds nothing to do.
/// </summary>
public sealed class EffectPassRewriter : ISourceRewriter
{
    /// <summary>The rule ID for <c>EffectPass.Begin</c> and <c>End</c>.</summary>
    public const string PassRuleId = "XNA039";

    /// <summary>The rule ID for <c>Effect.Begin</c> and <c>End</c>.</summary>
    public const string EffectRuleId = "XNA040";

    private const string _graphicsNamespace = "Microsoft.Xna.Framework.Graphics";
    private const string _markerRuleId = PassRuleId;
    private const string _markerMessage = "Replace Begin/End with pass.Apply() before drawing.";

    private readonly string _ruleId;

    /// <summary>Initializes a new instance of the <see cref="EffectPassRewriter"/> class.</summary>
    /// <param name="ruleId">Either <see cref="PassRuleId"/> or <see cref="EffectRuleId"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ruleId"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="ruleId"/> is neither XNA039 nor XNA040.</exception>
    public EffectPassRewriter(string ruleId)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        if (ruleId != PassRuleId && ruleId != EffectRuleId)
        {
            throw new ArgumentException($"The rule ID must be {PassRuleId} or {EffectRuleId}.", nameof(ruleId));
        }

        _ruleId = ruleId;
    }

    /// <inheritdoc />
    public string RuleId => _ruleId;

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var text = context.Text;
        var root = Parse(text, context).GetRoot();
        if (!ImportsGraphics(root))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var notes = new List<RewriteNote>();
        var edits = new Dictionary<int, TextEdit>();
        foreach (var block in root.DescendantNodes().OfType<BlockSyntax>())
        {
            FindPatterns(block, text, edits, notes);
        }

        var current = ApplyEdits(text, edits.Values);

        var markerText = current;
        var tree = string.Equals(current, text, StringComparison.Ordinal) ? root.SyntaxTree : Parse(current, context);
        foreach (var position in FindUnmatchedBegins(tree.GetRoot()).OrderByDescending(p => p))
        {
            var marked = MarkerInserter.InsertMarker(markerText, position, _markerRuleId, _markerMessage);
            if (!string.Equals(marked, markerText, StringComparison.Ordinal))
            {
                markerText = marked;
                notes.Add(new RewriteNote(_ruleId, _markerRuleId + ": " + _markerMessage, NeedsManualWork: true));
            }
        }

        return string.Equals(markerText, text, StringComparison.Ordinal)
            ? SourceRewriteResult.Unchanged(context)
            : new SourceRewriteResult(markerText, notes);
    }

    private static SyntaxTree Parse(string text, SourceRewriteContext context) =>
        CSharpSyntaxTree.ParseText(
            text,
            new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: context.OriginalSymbols));

    private static bool ImportsGraphics(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes())
        {
            if (node is UsingDirectiveSyntax usingDirective)
            {
                if (usingDirective.StaticKeyword.IsKind(SyntaxKind.None)
                    && usingDirective.Name is not null
                    && Normalize(usingDirective.Name) == _graphicsNamespace)
                {
                    return true;
                }
            }
            else if (node is QualifiedNameSyntax or MemberAccessExpressionSyntax
                && node.FirstAncestorOrSelf<UsingDirectiveSyntax>() is null
                && Normalize(node) == _graphicsNamespace)
            {
                return true;
            }
        }

        return false;
    }

    private void FindPatterns(
        BlockSyntax block,
        string text,
        Dictionary<int, TextEdit> edits,
        List<RewriteNote> notes)
    {
        var statements = block.Statements;
        for (var i = 0; i < statements.Count; i++)
        {
            if (statements[i] is not ForEachStatementSyntax loop
                || !TryReadLoop(loop, out var effect, out var passName, out var passBegin, out var passEnd))
            {
                continue;
            }

            var begin = FindEffectBegin(statements, i, effect);
            var end = FindEffectEnd(statements, i, effect);
            if (begin is null || end is null)
            {
                continue;
            }

            AddDeletion(text, begin, edits);
            AddDeletion(text, end, edits);
            AddDeletion(text, passEnd, edits);
            var beginName = ((MemberAccessExpressionSyntax)((InvocationExpressionSyntax)passBegin.Expression).Expression).Name;
            edits[beginName.Span.Start] = new TextEdit(beginName.Span.Start, beginName.Span.End, "Apply");
            notes.Add(new RewriteNote(
                _ruleId,
                $"Replaced {effect}.Begin/End and {passName}.Begin/End with {passName}.Apply() in the loop over {effect}.CurrentTechnique.Passes.",
                NeedsManualWork: false));
        }
    }

    private static bool TryReadLoop(
        ForEachStatementSyntax loop,
        out string effect,
        out string passName,
        out ExpressionStatementSyntax passBegin,
        out ExpressionStatementSyntax passEnd)
    {
        effect = passName = string.Empty;
        passBegin = passEnd = null!;

        if (loop.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Passes" } passes
            || passes.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "CurrentTechnique" } technique
            || !IsChain(technique.Expression)
            || !IsPassType(loop.Type)
            || loop.Statement is not BlockSyntax body)
        {
            return false;
        }

        effect = Normalize(technique.Expression);
        var name = loop.Identifier.ValueText;
        passName = name;

        var beginIndex = -1;
        var endIndex = -1;
        for (var i = 0; i < body.Statements.Count; i++)
        {
            if (body.Statements[i] is not ExpressionStatementSyntax statement)
            {
                continue;
            }

            if (IsCall(statement.Expression, name, "Begin", 0, 0))
            {
                if (beginIndex >= 0)
                {
                    return false;
                }

                beginIndex = i;
                passBegin = statement;
            }
            else if (IsCall(statement.Expression, name, "End", 0, 0))
            {
                if (endIndex >= 0)
                {
                    return false;
                }

                endIndex = i;
                passEnd = statement;
            }
        }

        if (beginIndex < 0 || endIndex < beginIndex)
        {
            return false;
        }

        var all = body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Count(call => IsCall(call, name, "Begin", 0, 0) || IsCall(call, name, "End", 0, 0));
        return all == 2;
    }

    private static bool IsPassType(TypeSyntax type)
    {
        if (type.IsVar)
        {
            return true;
        }

        var name = Normalize(type);
        return name == "EffectPass" || name.EndsWith(".EffectPass", StringComparison.Ordinal);
    }

    private static ExpressionStatementSyntax? FindEffectBegin(
        SyntaxList<StatementSyntax> statements, int loopIndex, string effect)
    {
        for (var j = loopIndex - 1; j >= 0; j--)
        {
            if (statements[j] is not ExpressionStatementSyntax statement)
            {
                continue;
            }

            if (IsCall(statement.Expression, effect, "End", 0, 0))
            {
                return null;
            }

            if (IsCall(statement.Expression, effect, "Begin", 0, 1))
            {
                var call = (InvocationExpressionSyntax)statement.Expression;
                return call.ArgumentList.Arguments.Count == 0 || IsSaveStateMode(call.ArgumentList.Arguments[0])
                    ? statement
                    : null;
            }
        }

        return null;
    }

    private static ExpressionStatementSyntax? FindEffectEnd(
        SyntaxList<StatementSyntax> statements, int loopIndex, string effect)
    {
        for (var k = loopIndex + 1; k < statements.Count; k++)
        {
            if (statements[k] is not ExpressionStatementSyntax statement)
            {
                continue;
            }

            if (IsCall(statement.Expression, effect, "Begin", 0, 1))
            {
                return null;
            }

            if (IsCall(statement.Expression, effect, "End", 0, 0))
            {
                return statement;
            }
        }

        return null;
    }

    private static bool IsSaveStateMode(ArgumentSyntax argument) =>
        argument.NameColon is null
        && argument.RefKindKeyword.IsKind(SyntaxKind.None)
        && argument.Expression is MemberAccessExpressionSyntax access
        && LastSegment(Normalize(access.Expression)) == "SaveStateMode";

    private static IEnumerable<int> FindUnmatchedBegins(SyntaxNode root)
    {
        var passNames = new HashSet<string>(StringComparer.Ordinal) { "pass" };
        var effectNames = new HashSet<string>(StringComparer.Ordinal) { "effect" };
        foreach (var loop in root.DescendantNodes().OfType<ForEachStatementSyntax>())
        {
            if (loop.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Passes" } passes)
            {
                passNames.Add(loop.Identifier.ValueText);
                if (passes.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "CurrentTechnique" } technique
                    && IsChain(technique.Expression))
                {
                    effectNames.Add(Normalize(technique.Expression));
                }
            }
        }

        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Begin" } access
                || !IsChain(access.Expression))
            {
                continue;
            }

            var receiver = Normalize(access.Expression);
            var last = LastSegment(receiver);
            var count = call.ArgumentList.Arguments.Count;
            if ((count == 0 && (passNames.Contains(receiver) || last == "pass"))
                || (count <= 1 && (effectNames.Contains(receiver) || last == "effect")))
            {
                yield return call.SpanStart;
            }
        }
    }

    private static bool IsCall(ExpressionSyntax expression, string receiver, string method, int minArgs, int maxArgs) =>
        expression is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access,
        } call
        && access.Name.Identifier.ValueText == method
        && call.ArgumentList.Arguments.Count >= minArgs
        && call.ArgumentList.Arguments.Count <= maxArgs
        && IsChain(access.Expression)
        && Normalize(access.Expression) == receiver;

    private static bool IsChain(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax => true,
        ThisExpressionSyntax => true,
        MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access =>
            access.Name is IdentifierNameSyntax && IsChain(access.Expression),
        _ => false,
    };

    private static string Normalize(SyntaxNode node) =>
        string.Concat(node.DescendantTokens().Select(token => token.Text));

    private static string LastSegment(string chain)
    {
        var dot = chain.LastIndexOf('.');
        return dot < 0 ? chain : chain[(dot + 1)..];
    }

    private static void AddDeletion(string text, StatementSyntax statement, Dictionary<int, TextEdit> edits)
    {
        var start = statement.SpanStart;
        var end = statement.Span.End;

        var lineStart = start;
        while (lineStart > 0 && text[lineStart - 1] != '\n' && text[lineStart - 1] != '\r')
        {
            lineStart--;
        }

        var after = end;
        while (after < text.Length && (text[after] == ' ' || text[after] == '\t'))
        {
            after++;
        }

        var aloneBefore = IsBlank(text, lineStart, start);
        var atLineEnd = after >= text.Length || text[after] == '\r' || text[after] == '\n';
        if (aloneBefore && atLineEnd)
        {
            if (after < text.Length)
            {
                after += text[after] == '\r' && after + 1 < text.Length && text[after + 1] == '\n' ? 2 : 1;
            }

            edits[lineStart] = new TextEdit(lineStart, after, string.Empty);
        }
        else
        {
            edits[start] = new TextEdit(start, atLineEnd ? end : after, string.Empty);
        }
    }

    private static bool IsBlank(string text, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            if (text[i] != ' ' && text[i] != '\t')
            {
                return false;
            }
        }

        return true;
    }

    private static string ApplyEdits(string text, IEnumerable<TextEdit> edits)
    {
        var ordered = edits.OrderBy(e => e.Start).ToList();
        if (ordered.Count == 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var position = 0;
        foreach (var edit in ordered)
        {
            builder.Append(text, position, edit.Start - position);
            builder.Append(edit.Replacement);
            position = edit.End;
        }

        builder.Append(text, position, text.Length - position);
        return builder.ToString();
    }

    private readonly record struct TextEdit(int Start, int End, string Replacement);
}
