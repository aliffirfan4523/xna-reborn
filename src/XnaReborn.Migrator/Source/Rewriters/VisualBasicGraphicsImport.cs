using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>Shared checks of the Visual Basic render state and effect pass rewriters.</summary>
internal static class VisualBasicGraphicsImport
{
    internal const string GraphicsNamespace = "Microsoft.Xna.Framework.Graphics";

    /// <summary>Tells whether the file or the project imports <c>Microsoft.Xna.Framework.Graphics</c>.</summary>
    internal static bool Imports(SyntaxNode root, IReadOnlyList<string>? implicitImports) =>
        (implicitImports ?? []).Any(i => Same(i.Trim(), GraphicsNamespace))
        || root.DescendantNodes().OfType<SimpleImportsClauseSyntax>().Any(c => c.Alias is null && Same(c.Name.ToString().Replace(" ", string.Empty, StringComparison.Ordinal), GraphicsNamespace));

    /// <summary>Compares two Visual Basic names without case.</summary>
    internal static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Joins the tokens of a node, so spacing and line breaks inside a name do not matter.</summary>
    internal static string Normalize(SyntaxNode node) => string.Concat(node.DescendantTokens().Select(t => t.Text));

    /// <summary>Tells whether the expression is a plain name chain such as <c>effect</c>, <c>Me.effect</c> or <c>a.b.c</c>.</summary>
    internal static bool IsChain(ExpressionSyntax? expression) => expression switch
    {
        IdentifierNameSyntax => true,
        MeExpressionSyntax => true,
        MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression) =>
            access.Name is IdentifierNameSyntax && IsChain(access.Expression),
        _ => false,
    };
}
