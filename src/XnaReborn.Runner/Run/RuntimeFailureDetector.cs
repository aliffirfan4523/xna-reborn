using XnaReborn.Runner.Hints;

namespace XnaReborn.Runner.Run;

/// <summary>Recognizes known startup and runtime failures in the output a game wrote.</summary>
public sealed class RuntimeFailureDetector
{
    private readonly HintMatcher _matcher;

    /// <summary>Creates a detector over the shipped hint catalog.</summary>
    public RuntimeFailureDetector()
        : this(new HintMatcher())
    {
    }

    /// <summary>Creates a detector that uses a matcher.</summary>
    /// <param name="matcher">The matcher over the hint catalog.</param>
    public RuntimeFailureDetector(HintMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        _matcher = matcher;
    }

    /// <summary>Finds the runtime hints that match the game's captured output.</summary>
    /// <param name="stdout">Captured standard output.</param>
    /// <param name="stderr">Captured standard error.</param>
    /// <returns>Matching hints in catalog order, without repeats; empty when nothing is recognized.</returns>
    public IReadOnlyList<FixHint> Detect(string stdout, string stderr)
    {
        var text = string.Join('\n', new[] { stdout, stderr }.Where(s => !string.IsNullOrEmpty(s)));
        return _matcher.MatchRuntime(text);
    }
}
