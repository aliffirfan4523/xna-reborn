using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// The Visual Basic version of <see cref="EffectPassRewriter"/> (rules XNA039 and XNA040). It rewrites the whole pattern
/// <c>effect.Begin</c>, <c>For Each pass As EffectPass In effect.CurrentTechnique.Passes</c>, <c>pass.Begin</c>, <c>pass.End</c>, <c>Next</c>, <c>effect.End</c>
/// (with or without parentheses) to a loop that calls <c>pass.Apply()</c>. Anything that does not fit the whole pattern keeps its code and gets
/// a marker from <see cref="VisualBasicMarkerRewriter"/>. Both rule IDs run the same rewrite, so the second run finds nothing to do.
/// </summary>
public sealed class VisualBasicEffectPassRewriter : ISourceRewriter
{
    private readonly VisualBasicMarkerRewriter _markers;

    /// <summary>Initializes a new instance of the <see cref="VisualBasicEffectPassRewriter"/> class.</summary>
    /// <param name="ruleId">Either <see cref="EffectPassRewriter.PassRuleId"/> or <see cref="EffectPassRewriter.EffectRuleId"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ruleId"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="ruleId"/> is neither XNA039 nor XNA040.</exception>
    public VisualBasicEffectPassRewriter(string ruleId)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        if (ruleId != EffectPassRewriter.PassRuleId && ruleId != EffectPassRewriter.EffectRuleId)
        {
            throw new ArgumentException($"The rule ID must be {EffectPassRewriter.PassRuleId} or {EffectPassRewriter.EffectRuleId}.", nameof(ruleId));
        }

        RuleId = ruleId;
        _markers = VisualBasicMarkerRewriter.CreateAll().Single(m => m.RuleId == ruleId);
    }

    /// <inheritdoc />
    public string RuleId { get; }

    /// <inheritdoc />
    public SourceLanguage Language => SourceLanguage.VisualBasic;

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var text = context.Text;
        var symbols = context.OriginalSymbols.Select(s => new KeyValuePair<string, object>(s, true)).ToList();
        var root = VisualBasicSyntaxTree.ParseText(text, new VisualBasicParseOptions(preprocessorSymbols: symbols)).GetRoot();
        var notes = new List<RewriteNote>();
        if (VisualBasicGraphicsImport.Imports(root, context.ImplicitImports))
        {
            var edits = new Dictionary<int, Edit>();
            foreach (var loop in root.DescendantNodes().OfType<ForEachBlockSyntax>())
            {
                var plan = TryPlan(loop, text);
                if (plan is null || plan.Edits.Any(e => edits.ContainsKey(e.Start)))
                {
                    continue;
                }

                foreach (var edit in plan.Edits)
                {
                    edits[edit.Start] = edit;
                }

                notes.Add(new RewriteNote(RuleId, plan.Note, NeedsManualWork: false));
            }

            foreach (var edit in edits.Values.OrderByDescending(e => e.Start))
            {
                text = text.Remove(edit.Start, edit.End - edit.Start).Insert(edit.Start, edit.NewText);
            }
        }

        var marked = _markers.Rewrite(context with { Text = text });
        notes.AddRange(marked.Notes);
        return string.Equals(marked.Text, context.Text, StringComparison.Ordinal) ? SourceRewriteResult.Unchanged(context) : new SourceRewriteResult(marked.Text, notes);
    }

    private static Plan? TryPlan(ForEachBlockSyntax loop, string text)
    {
        if (!TryReadLoop(loop, out var effect, out var pass, out var passBegin, out var passEnd))
        {
            return null;
        }

        var siblings = loop.Parent?.ChildNodes().OfType<StatementSyntax>().ToList();
        var index = siblings?.IndexOf(loop) ?? -1;
        if (siblings is null || index < 0)
        {
            return null;
        }

        var begin = FindEffectBegin(siblings, index, effect);
        var end = FindEffectEnd(siblings, index, effect);
        if (begin is null || end is null)
        {
            return null;
        }

        var edits = new List<Edit>();
        foreach (var statement in new StatementSyntax[] { begin, end, passEnd })
        {
            var deletion = Deletion(text, statement);
            if (deletion is null)
            {
                return null;
            }

            edits.Add(deletion);
        }

        var access = AccessOf(passBegin.Expression);
        var name = access!.Name;
        var hasParentheses = passBegin.Expression is InvocationExpressionSyntax { ArgumentList: not null };
        edits.Add(new Edit(name.SpanStart, name.Span.End, hasParentheses ? "Apply" : "Apply()", string.Empty));
        return new Plan(edits, $"Replaced {effect}.Begin/End and {pass}.Begin/End with {pass}.Apply() in the loop over {effect}.CurrentTechnique.Passes.");
    }

    private static bool TryReadLoop(ForEachBlockSyntax loop, out string effect, out string pass, out ExpressionStatementSyntax passBegin, out ExpressionStatementSyntax passEnd)
    {
        effect = pass = string.Empty;
        passBegin = passEnd = null!;
        var header = loop.ForEachStatement;
        if (header.Expression is not MemberAccessExpressionSyntax passes
            || !VisualBasicGraphicsImport.Same(passes.Name.Identifier.ValueText, "Passes")
            || passes.Expression is not MemberAccessExpressionSyntax technique
            || !VisualBasicGraphicsImport.Same(technique.Name.Identifier.ValueText, "CurrentTechnique")
            || !VisualBasicGraphicsImport.IsChain(technique.Expression)
            || !TryReadControlVariable(header.ControlVariable, out pass))
        {
            return false;
        }

        effect = VisualBasicGraphicsImport.Normalize(technique.Expression!);
        var beginIndex = -1;
        var endIndex = -1;
        for (var i = 0; i < loop.Statements.Count; i++)
        {
            if (loop.Statements[i] is not ExpressionStatementSyntax statement)
            {
                continue;
            }

            if (IsCall(statement, pass, "Begin", 0))
            {
                if (beginIndex >= 0)
                {
                    return false;
                }

                beginIndex = i;
                passBegin = statement;
            }
            else if (IsCall(statement, pass, "End", 0))
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

        // Any other Begin or End on the pass (in a branch, say) means the loop is not the plain pattern.
        var passName = pass;
        var all = loop.Statements.SelectMany(s => s.DescendantNodesAndSelf()).OfType<MemberAccessExpressionSyntax>()
            .Count(a => a.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                && (VisualBasicGraphicsImport.Same(a.Name.Identifier.ValueText, "Begin") || VisualBasicGraphicsImport.Same(a.Name.Identifier.ValueText, "End"))
                && a.Expression is not null
                && VisualBasicGraphicsImport.Same(VisualBasicGraphicsImport.Normalize(a.Expression), passName));
        return all == 2;
    }

    private static bool TryReadControlVariable(VisualBasicSyntaxNode variable, out string pass)
    {
        pass = string.Empty;
        switch (variable)
        {
            case VariableDeclaratorSyntax { Names.Count: 1 } declarator
                when declarator.AsClause is null || (declarator.AsClause is SimpleAsClauseSyntax simple && IsPassType(simple.Type)):
                pass = declarator.Names[0].Identifier.ValueText;
                return true;
            case IdentifierNameSyntax name:
                pass = name.Identifier.ValueText;
                return true;
            default:
                return false;
        }
    }

    private static bool IsPassType(TypeSyntax type)
    {
        var name = VisualBasicGraphicsImport.Normalize(type);
        return VisualBasicGraphicsImport.Same(name, "EffectPass") || name.EndsWith(".EffectPass", StringComparison.OrdinalIgnoreCase);
    }

    private static StatementSyntax? FindEffectBegin(List<StatementSyntax> statements, int loopIndex, string effect)
    {
        for (var j = loopIndex - 1; j >= 0; j--)
        {
            if (statements[j] is not ExpressionStatementSyntax statement)
            {
                continue;
            }

            if (IsCall(statement, effect, "End", 0))
            {
                return null;
            }

            if (IsCall(statement, effect, "Begin", 1))
            {
                var arguments = ArgumentsOf(statement.Expression);
                return arguments.Count == 0 || IsSaveStateMode(arguments[0]) ? statement : null;
            }
        }

        return null;
    }

    private static StatementSyntax? FindEffectEnd(List<StatementSyntax> statements, int loopIndex, string effect)
    {
        for (var k = loopIndex + 1; k < statements.Count; k++)
        {
            if (statements[k] is not ExpressionStatementSyntax statement)
            {
                continue;
            }

            if (IsCall(statement, effect, "Begin", 1))
            {
                return null;
            }

            if (IsCall(statement, effect, "End", 0))
            {
                return statement;
            }
        }

        return null;
    }

    private static bool IsSaveStateMode(ArgumentSyntax argument) =>
        argument is SimpleArgumentSyntax { IsNamed: false, Expression: MemberAccessExpressionSyntax access }
        && access.Expression is not null
        && VisualBasicGraphicsImport.Same(LastSegment(VisualBasicGraphicsImport.Normalize(access.Expression)), "SaveStateMode");

    private static string LastSegment(string chain)
    {
        var dot = chain.LastIndexOf('.');
        return dot < 0 ? chain : chain[(dot + 1)..];
    }

    private static MemberAccessExpressionSyntax? AccessOf(ExpressionSyntax expression) => expression switch
    {
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } => access,
        MemberAccessExpressionSyntax access => access,
        _ => null,
    };

    private static IReadOnlyList<ArgumentSyntax> ArgumentsOf(ExpressionSyntax expression) =>
        expression is InvocationExpressionSyntax { ArgumentList: { } list } ? list.Arguments : [];

    private static bool IsCall(ExpressionStatementSyntax statement, string receiver, string method, int maxArguments)
    {
        var access = AccessOf(statement.Expression);
        return access is not null
            && access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && access.Expression is not null
            && VisualBasicGraphicsImport.Same(access.Name.Identifier.ValueText, method)
            && ArgumentsOf(statement.Expression).Count <= maxArguments
            && VisualBasicGraphicsImport.IsChain(access.Expression)
            && VisualBasicGraphicsImport.Same(VisualBasicGraphicsImport.Normalize(access.Expression), receiver);
    }

    // The whole line goes away only when the statement is alone on it; a ':' or a comment on the line leaves the pattern for the marker.
    private static Edit? Deletion(string text, StatementSyntax statement)
    {
        var start = statement.SpanStart;
        var end = statement.Span.End;
        var lineStart = start;
        while (lineStart > 0 && text[lineStart - 1] != '\n' && text[lineStart - 1] != '\r')
        {
            lineStart--;
        }

        if (text[lineStart..start].Any(c => c != ' ' && c != '\t'))
        {
            return null;
        }

        var after = end;
        while (after < text.Length && (text[after] == ' ' || text[after] == '\t'))
        {
            after++;
        }

        if (after < text.Length && text[after] != '\r' && text[after] != '\n')
        {
            return null;
        }

        if (after < text.Length)
        {
            after += text[after] == '\r' && after + 1 < text.Length && text[after + 1] == '\n' ? 2 : 1;
        }

        return new Edit(lineStart, after, string.Empty, string.Empty);
    }

    private sealed record Edit(int Start, int End, string NewText, string Note);

    private sealed record Plan(IReadOnlyList<Edit> Edits, string Note);
}
