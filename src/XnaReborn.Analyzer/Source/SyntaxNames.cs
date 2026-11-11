using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Analyzer.Source;

/// <summary>Helpers that read dotted names out of syntax nodes without a semantic model.</summary>
internal static class SyntaxNames
{
    /// <summary>Gets the dotted text of a pure name chain such as <c>A.B.C</c>.</summary>
    /// <param name="node">A name or member-access expression.</param>
    /// <returns>
    /// The identifiers joined by dots (type arguments dropped, a leading <c>global::</c> dropped), or
    /// null when the node is anything other than a chain of simple names, for example a call result or <c>this</c>.
    /// </returns>
    internal static string? DottedText(SyntaxNode node)
    {
        switch (node)
        {
            case IdentifierNameSyntax id:
                return id.Identifier.ValueText;
            case GenericNameSyntax generic:
                return generic.Identifier.ValueText;
            case QualifiedNameSyntax qualified:
                return Join(DottedText(qualified.Left), qualified.Right);
            case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                return Join(DottedText(access.Expression), access.Name);
            case AliasQualifiedNameSyntax alias when alias.Alias.Identifier.ValueText == "global":
                return DottedText(alias.Name);
            default:
                return null;
        }
    }

    /// <summary>Gets the identifier directly left of the dot in a member access.</summary>
    /// <param name="left">The expression left of the dot.</param>
    /// <returns>
    /// The identifier for <c>b</c> in <c>b.Name</c> and <c>a.b.Name</c>, or null when the left side is not a
    /// name, for example a call result, an element access, <c>this</c>, or <c>base</c>.
    /// </returns>
    internal static string? ReceiverName(ExpressionSyntax left) => left switch
    {
        SimpleNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression) =>
            access.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Tests whether a dotted name is a namespace or, optionally, one of its children.</summary>
    /// <param name="text">The dotted name.</param>
    /// <param name="ns">The namespace.</param>
    /// <param name="includeChildren">Whether names below the namespace match.</param>
    /// <param name="comparison">How names are compared.</param>
    /// <returns>True when the name equals the namespace, or starts with it followed by a dot and children are included.</returns>
    internal static bool IsNamespaceOrChild(string text, string ns, bool includeChildren, StringComparison comparison = StringComparison.Ordinal)
    {
        if (!text.StartsWith(ns, comparison))
        {
            return false;
        }

        return text.Length == ns.Length || (includeChildren && text[ns.Length] == '.');
    }

    /// <summary>Tests whether a dotted name is one of the namespaces or starts with one followed by a dot.</summary>
    /// <param name="text">The dotted name, or null.</param>
    /// <param name="namespaces">The candidate namespaces.</param>
    /// <param name="comparison">How names are compared.</param>
    /// <returns>True when the name is qualified by one of the namespaces.</returns>
    internal static bool IsQualifiedBy(string? text, IReadOnlyList<string> namespaces, StringComparison comparison = StringComparison.Ordinal)
    {
        if (text is null)
        {
            return false;
        }

        foreach (var ns in namespaces)
        {
            if (IsNamespaceOrChild(text, ns, includeChildren: true, comparison))
            {
                return true;
            }
        }

        return false;
    }

    private static string? Join(string? left, SimpleNameSyntax right) =>
        left is null ? null : left + "." + right.Identifier.ValueText;
}
