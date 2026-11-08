using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Source;

/// <summary>A source rule paired with its position in the caller's rule list.</summary>
/// <typeparam name="T">The match type of the rule.</typeparam>
/// <param name="Order">Index of the rule in the list passed to the matcher.</param>
/// <param name="Rule">The rule.</param>
/// <param name="Match">The rule's match, already cast to its concrete type.</param>
internal readonly record struct IndexedRule<T>(int Order, Rule Rule, T Match)
    where T : RuleMatch;

/// <summary>The namespace, type, and member rules of one matcher call, grouped for fast lookup.</summary>
internal sealed class SourceRuleIndex
{
    private readonly List<IndexedRule<NamespaceMatch>> _namespaces = [];
    private readonly Dictionary<string, List<IndexedRule<TypeMatch>>> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<IndexedRule<MemberMatch>>> _members = new(StringComparer.Ordinal);

    private SourceRuleIndex()
    {
    }

    /// <summary>Gets the namespace rules in rule order.</summary>
    internal IReadOnlyList<IndexedRule<NamespaceMatch>> Namespaces => _namespaces;

    /// <summary>Gets the type rules by type name.</summary>
    internal IReadOnlyDictionary<string, List<IndexedRule<TypeMatch>>> TypesByName => _types;

    /// <summary>Gets the member rules by member name.</summary>
    internal IReadOnlyDictionary<string, List<IndexedRule<MemberMatch>>> MembersByName => _members;

    /// <summary>Gets a value indicating whether no rule in the list is a source rule.</summary>
    internal bool IsEmpty => _namespaces.Count == 0 && _types.Count == 0 && _members.Count == 0;

    /// <summary>Builds the index. Rules of other match types and repeated rule instances are skipped.</summary>
    /// <param name="rules">The rules passed to the matcher.</param>
    /// <returns>The index.</returns>
    internal static SourceRuleIndex Build(IReadOnlyList<Rule> rules)
    {
        var index = new SourceRuleIndex();
        var seen = new HashSet<Rule>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (!seen.Add(rule))
            {
                continue;
            }

            switch (rule.Match)
            {
                case NamespaceMatch ns:
                    index._namespaces.Add(new IndexedRule<NamespaceMatch>(i, rule, ns));
                    break;
                case TypeMatch type:
                    Add(index._types, type.Name, new IndexedRule<TypeMatch>(i, rule, type));
                    break;
                case MemberMatch member:
                    Add(index._members, member.Name, new IndexedRule<MemberMatch>(i, rule, member));
                    break;
            }
        }

        return index;
    }

    private static void Add<T>(Dictionary<string, List<IndexedRule<T>>> map, string name, IndexedRule<T> entry)
        where T : RuleMatch
    {
        if (!map.TryGetValue(name, out var list))
        {
            list = [];
            map[name] = list;
        }

        list.Add(entry);
    }
}
