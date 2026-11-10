using System.Text.RegularExpressions;
using XnaReborn.Runner.Diagnostics;

namespace XnaReborn.Runner.Hints;

/// <summary>Finds the hint for a build diagnostic or for a game's captured output.</summary>
public sealed class HintMatcher
{
    private readonly HintCatalog _catalog;

    /// <summary>Creates a matcher over the shipped catalog.</summary>
    public HintMatcher()
        : this(HintCatalog.Default)
    {
    }

    /// <summary>Creates a matcher over a catalog.</summary>
    /// <param name="catalog">The hint catalog.</param>
    public HintMatcher(HintCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>
    /// Finds the first hint for a build diagnostic. Order: hints with a code and messageContains, then hints
    /// with only a code, then hints with a message condition and no code (messageContains or messageRegex).
    /// A messageRegex next to a code also needs that code. The first match in each step, in catalog order, wins.
    /// </summary>
    /// <param name="diagnostic">The build diagnostic.</param>
    /// <returns>The hint, or null when no known fix exists.</returns>
    public FixHint? Match(BuildDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        var build = _catalog.Entries.Where(e => !e.Match.IsRuntime).ToList();

        foreach (var e in build.Where(e => e.Match.Code is not null && e.Match.MessageContains is not null && e.Match.MessageRegex is null))
        {
            if (CodeMatches(e, diagnostic) && Contains(diagnostic.Message, e.Match.MessageContains!))
            {
                return e.Hint;
            }
        }

        foreach (var e in build.Where(e => e.Match.Code is not null && e.Match.MessageContains is null && e.Match.MessageRegex is null))
        {
            if (CodeMatches(e, diagnostic))
            {
                return e.Hint;
            }
        }

        foreach (var e in build.Where(e => e.Match.Code is null || e.Match.MessageRegex is not null))
        {
            if (e.Match.Code is not null && !CodeMatches(e, diagnostic))
            {
                continue;
            }

            if (e.Match.MessageContains is not null && !Contains(diagnostic.Message, e.Match.MessageContains))
            {
                continue;
            }

            if (e.MessageRegex is not null && !IsMatch(e.MessageRegex, diagnostic.Message))
            {
                continue;
            }

            return e.Hint;
        }

        return null;
    }

    /// <summary>
    /// Finds every runtime hint that matches the captured output of a game, in catalog order, without repeats.
    /// A hint with both runtimeContains and runtimeRegex matches when either one matches.
    /// </summary>
    /// <param name="capturedOutput">Standard output and standard error of the game.</param>
    /// <returns>The matching hints; empty when nothing is recognized.</returns>
    public IReadOnlyList<FixHint> MatchRuntime(string capturedOutput)
    {
        if (string.IsNullOrEmpty(capturedOutput))
        {
            return [];
        }

        var found = new List<FixHint>();
        foreach (var e in _catalog.Entries.Where(e => e.Match.IsRuntime))
        {
            var hit = (e.Match.RuntimeContains is not null && Contains(capturedOutput, e.Match.RuntimeContains))
                || (e.RuntimeRegex is not null && IsMatch(e.RuntimeRegex, capturedOutput));
            if (hit && !found.Contains(e.Hint))
            {
                found.Add(e.Hint);
            }
        }

        return found;
    }

    private static bool CodeMatches(HintEntry entry, BuildDiagnostic diagnostic) =>
        string.Equals(entry.Match.Code, diagnostic.Code, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string text, string part) =>
        text.Contains(part, StringComparison.OrdinalIgnoreCase);

    private static bool IsMatch(Regex regex, string text)
    {
        try
        {
            return regex.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
