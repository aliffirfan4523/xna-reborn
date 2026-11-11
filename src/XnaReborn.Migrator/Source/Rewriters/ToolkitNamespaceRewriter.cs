using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XnaReborn.Core.Toolkit;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Rewrites one SharpDX Toolkit namespace to its MonoGame namespace (SDX020 to SDX024, one instance per rule): <c>using</c> directives,
/// <c>using</c> aliases and fully qualified names. A directive whose new namespace is already imported becomes a duplicate and is removed.
/// Namespaces that are not in the map (SharpDX.Direct3D11 and others) are never touched.
/// </summary>
public sealed class ToolkitNamespaceRewriter : ISourceRewriter
{
    private readonly ToolkitNamespaceMapping _mapping;
    private readonly string[] _fromParts;

    /// <summary>Creates the rewriter for one namespace rule of the map.</summary>
    /// <param name="ruleId">A rule ID from SDX020 to SDX024 that the map has a mapping for.</param>
    /// <param name="map">The rename map. Defaults to <see cref="ToolkitMap.Default"/>.</param>
    /// <exception cref="ArgumentException">The map has no mapping for <paramref name="ruleId"/>.</exception>
    public ToolkitNamespaceRewriter(string ruleId, ToolkitMap? map = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        _mapping = (map ?? ToolkitMap.Default).Namespaces.FirstOrDefault(m => m.RuleId == ruleId)
            ?? throw new ArgumentException($"The Toolkit map has no namespace mapping for {ruleId}.", nameof(ruleId));
        _fromParts = _mapping.From.Split('.');
        RuleId = ruleId;
    }

    /// <inheritdoc />
    public string RuleId { get; }

    /// <summary>Gets the rule IDs of every namespace mapping in <paramref name="map"/>.</summary>
    /// <param name="map">The rename map. Defaults to <see cref="ToolkitMap.Default"/>.</param>
    public static IReadOnlyList<string> RuleIds(ToolkitMap? map = null) =>
        [.. (map ?? ToolkitMap.Default).Namespaces.Select(m => m.RuleId)];

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var root = ToolkitSyntax.Parse(context.Text, context.OriginalSymbols);

        var edits = new List<TextEdit>();
        var directivesRewritten = new HashSet<UsingDirectiveSyntax>();
        var qualified = 0;
        foreach (var chain in OuterChains(root))
        {
            var segments = Segments(chain);
            if (segments is null || !StartsWithFrom(segments))
            {
                continue;
            }

            var exact = segments.Count == _fromParts.Length;
            var typeFollows = segments.Count > _fromParts.Length
                && segments[_fromParts.Length] is IdentifierNameSyntax or GenericNameSyntax
                && _mapping.QualifiedTypes.Contains(segments[_fromParts.Length].Identifier.ValueText, StringComparer.Ordinal);
            if (exact && chain.Parent is UsingDirectiveSyntax { Alias: null, StaticKeyword.RawKind: 0 } directive && directive.NamespaceOrType == chain)
            {
                directivesRewritten.Add(directive);
                continue;
            }

            if (exact && chain.Parent is UsingDirectiveSyntax { Alias: not null })
            {
                edits.Add(new TextEdit(segments[0].SpanStart, segments[^1].Span.End, _mapping.To));
            }
            else if (typeFollows)
            {
                edits.Add(new TextEdit(segments[0].SpanStart, segments[_fromParts.Length - 1].Span.End, _mapping.To));
                qualified++;
            }
        }

        var (renamed, removed) = PlanUsings(root, context.Text, directivesRewritten, edits);
        if (edits.Count == 0)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var text = ToolkitSyntax.Apply(context.Text, edits);
        if (string.Equals(text, context.Text, StringComparison.Ordinal))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var message = $"Rewrote {renamed} using(s) and {qualified} qualified name(s) from {_mapping.From} to {_mapping.To}"
            + (removed > 0 ? $", and removed {removed} using(s) that became duplicates." : ".");
        return new SourceRewriteResult(text, [new RewriteNote(RuleId, message, NeedsManualWork: false)]);
    }

    // Plans the edit of every rewritten using directive: rename it, or delete it when the target namespace is already imported in the same scope.
    private (int Renamed, int Removed) PlanUsings(SyntaxNode root, string text, HashSet<UsingDirectiveSyntax> rewritten, List<TextEdit> edits)
    {
        var renamed = 0;
        var removed = 0;
        foreach (var scope in root.DescendantNodesAndSelf().Where(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax))
        {
            var usings = scope switch
            {
                CompilationUnitSyntax unit => unit.Usings,
                BaseNamespaceDeclarationSyntax ns => ns.Usings,
                _ => default,
            };
            var seen = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var directive in usings)
            {
                if (directive.Alias is not null || directive.StaticKeyword.RawKind != 0 || directive.NamespaceOrType is null)
                {
                    continue;
                }

                var isRewritten = rewritten.Contains(directive);
                var finalName = isRewritten ? _mapping.To : directive.NamespaceOrType.ToString().Trim();
                if (seen.TryGetValue(finalName, out var earlierRewritten))
                {
                    // A duplicate caused by a rewrite goes. A duplicate that was already in the file stays as the author left it.
                    if (isRewritten || earlierRewritten)
                    {
                        edits.Add(ToolkitSyntax.RemoveLines(text, directive, includeLeadingComments: false));
                        removed++;
                    }

                    continue;
                }

                seen[finalName] = isRewritten;
                if (isRewritten)
                {
                    edits.Add(new TextEdit(directive.NamespaceOrType.SpanStart, directive.NamespaceOrType.Span.End, _mapping.To));
                    renamed++;
                }
            }
        }

        return (renamed, removed);
    }

    private bool StartsWithFrom(List<SimpleNameSyntax> segments)
    {
        if (segments.Count < _fromParts.Length)
        {
            return false;
        }

        for (var i = 0; i < _fromParts.Length; i++)
        {
            if (segments[i] is not IdentifierNameSyntax id || id.Identifier.ValueText != _fromParts[i])
            {
                return false;
            }
        }

        return true;
    }

    // Dotted chains that are not the left part of a longer chain and are not the name of a namespace declaration.
    private static IEnumerable<SyntaxNode> OuterChains(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes())
        {
            // `using SharpDX;` is one plain name, not a dotted chain, but it is a whole namespace name all the same.
            if (node is IdentifierNameSyntax { Parent: UsingDirectiveSyntax plain } && plain.NamespaceOrType == node)
            {
                yield return node;
                continue;
            }

            if (node is not (QualifiedNameSyntax or MemberAccessExpressionSyntax))
            {
                continue;
            }

            if (node is MemberAccessExpressionSyntax access && !access.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                continue;
            }

            if (node.Parent is QualifiedNameSyntax qualifiedParent && qualifiedParent.Left == node)
            {
                continue;
            }

            if (node.Parent is MemberAccessExpressionSyntax accessParent && accessParent.Expression == node && accessParent.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                continue;
            }

            if (node.Parent is BaseNamespaceDeclarationSyntax)
            {
                continue;
            }

            yield return node;
        }
    }

    // The identifiers of a chain from left to right, or null when the chain does not start with a plain name (this.x, a call, and so on).
    private static List<SimpleNameSyntax>? Segments(SyntaxNode chain)
    {
        var segments = new List<SimpleNameSyntax>();
        var current = chain;
        while (true)
        {
            switch (current)
            {
                case QualifiedNameSyntax qualified:
                    segments.Add(qualified.Right);
                    current = qualified.Left;
                    break;
                case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                    segments.Add(access.Name);
                    current = access.Expression;
                    break;
                case IdentifierNameSyntax or GenericNameSyntax:
                    segments.Add((SimpleNameSyntax)current);
                    segments.Reverse();
                    return segments;
                default:
                    return null;
            }
        }
    }
}
