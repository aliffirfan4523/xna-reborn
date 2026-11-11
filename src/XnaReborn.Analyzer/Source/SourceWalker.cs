using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Source;

/// <summary>A rule hit before it is turned into a line, column, and snippet.</summary>
/// <param name="Position">Character offset of the start of the matched node.</param>
/// <param name="Order">Index of the rule in the list passed to the matcher.</param>
/// <param name="Rule">The rule that matched.</param>
internal readonly record struct RawHit(int Position, int Order, Rule Rule);

/// <summary>Walks one syntax tree once and collects hits for namespace, type, and member rules.</summary>
internal sealed class SourceWalker
{
    private readonly SourceRuleIndex _index;
    private readonly HashSet<string> _imports = new(StringComparer.Ordinal);
    private readonly List<RawHit> _hits = [];
    private readonly HashSet<(int Position, int Order)> _seen = [];

    private SourceWalker(SourceRuleIndex index) => _index = index;

    /// <summary>Collects every hit in a tree.</summary>
    /// <param name="root">The root of the tree.</param>
    /// <param name="index">The rules to look for.</param>
    /// <returns>Hits in traversal order. The caller sorts them.</returns>
    internal static List<RawHit> Walk(SyntaxNode root, SourceRuleIndex index)
    {
        var walker = new SourceWalker(index);
        walker.CollectImports(root);
        walker.Visit(root);
        walker.VisitDirectives(root);
        return walker._hits;
    }

    private static bool IsInNamespaceName(SyntaxNode node) =>
        node is NameSyntax && node.Parent is BaseNamespaceDeclarationSyntax declaration && declaration.Name == node;

    /// <summary>Tells whether a simple name stands in a position that cannot be a use of a type.</summary>
    private static bool IsNonUsePosition(SimpleNameSyntax name) => name.Parent switch
    {
        NameColonSyntax or NameEqualsSyntax => true,
        AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax initializer } assignment =>
            assignment.Left == name && initializer.IsKind(SyntaxKind.ObjectInitializerExpression),
        _ => false,
    };

    /// <summary>Finds the expression left of a name when the name is the right-hand part of a dotted name.</summary>
    private static bool TryGetLeft(SimpleNameSyntax name, out SyntaxNode? left)
    {
        switch (name.Parent)
        {
            case QualifiedNameSyntax qualified when qualified.Right == name:
                left = qualified.Left;
                return true;
            case MemberAccessExpressionSyntax access when access.Name == name:
                left = access.Expression;
                return true;
            case MemberBindingExpressionSyntax:
            case AliasQualifiedNameSyntax:
                left = null;
                return true;
            default:
                left = null;
                return false;
        }
    }

    private void CollectImports(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax))
        {
            if (node is UsingDirectiveSyntax { StaticKeyword.RawKind: 0 } directive
                && SyntaxNames.DottedText(directive.NamespaceOrType) is { } text)
            {
                _imports.Add(text);
            }
        }
    }

    private void Visit(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodesAndSelf(n => n is not UsingDirectiveSyntax && !IsInNamespaceName(n)))
        {
            switch (node)
            {
                case UsingDirectiveSyntax directive:
                    MatchUsing(directive);
                    break;
                case QualifiedNameSyntax or MemberAccessExpressionSyntax:
                    MatchNamespaceChain(node);
                    if (node is MemberAccessExpressionSyntax memberAccess
                        && memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    {
                        MatchMember(memberAccess);
                    }

                    break;
                case SimpleNameSyntax name when !IsInNamespaceName(name):
                    MatchType(name);
                    break;
                case MethodDeclarationSyntax method:
                    MatchMethod(method);
                    break;
            }
        }
    }

    /// <summary>
    /// Reads <c>#if</c> and <c>#elif</c> conditions from directive trivia. Roslyn builds this trivia for inactive
    /// branches too, so a symbol that only appears in code the project's defines switch off is still found.
    /// Each rule gives one hit per file, at its first match.
    /// </summary>
    private void VisitDirectives(SyntaxNode root)
    {
        if (_index.Symbols.Count == 0)
        {
            return;
        }

        var fired = new HashSet<int>();
        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: false))
        {
            if (!trivia.IsDirective)
            {
                continue;
            }

            var condition = trivia.GetStructure() switch
            {
                IfDirectiveTriviaSyntax directive => directive.Condition,
                ElifDirectiveTriviaSyntax directive => directive.Condition,
                _ => null,
            };
            if (condition is null)
            {
                continue;
            }

            foreach (var identifier in condition.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            {
                foreach (var rule in _index.Symbols)
                {
                    if (rule.Match.Names.Contains(identifier.Identifier.ValueText, StringComparer.Ordinal) && fired.Add(rule.Order))
                    {
                        AddHit(identifier.SpanStart, rule.Order, rule.Rule);
                    }
                }
            }
        }
    }

    private void MatchUsing(UsingDirectiveSyntax directive)
    {
        if (_index.Namespaces.Count == 0 || SyntaxNames.DottedText(directive.NamespaceOrType) is not { } text)
        {
            return;
        }

        AddNamespaceHits(text, directive.SpanStart);
    }

    private void MatchNamespaceChain(SyntaxNode node)
    {
        if (_index.Namespaces.Count == 0 || IsInsideLongerChain(node) || IsInNamespaceName(node))
        {
            return;
        }

        if (SyntaxNames.DottedText(node) is { } text)
        {
            AddNamespaceHits(text, node.SpanStart);
        }
    }

    private static bool IsInsideLongerChain(SyntaxNode node) => node.Parent switch
    {
        QualifiedNameSyntax qualified => qualified.Left == node,
        MemberAccessExpressionSyntax access => access.Expression == node
            && access.IsKind(SyntaxKind.SimpleMemberAccessExpression),
        _ => false,
    };

    private void AddNamespaceHits(string text, int position)
    {
        foreach (var rule in _index.Namespaces)
        {
            if (SyntaxNames.IsNamespaceOrChild(text, rule.Match.Namespace, rule.Match.IncludeChildren))
            {
                AddHit(position, rule.Order, rule.Rule);
            }
        }
    }

    private void MatchType(SimpleNameSyntax name)
    {
        if (!_index.TypesByName.TryGetValue(name.Identifier.ValueText, out var candidates) || IsNonUsePosition(name))
        {
            return;
        }

        var qualified = TryGetLeft(name, out var left);
        var leftText = left is null ? null : SyntaxNames.DottedText(left);
        foreach (var candidate in candidates)
        {
            var applies = qualified
                ? leftText is not null && candidate.Match.Namespaces.Contains(leftText, StringComparer.Ordinal)
                : candidate.Match.Namespaces.Any(_imports.Contains);
            if (applies)
            {
                AddHit(name.SpanStart, candidate.Order, candidate.Rule);
            }
        }
    }

    private void MatchMember(MemberAccessExpressionSyntax access)
    {
        if (!_index.MembersByName.TryGetValue(access.Name.Identifier.ValueText, out var candidates))
        {
            return;
        }

        var invocation = access.Parent is InvocationExpressionSyntax call && call.Expression == access ? call : null;
        var leftText = SyntaxNames.DottedText(access.Expression);
        var receiver = SyntaxNames.ReceiverName(access.Expression);
        foreach (var candidate in candidates)
        {
            var match = candidate.Match;
            if (IsGateOpen(match.Namespaces, leftText) && IsShapeMatch(match, invocation, receiver))
            {
                AddHit(access.SpanStart, candidate.Order, candidate.Rule);
            }
        }
    }

    private void MatchMethod(MethodDeclarationSyntax method)
    {
        if (!_index.MethodsByName.TryGetValue(method.Identifier.ValueText, out var candidates))
        {
            return;
        }

        var isOverride = method.Modifiers.Any(SyntaxKind.OverrideKeyword);
        foreach (var candidate in candidates)
        {
            if ((!candidate.Match.Overrides || isOverride) && candidate.Match.Namespaces.Any(_imports.Contains))
            {
                AddHit(method.Identifier.SpanStart, candidate.Order, candidate.Rule);
            }
        }
    }

    private bool IsGateOpen(IReadOnlyList<string> namespaces, string? leftText) =>
        namespaces.Any(_imports.Contains) || SyntaxNames.IsQualifiedBy(leftText, namespaces);

    private static bool IsShapeMatch(MemberMatch match, InvocationExpressionSyntax? invocation, string? receiver)
    {
        if (match.Receiver is not null && !string.Equals(match.Receiver, receiver, StringComparison.Ordinal))
        {
            return false;
        }

        if ((match.Kind == MemberMatchKind.Invocation && invocation is null)
            || (match.Kind == MemberMatchKind.Access && invocation is not null))
        {
            return false;
        }

        if (match.Arguments is null)
        {
            return true;
        }

        if (invocation is null)
        {
            return false;
        }

        var count = invocation.ArgumentList.Arguments.Count;
        return (match.Arguments.Min is not { } min || count >= min) && (match.Arguments.Max is not { } max || count <= max);
    }

    private void AddHit(int position, int order, Rule rule)
    {
        if (_seen.Add((position, order)))
        {
            _hits.Add(new RawHit(position, order, rule));
        }
    }
}
