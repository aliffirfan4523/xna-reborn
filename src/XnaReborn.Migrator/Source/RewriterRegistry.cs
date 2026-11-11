using System.Diagnostics.CodeAnalysis;
using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Source;

/// <summary>Holds the source rewriters, keyed by rule ID and language.</summary>
public sealed class RewriterRegistry
{
    private readonly SortedDictionary<(string RuleId, SourceLanguage Language), ISourceRewriter> _rewriters = new(new KeyComparer());

    /// <summary>Gets the registered rule IDs in ordinal order. A rule that has rewriters for several languages is listed once.</summary>
    public IReadOnlyList<string> RuleIds => [.. _rewriters.Keys.Select(k => k.RuleId).Distinct(StringComparer.Ordinal)];

    /// <summary>Gets the registered rewriters in rule ID order, then language order.</summary>
    public IReadOnlyList<ISourceRewriter> All => [.. _rewriters.Values];

    /// <summary>Adds a rewriter.</summary>
    /// <param name="rewriter">The rewriter to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rewriter"/> is null.</exception>
    /// <exception cref="ArgumentException">The rule ID is empty, or a rewriter with the same rule ID and language is already registered.</exception>
    public void Register(ISourceRewriter rewriter)
    {
        ArgumentNullException.ThrowIfNull(rewriter);
        var id = rewriter.RuleId;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A rewriter needs a rule ID.", nameof(rewriter));
        }

        if (!_rewriters.TryAdd((id, rewriter.Language), rewriter))
        {
            throw new ArgumentException($"A rewriter for rule '{id}' and language '{rewriter.Language}' is already registered.", nameof(rewriter));
        }
    }

    /// <summary>Looks up the C# rewriter of a rule.</summary>
    /// <param name="ruleId">The rule ID, compared ordinally.</param>
    /// <param name="rewriter">The rewriter, when found.</param>
    /// <returns>True when a C# rewriter is registered for the rule ID.</returns>
    public bool TryGet(string ruleId, [NotNullWhen(true)] out ISourceRewriter? rewriter) => TryGet(ruleId, SourceLanguage.CSharp, out rewriter);

    /// <summary>Looks up the rewriter of a rule for a language.</summary>
    /// <param name="ruleId">The rule ID, compared ordinally.</param>
    /// <param name="language">The language of the files.</param>
    /// <param name="rewriter">The rewriter, when found.</param>
    /// <returns>True when a rewriter is registered for the rule ID and language.</returns>
    public bool TryGet(string ruleId, SourceLanguage language, [NotNullWhen(true)] out ISourceRewriter? rewriter)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        return _rewriters.TryGetValue((ruleId, language), out rewriter);
    }

    private sealed class KeyComparer : IComparer<(string RuleId, SourceLanguage Language)>
    {
        public int Compare((string RuleId, SourceLanguage Language) x, (string RuleId, SourceLanguage Language) y)
        {
            var byId = string.CompareOrdinal(x.RuleId, y.RuleId);
            return byId != 0 ? byId : x.Language.CompareTo(y.Language);
        }
    }
}
