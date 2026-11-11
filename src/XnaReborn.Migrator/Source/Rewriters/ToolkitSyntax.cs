using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>One change to a file: the characters from <paramref name="Start"/> to <paramref name="End"/> become <paramref name="NewText"/>.</summary>
/// <param name="Start">Offset of the first character that is replaced.</param>
/// <param name="End">Offset after the last character that is replaced.</param>
/// <param name="NewText">The replacement text. Empty to delete.</param>
internal readonly record struct TextEdit(int Start, int End, string NewText);

/// <summary>Small helpers shared by the SharpDX Toolkit source rewriters. Text in, text out, no semantic model.</summary>
internal static class ToolkitSyntax
{
    internal const string MonoGameNamespace = "Microsoft.Xna.Framework";
    internal const string MonoGameGraphics = "Microsoft.Xna.Framework.Graphics";
    internal const string MonoGameInput = "Microsoft.Xna.Framework.Input";

    /// <summary>Parses a file with the original project's symbols, so code in active <c>#if</c> branches is seen as the project saw it.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="symbols">The project's compilation symbols.</param>
    internal static SyntaxNode Parse(string text, IReadOnlyCollection<string> symbols) =>
        CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: symbols)).GetRoot();

    /// <summary>Applies edits from the end of the text to the start. An edit that overlaps an edit that is kept is dropped.</summary>
    /// <param name="text">The text the offsets refer to.</param>
    /// <param name="edits">The edits, in any order.</param>
    internal static string Apply(string text, IEnumerable<TextEdit> edits)
    {
        var kept = new List<TextEdit>();
        foreach (var edit in edits.OrderBy(e => e.Start).ThenBy(e => e.End))
        {
            if (kept.Count == 0 || edit.Start >= kept[^1].End)
            {
                kept.Add(edit);
            }
        }

        var builder = new StringBuilder(text);
        for (var i = kept.Count - 1; i >= 0; i--)
        {
            builder.Remove(kept[i].Start, kept[i].End - kept[i].Start).Insert(kept[i].Start, kept[i].NewText);
        }

        return builder.ToString();
    }

    /// <summary>Tells whether the file looks like Toolkit or MonoGame code: it imports or names a SharpDX or Microsoft.Xna.Framework namespace.</summary>
    /// <param name="root">The syntax root.</param>
    internal static bool HasFrameworkContext(SyntaxNode root)
    {
        foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            if (directive.Name is { } name && IsFrameworkName(name.ToString()))
            {
                return true;
            }
        }

        return root.DescendantNodes().OfType<QualifiedNameSyntax>().Any(q => IsFrameworkName(q.ToString()))
            || root.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(m => IsFrameworkName(m.ToString()));
    }

    /// <summary>Tells whether a non-alias <c>using</c> of <paramref name="ns"/> exists anywhere in the file.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="ns">The namespace.</param>
    internal static bool Imports(SyntaxNode root, string ns) =>
        root.DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Any(u => u.Alias is null && u.StaticKeyword.RawKind == 0 && string.Equals(u.Name?.ToString().Trim(), ns, StringComparison.Ordinal));

    /// <summary>Gets the name left of the dot, like the analyzer does: <c>b</c> in <c>b.Name</c> and in <c>a.b.Name</c>.</summary>
    /// <param name="left">The expression left of the dot.</param>
    internal static string? ReceiverName(ExpressionSyntax left) => left switch
    {
        SimpleNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression) => access.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Tells whether an expression is the type name itself, written bare or with a SharpDX or MonoGame namespace in front.</summary>
    /// <param name="expression">The expression left of the dot.</param>
    /// <param name="typeName">The type name, such as Matrix.</param>
    internal static bool IsTypeName(ExpressionSyntax expression, string typeName)
    {
        switch (expression)
        {
            case IdentifierNameSyntax id:
                return id.Identifier.ValueText == typeName;
            case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                return access.Name.Identifier.ValueText == typeName
                    && access.Expression.ToString() is "SharpDX" or "Microsoft.Xna.Framework" or "global::SharpDX" or "global::Microsoft.Xna.Framework";
            default:
                return false;
        }
    }

    /// <summary>Tells whether an expression names a graphics device the way the analyzer does: the last name before the dot is GraphicsDevice.</summary>
    /// <param name="expression">The expression left of the dot.</param>
    internal static bool IsGraphicsDevice(ExpressionSyntax expression) => ReceiverName(expression) == "GraphicsDevice";

    /// <summary>Finds the characters of a whole-line change for a statement or member, so a removed line leaves no blank line behind.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="node">The node to remove.</param>
    /// <param name="includeLeadingComments">True to also remove comment lines directly above the node.</param>
    /// <returns>The edit, or a narrower one when other code shares the line.</returns>
    internal static TextEdit RemoveLines(string text, SyntaxNode node, bool includeLeadingComments)
    {
        var start = LineStart(text, node.SpanStart);
        var end = node.Span.End;
        var lineEnd = end;
        while (lineEnd < text.Length && text[lineEnd] is ' ' or '\t')
        {
            lineEnd++;
        }

        var alone = text[start..node.SpanStart].All(c => c is ' ' or '\t')
            && (lineEnd >= text.Length || text[lineEnd] is '\r' or '\n');
        if (!alone)
        {
            return new TextEdit(node.SpanStart, end, string.Empty);
        }

        if (lineEnd < text.Length && text[lineEnd] == '\r')
        {
            lineEnd++;
        }

        if (lineEnd < text.Length && text[lineEnd] == '\n')
        {
            lineEnd++;
        }

        while (includeLeadingComments && start > 0)
        {
            var previousEnd = start - 1;
            if (previousEnd > 0 && text[previousEnd] == '\n' && text[previousEnd - 1] == '\r')
            {
                previousEnd--;
            }

            var previousStart = LineStart(text, previousEnd);
            var line = text[previousStart..previousEnd].Trim();
            if (!line.StartsWith("//", StringComparison.Ordinal))
            {
                break;
            }

            start = previousStart;
        }

        return new TextEdit(start, lineEnd, string.Empty);
    }

    /// <summary>Gets the offset of the first character of the line that contains <paramref name="position"/>.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="position">An offset into the text.</param>
    internal static int LineStart(string text, int position)
    {
        var i = Math.Min(position, text.Length);
        while (i > 0 && text[i - 1] != '\n' && text[i - 1] != '\r')
        {
            i--;
        }

        return i;
    }

    private static bool IsFrameworkName(string name) =>
        name.StartsWith("SharpDX", StringComparison.Ordinal)
        || name.StartsWith("global::SharpDX", StringComparison.Ordinal)
        || name.StartsWith("Microsoft.Xna.Framework", StringComparison.Ordinal)
        || name.StartsWith("global::Microsoft.Xna.Framework", StringComparison.Ordinal);
}
