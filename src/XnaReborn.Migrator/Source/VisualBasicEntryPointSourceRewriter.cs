using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Source;

/// <summary>
/// The Visual Basic entry point fix. It removes platform conditionals (for example <c>#If WINDOWS OrElse XBOX Then</c>) that wrap
/// <c>Sub Main</c>, so that the program still has an entry point after the project is regenerated without those symbols.
/// </summary>
public sealed class VisualBasicEntryPointSourceRewriter : ISourceRewriter
{
    private static readonly HashSet<string> _platformSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        "WINDOWS", "XBOX", "XBOX360", "ZUNE", "WINDOWS_PHONE", "LINUX",
    };

    private static readonly HashSet<string> _undefinedSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        "NETFX_CORE", "WINDOWS_APP", "WINDOWS_PHONE_APP", "WINDOWS_UWP", "WINRT",
    };

    /// <inheritdoc />
    public string RuleId => EntryPointSourceRewriter.Id;

    /// <inheritdoc />
    public SourceLanguage Language => SourceLanguage.VisualBasic;

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var symbols = context.OriginalSymbols.Select(s => new KeyValuePair<string, object>(s, true)).ToList();
        var root = VisualBasicSyntaxTree.ParseText(context.Text, new VisualBasicParseOptions(preprocessorSymbols: symbols)).GetRoot();

        var main = root.DescendantNodes().OfType<MethodStatementSyntax>().FirstOrDefault(IsEntryPoint);
        if (main is null)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var regions = FindEnclosingRegions(root, main.SpanStart).Where(r => !IsOnly(r.If.Condition, _undefinedSymbols)).ToList();
        if (regions.Count == 0)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        foreach (var region in regions)
        {
            if (region.HasBranch)
            {
                return Skipped(context, region.Condition, "has an '#Else' or '#ElseIf'");
            }

            if (region.EndIf is null || !IsOnly(region.If.Condition, _platformSymbols))
            {
                return Skipped(context, region.Condition, null);
            }
        }

        var text = context.Text;
        var ranges = regions.SelectMany(r => new[] { LineRange(text, r.If.SpanStart), LineRange(text, r.EndIf!.SpanStart) });
        foreach (var (start, end) in ranges.OrderByDescending(r => r.Start))
        {
            text = text.Remove(start, end - start);
        }

        var removed = string.Join(" ", regions.Select(r => $"Removed '#If {r.Condition}' around the entry point."));
        return new SourceRewriteResult(text, [new RewriteNote(RuleId, removed, NeedsManualWork: false)]);
    }

    private static SourceRewriteResult Skipped(SourceRewriteContext context, string condition, string? reason)
    {
        var note = reason is null
            ? $"The entry point is inside '#If {condition}'. Check the conditional by hand."
            : $"The entry point is inside '#If {condition}', which {reason}. Check the conditional by hand.";
        return new SourceRewriteResult(context.Text, [new RewriteNote(EntryPointSourceRewriter.Id, note, NeedsManualWork: true)]);
    }

    // Shared Sub Main in a class or structure, or Sub Main in a module.
    private static bool IsEntryPoint(MethodStatementSyntax method) =>
        method.SubOrFunctionKeyword.IsKind(SyntaxKind.SubKeyword)
        && method.Identifier.ValueText.Equals("Main", StringComparison.OrdinalIgnoreCase)
        && (method.Modifiers.Any(SyntaxKind.SharedKeyword) || method.Parent?.Parent is ModuleBlockSyntax);

    private static List<Region> FindEnclosingRegions(SyntaxNode root, int position)
    {
        var stack = new Stack<Region>();
        var enclosing = new List<Region>();
        foreach (var directive in root.DescendantNodes(descendIntoTrivia: true).OfType<DirectiveTriviaSyntax>())
        {
            switch (directive)
            {
                case IfDirectiveTriviaSyntax ifDirective when ifDirective.ElseKeyword.IsKind(SyntaxKind.None):
                    stack.Push(new Region(ifDirective));
                    break;
                case IfDirectiveTriviaSyntax or ElseDirectiveTriviaSyntax:
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

        enclosing.AddRange(stack.Where(open => open.If.SpanStart < position));
        return [.. enclosing.OrderBy(r => r.If.SpanStart)];
    }

    private static bool IsOnly(ExpressionSyntax expression, HashSet<string> symbols) => expression switch
    {
        IdentifierNameSyntax id => !id.IsMissing && symbols.Contains(id.Identifier.ValueText),
        ParenthesizedExpressionSyntax p => IsOnly(p.Expression, symbols),
        UnaryExpressionSyntax u when u.IsKind(SyntaxKind.NotExpression) => IsOnly(u.Operand, symbols),
        BinaryExpressionSyntax b when b.IsKind(SyntaxKind.OrElseExpression) || b.IsKind(SyntaxKind.OrExpression)
            || b.IsKind(SyntaxKind.AndAlsoExpression) || b.IsKind(SyntaxKind.AndExpression) => IsOnly(b.Left, symbols) && IsOnly(b.Right, symbols),
        _ => false,
    };

    private static (int Start, int End) LineRange(string text, int position)
    {
        var start = position;
        while (start > 0 && text[start - 1] != '\n')
        {
            start--;
        }

        var newline = text.IndexOf('\n', position);
        return (start, newline < 0 ? text.Length : newline + 1);
    }

    private sealed class Region(IfDirectiveTriviaSyntax ifDirective)
    {
        public IfDirectiveTriviaSyntax If { get; } = ifDirective;

        public EndIfDirectiveTriviaSyntax? EndIf { get; set; }

        public bool HasBranch { get; set; }

        public string Condition { get; } = ifDirective.Condition.ToString().Trim();
    }
}
