namespace XnaReborn.Migrator.Source;

/// <summary>
/// Adapts <see cref="EntryPointRewriter"/> to <see cref="ISourceRewriter"/>.
/// The rule ID is a pseudo ID, because the entry point fix is not driven by an analyzer finding.
/// </summary>
/// <param name="inner">The rewriter to wrap. A new one is used when null.</param>
public sealed class EntryPointSourceRewriter(EntryPointRewriter? inner = null) : ISourceRewriter
{
    /// <summary>The pseudo rule ID of the entry point fix.</summary>
    public const string Id = "ENTRYPOINT";

    private readonly EntryPointRewriter _inner = inner ?? new EntryPointRewriter();

    /// <inheritdoc />
    public string RuleId => Id;

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var result = _inner.Rewrite(context.Text, context.OriginalSymbols);
        return result.Status switch
        {
            EntryPointStatus.Unwrapped => new SourceRewriteResult(
                result.Text, [new RewriteNote(Id, result.Note ?? string.Empty, NeedsManualWork: false)]),
            EntryPointStatus.Skipped => new SourceRewriteResult(
                context.Text, [new RewriteNote(Id, result.Note ?? string.Empty, NeedsManualWork: true)]),
            _ => SourceRewriteResult.Unchanged(context),
        };
    }
}
