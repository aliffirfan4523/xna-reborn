using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace XnaReborn.Analyzer.Source;

/// <summary>
/// Walks one Visual Basic syntax tree once and reports language independent facts to a sink. Names are reported as written;
/// the sink compares them without case. Indexing and calls both look like calls in Visual Basic, so a receiver is the name that is invoked.
/// </summary>
internal sealed class VisualBasicSourceWalker : ISourceWalker
{
    /// <inheritdoc />
    public void Walk(SyntaxNode root, ISourceFactSink sink)
    {
        CollectImports(root, sink);
        Visit(root, sink);
        if (sink.WantsPreprocessorSymbols)
        {
            VisitDirectives(root, sink);
        }
    }

    /// <summary>Gets the dotted text of a pure name chain, or null when the node is anything else.</summary>
    /// <param name="node">A name or member access.</param>
    internal static string? DottedText(SyntaxNode? node)
    {
        switch (node)
        {
            case IdentifierNameSyntax id:
                return id.Identifier.ValueText;
            case GenericNameSyntax generic:
                return generic.Identifier.ValueText;
            case QualifiedNameSyntax qualified:
                return Join(DottedText(qualified.Left), qualified.Right);
            case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression) && access.Expression is not null:
                return Join(DottedText(access.Expression), access.Name);
            case GlobalNameSyntax:
                return string.Empty;
            default:
                return null;
        }
    }

    private static string? Join(string? left, SimpleNameSyntax right) =>
        left is null ? null : left.Length == 0 ? right.Identifier.ValueText : left + "." + right.Identifier.ValueText;

    private static string? ReceiverName(ExpressionSyntax? left) => left switch
    {
        SimpleNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression) => access.Name.Identifier.ValueText,
        InvocationExpressionSyntax call => ReceiverName(call.Expression),
        _ => null,
    };

    private static bool IsInNamespaceName(SyntaxNode node) =>
        node is NameSyntax && node.Parent is NamespaceStatementSyntax statement && statement.Name == node;

    private static bool IsNonUsePosition(SimpleNameSyntax name) => name.Parent switch
    {
        NameColonEqualsSyntax => true,
        NamedFieldInitializerSyntax initializer => initializer.Name == name,
        _ => false,
    };

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
            default:
                left = null;
                return false;
        }
    }

    private static bool IsInsideLongerChain(SyntaxNode node) => node.Parent switch
    {
        QualifiedNameSyntax qualified => qualified.Left == node,
        MemberAccessExpressionSyntax access => access.Expression == node && access.IsKind(SyntaxKind.SimpleMemberAccessExpression),
        _ => false,
    };

    private static IEnumerable<SimpleImportsClauseSyntax> Clauses(ImportsStatementSyntax statement) =>
        statement.ImportsClauses.OfType<SimpleImportsClauseSyntax>();

    private static void CollectImports(SyntaxNode root, ISourceFactSink sink)
    {
        foreach (var statement in root.DescendantNodes(n => n is CompilationUnitSyntax).OfType<ImportsStatementSyntax>())
        {
            foreach (var clause in Clauses(statement))
            {
                if (DottedText(clause.Name) is not { } text)
                {
                    continue;
                }

                sink.DeclareImport(text);
                if (clause.Alias is { } alias)
                {
                    sink.Alias(alias.Identifier.ValueText, text);
                }
            }
        }
    }

    private static void Visit(SyntaxNode root, ISourceFactSink sink)
    {
        foreach (var node in root.DescendantNodesAndSelf(n => n is not ImportsStatementSyntax && !IsInNamespaceName(n)))
        {
            switch (node)
            {
                case ImportsStatementSyntax statement:
                    foreach (var clause in Clauses(statement))
                    {
                        if (DottedText(clause.Name) is { } usingText)
                        {
                            sink.UsingDirective(usingText, clause.SpanStart);
                        }
                    }

                    break;
                case QualifiedNameSyntax or MemberAccessExpressionSyntax:
                    if (!IsInsideLongerChain(node) && !IsInNamespaceName(node) && DottedText(node) is { Length: > 0 } chain)
                    {
                        sink.NamespaceChain(chain, node.SpanStart);
                    }

                    if (node is MemberAccessExpressionSyntax memberAccess && memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    {
                        ReportMember(memberAccess, sink);
                    }

                    break;
                case SimpleNameSyntax name when !IsInNamespaceName(name):
                    ReportName(name, sink);
                    break;
                case MethodStatementSyntax method:
                    sink.MethodDeclaration(method.Identifier.ValueText, method.Modifiers.Any(SyntaxKind.OverridesKeyword), method.Identifier.SpanStart);
                    break;
            }
        }
    }

    private static void ReportName(SimpleNameSyntax name, ISourceFactSink sink)
    {
        if (IsNonUsePosition(name))
        {
            return;
        }

        var qualified = TryGetLeft(name, out var left);
        sink.SimpleName(name.Identifier.ValueText, left is null ? null : DottedText(left), qualified, name.SpanStart);
    }

    private static void ReportMember(MemberAccessExpressionSyntax access, ISourceFactSink sink)
    {
        // A call looks the same with and without parentheses in statement position, so a null argument list is a call with no arguments.
        var invocation = access.Parent is InvocationExpressionSyntax call && call.Expression == access ? call : null;
        sink.MemberAccess(new MemberUse(
            access.Name.Identifier.ValueText,
            DottedText(access.Expression),
            ReceiverName(access.Expression),
            invocation is not null,
            invocation?.ArgumentList?.Arguments.Count ?? 0,
            access.SpanStart));
    }

    private static void VisitDirectives(SyntaxNode root, ISourceFactSink sink)
    {
        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: false))
        {
            if (!trivia.IsDirective)
            {
                continue;
            }

            var condition = trivia.GetStructure() switch
            {
                IfDirectiveTriviaSyntax directive => directive.Condition,
                _ => null,
            };
            if (condition is null)
            {
                continue;
            }

            foreach (var identifier in condition.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            {
                sink.PreprocessorSymbol(identifier.Identifier.ValueText, identifier.SpanStart);
            }
        }
    }
}
