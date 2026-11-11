namespace XnaReborn.Flash.Analyzer.As3;

/// <summary>A range of source text, with the 1-based position of its first character.</summary>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
/// <param name="Line">1-based line of the first character.</param>
/// <param name="Column">1-based column of the first character.</param>
public readonly record struct SourceSpan(int Start, int Length, int Line, int Column)
{
    /// <summary>Zero-based offset just past the last character.</summary>
    public int End => Start + Length;

    /// <summary>The span that starts at the start of <paramref name="first"/> and ends at the end of <paramref name="last"/>.</summary>
    /// <param name="first">The earlier span.</param>
    /// <param name="last">The later span.</param>
    public static SourceSpan Between(SourceSpan first, SourceSpan last) =>
        new(first.Start, Math.Max(0, last.End - first.Start), first.Line, first.Column);
}
