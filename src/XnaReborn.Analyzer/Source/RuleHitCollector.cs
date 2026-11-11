using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Source;

/// <summary>A rule hit before it is turned into a line, column, and snippet.</summary>
/// <param name="Position">Character offset of the start of the matched node.</param>
/// <param name="Order">Index of the rule in the list passed to the matcher.</param>
/// <param name="Rule">The rule that matched.</param>
internal readonly record struct RawHit(int Position, int Order, Rule Rule);

/// <summary>Applies the namespace, type, member, method and preprocessor symbol rules of a <see cref="SourceRuleIndex"/> to the facts of a walker.</summary>
internal sealed class RuleHitCollector : ISourceFactSink
{
    private readonly SourceRuleIndex _index;
    private readonly StringComparison _comparison;
    private readonly StringComparer _comparer;
    private readonly HashSet<string> _imports;
    private readonly Dictionary<string, string> _aliases;
    private readonly List<RawHit> _hits = [];
    private readonly HashSet<(int Position, int Order)> _seen = [];
    private readonly HashSet<int> _firedSymbols = [];

    /// <summary>Creates a collector.</summary>
    /// <param name="index">The rules to look for.</param>
    /// <param name="ignoreCase">True for Visual Basic, where names compare without case.</param>
    /// <param name="implicitImports">Namespaces imported by the project for every file, or null.</param>
    internal RuleHitCollector(SourceRuleIndex index, bool ignoreCase, IEnumerable<string>? implicitImports)
    {
        _index = index;
        _comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        _comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _imports = new HashSet<string>(implicitImports ?? [], _comparer);
        _aliases = new Dictionary<string, string>(_comparer);
    }

    /// <summary>Gets the hits in traversal order. The caller sorts them.</summary>
    internal List<RawHit> Hits => _hits;

    /// <inheritdoc />
    public bool WantsPreprocessorSymbols => _index.Symbols.Count > 0;

    /// <inheritdoc />
    public void DeclareImport(string dottedNamespace) => _imports.Add(dottedNamespace);

    /// <inheritdoc />
    public void Alias(string alias, string target) => _aliases[alias] = target;

    /// <inheritdoc />
    public void UsingDirective(string dottedNamespace, int position)
    {
        if (_index.Namespaces.Count > 0)
        {
            AddNamespaceHits(dottedNamespace, position);
        }
    }

    /// <inheritdoc />
    public void NamespaceChain(string dottedText, int position)
    {
        if (_index.Namespaces.Count > 0)
        {
            // Not resolved through aliases: the import statement already hits, and a second hit at every alias use would change C# reports.
            AddNamespaceHits(dottedText, position);
        }
    }

    /// <inheritdoc />
    public void SimpleName(string name, string? leftText, bool qualified, int position)
    {
        if (!_index.TypesByName.TryGetValue(name, out var candidates))
        {
            return;
        }

        var resolved = leftText is null ? null : Resolve(leftText);
        foreach (var candidate in candidates)
        {
            var applies = qualified
                ? resolved is not null && candidate.Match.Namespaces.Contains(resolved, _comparer)
                : candidate.Match.Namespaces.Any(_imports.Contains);
            if (applies)
            {
                AddHit(position, candidate.Order, candidate.Rule);
            }
        }
    }

    /// <inheritdoc />
    public void MemberAccess(MemberUse use)
    {
        if (!_index.MembersByName.TryGetValue(use.Name, out var candidates))
        {
            return;
        }

        var leftText = use.LeftText is null ? null : Resolve(use.LeftText);
        foreach (var candidate in candidates)
        {
            var match = candidate.Match;
            if (IsGateOpen(match.Namespaces, leftText) && IsShapeMatch(match, use))
            {
                AddHit(use.Position, candidate.Order, candidate.Rule);
            }
        }
    }

    /// <inheritdoc />
    public void MethodDeclaration(string name, bool isOverride, int position)
    {
        if (!_index.MethodsByName.TryGetValue(name, out var candidates))
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            if ((!candidate.Match.Overrides || isOverride) && candidate.Match.Namespaces.Any(_imports.Contains))
            {
                AddHit(position, candidate.Order, candidate.Rule);
            }
        }
    }

    /// <inheritdoc />
    public void PreprocessorSymbol(string name, int position)
    {
        foreach (var rule in _index.Symbols)
        {
            if (rule.Match.Names.Contains(name, _comparer) && _firedSymbols.Add(rule.Order))
            {
                AddHit(position, rule.Order, rule.Rule);
            }
        }
    }

    /// <summary>Replaces an alias at the start of a dotted name by its target.</summary>
    private string Resolve(string dotted)
    {
        if (_aliases.Count == 0)
        {
            return dotted;
        }

        var dot = dotted.IndexOf('.', StringComparison.Ordinal);
        var first = dot < 0 ? dotted : dotted[..dot];
        return _aliases.TryGetValue(first, out var target) ? target + (dot < 0 ? string.Empty : dotted[dot..]) : dotted;
    }

    private void AddNamespaceHits(string text, int position)
    {
        foreach (var rule in _index.Namespaces)
        {
            if (SyntaxNames.IsNamespaceOrChild(text, rule.Match.Namespace, rule.Match.IncludeChildren, _comparison))
            {
                AddHit(position, rule.Order, rule.Rule);
            }
        }
    }

    private bool IsGateOpen(IReadOnlyList<string> namespaces, string? leftText) =>
        namespaces.Any(_imports.Contains) || SyntaxNames.IsQualifiedBy(leftText, namespaces, _comparison);

    private bool IsShapeMatch(MemberMatch match, MemberUse use)
    {
        if (match.Receiver is not null && !string.Equals(match.Receiver, use.Receiver, _comparison))
        {
            return false;
        }

        if ((match.Kind == MemberMatchKind.Invocation && !use.IsInvocation)
            || (match.Kind == MemberMatchKind.Access && use.IsInvocation))
        {
            return false;
        }

        if (match.Arguments is null)
        {
            return true;
        }

        if (!use.IsInvocation)
        {
            return false;
        }

        return (match.Arguments.Min is not { } min || use.ArgumentCount >= min) && (match.Arguments.Max is not { } max || use.ArgumentCount <= max);
    }

    private void AddHit(int position, int order, Rule rule)
    {
        if (_seen.Add((position, order)))
        {
            _hits.Add(new RawHit(position, order, rule));
        }
    }
}
