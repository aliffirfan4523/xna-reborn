using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source;

/// <summary>
/// Removes platform conditionals (for example <c>#if WINDOWS || XBOX</c>) that wrap the program entry point,
/// so that <c>Main</c> still compiles after the project is regenerated without those symbols.
/// </summary>
public sealed class EntryPointRewriter
{
    private static readonly HashSet<string> _platformSymbols = new(StringComparer.Ordinal)
    {
        "WINDOWS",
        "XBOX",
        "XBOX360",
        "ZUNE",
        "WINDOWS_PHONE",
    };

    /// <summary>Unwraps platform-only conditionals around the entry point of one source file.</summary>
    /// <param name="sourceText">Text of one .cs file.</param>
    /// <param name="originalSymbols">The original project's DefineConstants, such as WINDOWS, DEBUG and TRACE.</param>
    /// <returns>The new text, a status and a note for a person.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EntryPointRewrite Rewrite(string sourceText, IReadOnlyCollection<string> originalSymbols)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(originalSymbols);

        var options = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: originalSymbols);
        var tree = CSharpSyntaxTree.ParseText(sourceText, options);
        var root = tree.GetRoot();

        var position = FindEntryPointPosition(root);
        if (position is null)
        {
            return new EntryPointRewrite(sourceText, EntryPointStatus.NoEntryPoint, null);
        }

        var regions = FindEnclosingRegions(root, position.Value);
        if (regions.Count == 0)
        {
            return new EntryPointRewrite(sourceText, EntryPointStatus.Unchanged, null);
        }

        foreach (var region in regions)
        {
            if (region.HasBranch)
            {
                return Skipped(sourceText, region.Condition, "has an '#else' or '#elif'");
            }

            if (region.EndIf is null || !IsPlatformOnly(region.If.Condition))
            {
                return Skipped(sourceText, region.Condition, null);
            }
        }

        var ranges = new List<(int Start, int End)>();
        foreach (var region in regions)
        {
            ranges.Add(LineRange(sourceText, region.If.SpanStart));
            ranges.Add(LineRange(sourceText, region.EndIf!.SpanStart));
        }

        var text = sourceText;
        foreach (var (start, end) in ranges.OrderByDescending(r => r.Start))
        {
            text = text.Remove(start, end - start);
        }

        var removed = string.Join(" ", regions.Select(r => $"Removed '#if {r.Condition}' around the entry point."));
        return new EntryPointRewrite(text, EntryPointStatus.Unwrapped, removed);
    }

    private static EntryPointRewrite Skipped(string text, string condition, string? reason)
    {
        var note = reason is null
            ? $"The entry point is inside '#if {condition}'. Check the conditional by hand."
            : $"The entry point is inside '#if {condition}', which {reason}. Check the conditional by hand.";
        return new EntryPointRewrite(text, EntryPointStatus.Skipped, note);
    }

    private static int? FindEntryPointPosition(SyntaxNode root)
    {
        var main = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == "Main"
                && m.Modifiers.Any(SyntaxKind.StaticKeyword)
                && m.Parent is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax);
        if (main is not null)
        {
            return main.SpanStart;
        }

        var global = root.DescendantNodes().OfType<GlobalStatementSyntax>().FirstOrDefault();
        return global?.SpanStart;
    }

    private static List<Region> FindEnclosingRegions(SyntaxNode root, int position)
    {
        var stack = new Stack<Region>();
        var enclosing = new List<Region>();
        foreach (var directive in root.DescendantNodes(descendIntoTrivia: true).OfType<DirectiveTriviaSyntax>())
        {
            switch (directive)
            {
                case IfDirectiveTriviaSyntax ifDirective:
                    stack.Push(new Region(ifDirective));
                    break;
                case ElseDirectiveTriviaSyntax or ElifDirectiveTriviaSyntax:
                    if (stack.Count > 0)
                    {
                        stack.Peek().HasBranch = true;
                    }

                    break;
                case EndIfDirectiveTriviaSyntax endIf:
                    if (stack.Count > 0)
                    {
                        var region = stack.Pop();
                        region.EndIf = endIf;
                        if (region.If.SpanStart < position && position < endIf.SpanStart)
                        {
                            enclosing.Add(region);
                        }
                    }

                    break;
            }
        }

        // Unterminated regions that contain the entry point.
        foreach (var open in stack)
        {
            if (open.If.SpanStart < position)
            {
                enclosing.Add(open);
            }
        }

        return enclosing.OrderBy(r => r.If.SpanStart).ToList();
    }

    private static bool IsPlatformOnly(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax id => !id.IsMissing && _platformSymbols.Contains(id.Identifier.ValueText),
        ParenthesizedExpressionSyntax p => IsPlatformOnly(p.Expression),
        PrefixUnaryExpressionSyntax u when u.IsKind(SyntaxKind.LogicalNotExpression) => IsPlatformOnly(u.Operand),
        BinaryExpressionSyntax b when b.IsKind(SyntaxKind.LogicalOrExpression) || b.IsKind(SyntaxKind.LogicalAndExpression)
            => IsPlatformOnly(b.Left) && IsPlatformOnly(b.Right),
        _ => false,
    };

    /// <summary>Returns the range covering the whole line that contains <paramref name="position"/>, including its newline.</summary>
    private static (int Start, int End) LineRange(string text, int position)
    {
        var start = position;
        while (start > 0 && text[start - 1] != '\n')
        {
            start--;
        }

        var newline = text.IndexOf('\n', position);
        var end = newline < 0 ? text.Length : newline + 1;
        return (start, end);
    }

    private sealed class Region(IfDirectiveTriviaSyntax ifDirective)
    {
        public IfDirectiveTriviaSyntax If { get; } = ifDirective;

        public EndIfDirectiveTriviaSyntax? EndIf { get; set; }

        public bool HasBranch { get; set; }

        public string Condition { get; } = ifDirective.Condition.ToString().Trim();
    }
}
