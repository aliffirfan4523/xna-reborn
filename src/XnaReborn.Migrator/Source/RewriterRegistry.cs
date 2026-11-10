using System.Diagnostics.CodeAnalysis;

namespace XnaReborn.Migrator.Source;

/// <summary>Holds the source rewriters, keyed by rule ID.</summary>
public sealed class RewriterRegistry
{
    private readonly SortedDictionary<string, ISourceRewriter> _rewriters = new(StringComparer.Ordinal);

    /// <summary>Gets the registered rule IDs in ordinal order.</summary>
    public IReadOnlyList<string> RuleIds => [.. _rewriters.Keys];

    /// <summary>Gets the registered rewriters in rule ID order.</summary>
    public IReadOnlyList<ISourceRewriter> All => [.. _rewriters.Values];

    /// <summary>Adds a rewriter.</summary>
    /// <param name="rewriter">The rewriter to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rewriter"/> is null.</exception>
    /// <exception cref="ArgumentException">The rule ID is empty, or a rewriter with the same rule ID is already registered.</exception>
    public void Register(ISourceRewriter rewriter)
    {
        ArgumentNullException.ThrowIfNull(rewriter);
        var id = rewriter.RuleId;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A rewriter needs a rule ID.", nameof(rewriter));
        }

        if (!_rewriters.TryAdd(id, rewriter))
        {
            throw new ArgumentException($"A rewriter for rule '{id}' is already registered.", nameof(rewriter));
        }
    }

    /// <summary>Looks up a rewriter by rule ID.</summary>
    /// <param name="ruleId">The rule ID, compared ordinally.</param>
    /// <param name="rewriter">The rewriter, when found.</param>
    /// <returns>True when a rewriter is registered for the rule ID.</returns>
    public bool TryGet(string ruleId, [NotNullWhen(true)] out ISourceRewriter? rewriter)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        return _rewriters.TryGetValue(ruleId, out rewriter);
    }
}
