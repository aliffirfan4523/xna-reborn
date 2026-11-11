using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Source;

/// <summary>Runs the enabled source rewriters over one file, one after the other.</summary>
/// <param name="registry">The available rewriters.</param>
public sealed class SourceRewriteEngine(RewriterRegistry registry)
{
    // Rewriters that must run before the rest, in this order. Everything else runs in rule ID order.
    // XNA053 folds platform #if groups first, so the entry point rewriter sees the result.
    private static readonly string[] _runFirst = [PlatformDirectiveRewriter.Id];

    private static int Priority(string ruleId)
    {
        var index = Array.IndexOf(_runFirst, ruleId);
        return index < 0 ? _runFirst.Length : index;
    }

    private readonly RewriterRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    /// <summary>Rewrites one file.</summary>
    /// <param name="filePath">Path relative to the project root, with '/' separators.</param>
    /// <param name="text">Text of the file.</param>
    /// <param name="originalSymbols">The original project's DefineConstants.</param>
    /// <param name="enabledRuleIds">Rule IDs to run. Registered rewriters outside this set are skipped.</param>
    /// <param name="platform">The platform of the migrated project, for rewriters that fold platform conditionals.</param>
    /// <param name="language">The language of the file. Only rewriters for this language run.</param>
    /// <param name="implicitImports">Namespaces the project imports for every file (Visual Basic), or null.</param>
    /// <returns>
    /// The final text and what happened. A rewriter that throws is recorded as a failure and the text from before it is kept.
    /// <see cref="OperationCanceledException"/> is not caught.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public FileRewriteResult Rewrite(
        string filePath,
        string text,
        IReadOnlyCollection<string> originalSymbols,
        IReadOnlySet<string> enabledRuleIds,
        XnaPlatform platform = XnaPlatform.Unknown,
        SourceLanguage language = SourceLanguage.CSharp,
        IReadOnlyList<string>? implicitImports = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(originalSymbols);
        ArgumentNullException.ThrowIfNull(enabledRuleIds);

        var current = text;
        var applied = new List<string>();
        var notes = new List<RewriteNote>();
        var failures = new List<RewriteFailure>();

        foreach (var rewriter in _registry.All.OrderBy(r => Priority(r.RuleId)))
        {
            if (rewriter.Language != language || !enabledRuleIds.Contains(rewriter.RuleId))
            {
                continue;
            }

            var context = new SourceRewriteContext(filePath, current, originalSymbols, platform, language, implicitImports);
            SourceRewriteResult result;
            try
            {
                result = rewriter.Rewrite(context);
                if (result is null || result.Text is null)
                {
                    throw new InvalidOperationException("The rewriter returned no text.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new RewriteFailure(rewriter.RuleId, ex.Message));
                continue;
            }

            notes.AddRange(result.Notes ?? []);
            if (result.Changed(context))
            {
                current = result.Text;
                applied.Add(rewriter.RuleId);
            }
        }

        var changed = !string.Equals(current, text, StringComparison.Ordinal);
        return new FileRewriteResult(changed ? current : text, changed, applied, notes, failures);
    }
}
