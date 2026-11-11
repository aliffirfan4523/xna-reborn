namespace XnaReborn.Migrator.Output;

/// <summary>Builds the <see cref="Manifest"/> record from the planned file changes.</summary>
public static class ManifestBuilder
{
    private const int _formatVersion = 1;

    /// <summary>Creates a manifest with one entry per change, sorted by path.</summary>
    /// <param name="catalogName">Name of the rule catalog the analysis used.</param>
    /// <param name="completed">False when the run stopped before the output was moved into place.</param>
    /// <param name="changes">Every planned file change.</param>
    /// <param name="ruleIdsByPath">Rule ids that changed each output file, keyed by relative path.</param>
    /// <param name="build">Result of the optional post-migration build, or null.</param>
    /// <param name="migratorVersion">Version of the migrator writing the manifest.</param>
    /// <param name="content">Content conversion totals, or null when no content was converted.</param>
    /// <param name="shims">The shim library and its uses, or null when shims were off.</param>
    /// <param name="modern">The opt-in window extras, or null when all were off.</param>
    /// <param name="sourceFrameworks">The source frameworks when the source had a SharpDX Toolkit project, otherwise null.</param>
    /// <param name="rulesFired">Rule ids with a finding, shown together with <paramref name="sourceFrameworks"/>, otherwise null.</param>
    /// <returns>The manifest.</returns>
    public static Manifest Build(
        string catalogName,
        bool completed,
        IReadOnlyList<FileChange> changes,
        IReadOnlyDictionary<string, IReadOnlyList<string>> ruleIdsByPath,
        ManifestBuild? build,
        string migratorVersion,
        ManifestContent? content = null,
        ManifestShims? shims = null,
        ManifestModern? modern = null,
        IReadOnlyList<string>? sourceFrameworks = null,
        IReadOnlyList<string>? rulesFired = null)
    {
        ArgumentNullException.ThrowIfNull(catalogName);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(ruleIdsByPath);
        ArgumentNullException.ThrowIfNull(migratorVersion);

        var entries = changes
            .Select(c => new ManifestChange(
                c.RelativePath,
                c.Kind,
                ruleIdsByPath.TryGetValue(c.RelativePath, out var ids)
                    ? ids.Distinct(StringComparer.Ordinal).OrderBy(i => i, StringComparer.Ordinal).ToList()
                    : [],
                c.Reason))
            .OrderBy(e => e.Path, StringComparer.Ordinal)
            .ToList();

        return new Manifest(_formatVersion, migratorVersion, catalogName, completed, entries, SortedBuild(build), content, shims, modern, sourceFrameworks, rulesFired);
    }

    private static ManifestBuild? SortedBuild(ManifestBuild? build) =>
        build is null ? null : build with { Errors = build.Errors.OrderBy(e => e, StringComparer.Ordinal).ToList() };
}
