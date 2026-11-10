namespace XnaReborn.Migrator.Source;

/// <summary>The output of one <see cref="ISourceRewriter"/> call.</summary>
/// <param name="Text">The new text. Equal to the input text when nothing changed.</param>
/// <param name="Notes">Notes for a person, possibly empty.</param>
public sealed record SourceRewriteResult(string Text, IReadOnlyList<RewriteNote> Notes)
{
    /// <summary>Creates a result that leaves the text of <paramref name="context"/> alone.</summary>
    /// <param name="context">The context that was rewritten.</param>
    /// <returns>A result with the same text and no notes.</returns>
    public static SourceRewriteResult Unchanged(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SourceRewriteResult(context.Text, []);
    }

    /// <summary>Tells whether the text differs from the input.</summary>
    /// <param name="context">The context that was rewritten.</param>
    /// <returns>True when <see cref="Text"/> is not equal to the input text.</returns>
    public bool Changed(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return !string.Equals(Text, context.Text, StringComparison.Ordinal);
    }
}
