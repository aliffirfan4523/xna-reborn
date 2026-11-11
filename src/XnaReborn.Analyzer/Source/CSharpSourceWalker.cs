using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Analyzer.Source;

/// <summary>Walks one C# syntax tree once and reports language independent facts to a sink. It only answers syntax questions; the rule logic is in the sink.</summary>
internal sealed class CSharpSourceWalker : ISourceWalker
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

    private static bool IsInsideLongerChain(SyntaxNode node) => node.Parent switch
    {
        QualifiedNameSyntax qualified => qualified.Left == node,
        MemberAccessExpressionSyntax access => access.Expression == node
            && access.IsKind(SyntaxKind.SimpleMemberAccessExpression),
        _ => false,
    };

    private static void CollectImports(SyntaxNode root, ISourceFactSink sink)
    {
        foreach (var node in root.DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax))
        {
            if (node is UsingDirectiveSyntax { StaticKeyword.RawKind: 0 } directive
                && SyntaxNames.DottedText(directive.NamespaceOrType) is { } text)
            {
                sink.DeclareImport(text);
                if (directive.Alias is { } alias)
                {
                    sink.Alias(alias.Name.Identifier.ValueText, text);
                }
            }
        }
    }

    private static void Visit(SyntaxNode root, ISourceFactSink sink)
    {
        foreach (var node in root.DescendantNodesAndSelf(n => n is not UsingDirectiveSyntax && !IsInNamespaceName(n)))
        {
            switch (node)
            {
                case UsingDirectiveSyntax directive:
                    if (SyntaxNames.DottedText(directive.NamespaceOrType) is { } usingText)
                    {
                        sink.UsingDirective(usingText, directive.SpanStart);
                    }

                    break;
                case QualifiedNameSyntax or MemberAccessExpressionSyntax:
                    if (!IsInsideLongerChain(node) && !IsInNamespaceName(node) && SyntaxNames.DottedText(node) is { } chain)
                    {
                        sink.NamespaceChain(chain, node.SpanStart);
                    }

                    if (node is MemberAccessExpressionSyntax memberAccess
                        && memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    {
                        ReportMember(memberAccess, sink);
                    }

                    break;
                case SimpleNameSyntax name when !IsInNamespaceName(name):
                    ReportName(name, sink);
                    break;
                case MethodDeclarationSyntax method:
                    sink.MethodDeclaration(method.Identifier.ValueText, method.Modifiers.Any(SyntaxKind.OverrideKeyword), method.Identifier.SpanStart);
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
        var leftText = left is null ? null : SyntaxNames.DottedText(left);
        sink.SimpleName(name.Identifier.ValueText, leftText, qualified, name.SpanStart);
    }

    private static void ReportMember(MemberAccessExpressionSyntax access, ISourceFactSink sink)
    {
        var invocation = access.Parent is InvocationExpressionSyntax call && call.Expression == access ? call : null;
        sink.MemberAccess(new MemberUse(
            access.Name.Identifier.ValueText,
            SyntaxNames.DottedText(access.Expression),
            SyntaxNames.ReceiverName(access.Expression),
            invocation is not null,
            invocation?.ArgumentList.Arguments.Count ?? 0,
            access.SpanStart));
    }

    /// <summary>
    /// Reads <c>#if</c> and <c>#elif</c> conditions from directive trivia. Roslyn builds this trivia for inactive
    /// branches too, so a symbol that only appears in code the project's defines switch off is still found.
    /// </summary>
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
                ElifDirectiveTriviaSyntax directive => directive.Condition,
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
