using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Rewrites <c>Color.TransparentBlack</c> to <c>Color.Transparent</c> (MG032). MonoGame 3.8.1 removed <c>TransparentBlack</c>, and both are (0, 0, 0, 0).
/// The change is made only when the file shows that <c>Color</c> is the XNA one: it imports <c>Microsoft.Xna.Framework</c>, aliases
/// <c>Microsoft.Xna.Framework.Color</c>, or uses the full name. Any other <c>Color.TransparentBlack</c> gets a marker and is left alone.
/// </summary>
public sealed class ColorTransparentBlackRewriter : ISourceRewriter
{
    /// <summary>The rule ID.</summary>
    public const string Id = "MG032";

    private const string _namespace = "Microsoft.Xna.Framework";
    private const string _colorName = "Microsoft.Xna.Framework.Color";
    private const string _from = "TransparentBlack";
    private const string _to = "Transparent";
    private const string _markerMessage = "Color.TransparentBlack was removed in MonoGame 3.8.1. If this is the XNA Color, use Color.Transparent.";

    /// <inheritdoc />
    public string RuleId => Id;

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: context.OriginalSymbols);
        var root = CSharpSyntaxTree.ParseText(context.Text, options).GetRoot();
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
        var imported = usings.Any(u => u.Alias is null && u.StaticKeyword.IsKind(SyntaxKind.None) && u.Name?.ToString().Trim() == _namespace);
        var aliases = usings
            .Where(u => u.Alias is not null && u.Name?.ToString().Trim() == _colorName)
            .Select(u => u.Alias!.Name.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

        var spans = Accesses(root)
            .Where(a => IsXnaColor(a.Expression.ToString().Trim(), imported, aliases))
            .Select(a => a.Name.Span)
            .OrderByDescending(s => s.Start)
            .ToList();
        var text = context.Text;
        foreach (var span in spans)
        {
            text = text.Remove(span.Start, span.Length).Insert(span.Start, _to);
        }

        var notes = new List<RewriteNote>();
        if (spans.Count > 0)
        {
            notes.Add(new RewriteNote(Id, $"{spans.Count} use(s) of Color.TransparentBlack changed to Color.Transparent.", NeedsManualWork: false));
        }

        // What is left is a Color that the file does not show to be the XNA one. Mark it from the end so earlier offsets stay valid.
        var rest = Accesses(CSharpSyntaxTree.ParseText(text, options).GetRoot()).Where(a => a.Expression.ToString().Trim() == "Color").Select(a => a.SpanStart).OrderByDescending(p => p).ToList();
        var marked = 0;
        foreach (var position in rest)
        {
            var next = MarkerInserter.InsertMarker(text, position, Id, _markerMessage, context.OriginalSymbols);
            if (!string.Equals(next, text, StringComparison.Ordinal))
            {
                marked++;
                text = next;
            }
        }

        if (marked > 0)
        {
            notes.Add(new RewriteNote(Id, $"{marked} use(s) of Color.TransparentBlack left as they are with a marker: the file does not show that Color is the XNA Color.", NeedsManualWork: true));
        }

        return new SourceRewriteResult(text, notes);
    }

    private static IEnumerable<MemberAccessExpressionSyntax> Accesses(SyntaxNode root) =>
        root.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Where(a => a.IsKind(SyntaxKind.SimpleMemberAccessExpression) && a.Name.Identifier.ValueText == _from);

    private static bool IsXnaColor(string receiver, bool imported, HashSet<string> aliases) =>
        receiver is _colorName or "global::" + _colorName || aliases.Contains(receiver) || (imported && receiver == "Color");
}
