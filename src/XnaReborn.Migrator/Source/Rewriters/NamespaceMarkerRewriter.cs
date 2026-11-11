using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Puts a <c>// XNAREBORN:</c> marker above each <c>using</c> of a namespace that has no MonoGame 3.8.6.1 equivalent and no stand-in
/// (MG028 to MG031). The code is not changed, so the compiler errors that follow point at the marked lines.
/// </summary>
public sealed class NamespaceMarkerRewriter : ISourceRewriter
{
    private readonly string[] _namespaces;
    private readonly string _message;

    /// <summary>Creates a marker for one rule.</summary>
    /// <param name="ruleId">The rule ID, such as MG030.</param>
    /// <param name="namespaces">The namespaces. A <c>using</c> of the namespace or of a child namespace is marked.</param>
    /// <param name="message">What the person has to do.</param>
    public NamespaceMarkerRewriter(string ruleId, string[] namespaces, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(namespaces);
        ArgumentNullException.ThrowIfNull(message);
        RuleId = ruleId;
        _namespaces = namespaces;
        _message = message;
    }

    /// <inheritdoc />
    public string RuleId { get; }

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var root = CSharpSyntaxTree.ParseText(context.Text, new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: context.OriginalSymbols)).GetRoot();
        var positions = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Where(u => u.Name is { } n && _namespaces.Any(ns => IsNamespaceOrChild(n.ToString().Trim(), ns)))
            .Select(u => u.SpanStart)
            .OrderByDescending(p => p);

        var text = context.Text;
        var marked = 0;
        foreach (var position in positions)
        {
            var next = MarkerInserter.InsertMarker(text, position, RuleId, _message, context.OriginalSymbols);
            if (!string.Equals(next, text, StringComparison.Ordinal))
            {
                marked++;
                text = next;
            }
        }

        return marked == 0
            ? SourceRewriteResult.Unchanged(context)
            : new SourceRewriteResult(text, [new RewriteNote(RuleId, $"{marked} using(s) left as they are with a marker. {_message}", NeedsManualWork: true)]);
    }

    private static bool IsNamespaceOrChild(string name, string ns) =>
        name.Equals(ns, StringComparison.Ordinal) || name.StartsWith(ns + ".", StringComparison.Ordinal);
}
