using XnaReborn.Runner.Hints;

namespace XnaReborn.Runner.Diagnostics;

/// <summary>Removes repeated diagnostics and groups the rest by the hint that explains them.</summary>
public static class DiagnosticGrouper
{
    /// <summary>
    /// Builds the groups shown to the user. Exact repeats (same severity, code, message, file, line, column, project and
    /// asset) are kept once, because MSBuild prints every error again in its summary. Diagnostics that share a hint id form
    /// one group. Diagnostics without a hint form one "no known fix" group per diagnostic code.
    /// </summary>
    /// <param name="diagnostics">All diagnostics, in output order.</param>
    /// <param name="match">Finds the hint for a diagnostic, or returns null when none is known. Called once per distinct diagnostic.</param>
    /// <returns>
    /// Groups with an error first, then groups with only warnings; inside each part the order of first appearance is kept.
    /// Inside a group, errors come before warnings and the original order is kept.
    /// </returns>
    public static IReadOnlyList<DiagnosticGroup> Group(IEnumerable<BuildDiagnostic> diagnostics, Func<BuildDiagnostic, FixHint?> match)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(match);

        var seen = new HashSet<BuildDiagnostic>();
        var groups = new List<Bucket>();
        var byKey = new Dictionary<string, Bucket>(StringComparer.Ordinal);

        foreach (var diagnostic in diagnostics)
        {
            if (!seen.Add(diagnostic))
            {
                continue;
            }

            var hint = match(diagnostic);
            var key = hint is null ? "code:" + diagnostic.Code : "hint:" + hint.Id;
            if (!byKey.TryGetValue(key, out var bucket))
            {
                bucket = new Bucket(hint);
                byKey.Add(key, bucket);
                groups.Add(bucket);
            }

            bucket.Items.Add(diagnostic);
        }

        return groups
            .Select((bucket, index) => (bucket, index))
            .OrderBy(g => g.bucket.Items.Any(d => d.Severity == BuildDiagnosticSeverity.Error) ? 0 : 1)
            .ThenBy(g => g.index)
            .Select(g => new DiagnosticGroup(
                g.bucket.Hint,
                g.bucket.Items
                    .Select((item, index) => (item, index))
                    .OrderBy(i => SeverityRank(i.item.Severity))
                    .ThenBy(i => i.index)
                    .Select(i => i.item)
                    .ToList()))
            .ToList();
    }

    private static int SeverityRank(BuildDiagnosticSeverity severity) => severity switch
    {
        BuildDiagnosticSeverity.Error => 0,
        BuildDiagnosticSeverity.Warning => 1,
        _ => 2,
    };

    private sealed class Bucket(FixHint? hint)
    {
        public FixHint? Hint { get; } = hint;

        public List<BuildDiagnostic> Items { get; } = [];
    }
}
