using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Source;

/// <summary>
/// Matches namespace, type, and member rules against one parsed file using syntax only.
/// No semantic model and no XNA assemblies are needed, so a type or member name can match an unrelated
/// type that happens to share the name. See the design section "Source matching limits".
/// </summary>
internal static class SourceRuleMatcher
{
    private const int _maxSnippetLength = 200;

    /// <summary>Finds every match of the namespace, type, and member rules in one syntax tree.</summary>
    /// <param name="tree">A parsed C# file.</param>
    /// <param name="rules">Source rules (namespace, type, or member matches). Rules of other match types are ignored.</param>
    /// <returns>
    /// Matches in source order, and in rule order at equal positions. A rule matches at most once per position.
    /// Line and column are 1-based and point to the start of the matched node.
    /// </returns>
    internal static IReadOnlyList<SourceMatch> Match(SyntaxTree tree, IReadOnlyList<Rule> rules)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(rules);

        var index = SourceRuleIndex.Build(rules);
        if (index.IsEmpty)
        {
            return [];
        }

        var hits = SourceWalker.Walk(tree.GetRoot(), index);
        if (hits.Count == 0)
        {
            return [];
        }

        hits.Sort(static (a, b) => a.Position != b.Position ? a.Position.CompareTo(b.Position) : a.Order.CompareTo(b.Order));
        var text = tree.GetText();
        return hits.ConvertAll(hit => ToMatch(text, hit));
    }

    private static SourceMatch ToMatch(SourceText text, RawHit hit)
    {
        var position = text.Lines.GetLinePosition(hit.Position);
        var snippet = text.Lines[position.Line].ToString().Trim();
        if (snippet.Length > _maxSnippetLength)
        {
            snippet = snippet[.._maxSnippetLength];
        }

        return new SourceMatch(hit.Rule, position.Line + 1, position.Character + 1, snippet);
    }
}
