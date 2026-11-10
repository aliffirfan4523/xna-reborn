namespace XnaReborn.Migrator.Source;

/// <summary>
/// One source-level migration rule. It takes the text of one .cs file and returns the new text.
/// Rewriters work on text instead of a syntax tree so that code inside inactive <c>#if</c> blocks stays visible,
/// and so that unchanged parts of the file stay byte for byte identical. Rewriters that need a syntax tree parse the text themselves.
/// </summary>
public interface ISourceRewriter
{
    /// <summary>Gets the rule ID this rewriter handles, such as an analyzer ID or <c>ENTRYPOINT</c>.</summary>
    string RuleId { get; }

    /// <summary>Rewrites one file.</summary>
    /// <param name="context">The file and its original compilation symbols.</param>
    /// <returns>The new text, which equals <see cref="SourceRewriteContext.Text"/> when nothing changed, and notes for a person.</returns>
    SourceRewriteResult Rewrite(SourceRewriteContext context);
}
