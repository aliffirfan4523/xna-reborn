namespace XnaReborn.Migrator.Output;

/// <summary>The record of a migration: what changed, what was skipped, and what needs a person.</summary>
/// <param name="Version">Manifest format version. Currently 1.</param>
/// <param name="Migrator">Version of the migrator that wrote it.</param>
/// <param name="CatalogName">Name of the rule catalog the analysis used.</param>
/// <param name="Completed">False when the run stopped before the output was moved into place.</param>
/// <param name="Changes">One entry per output file, sorted by path.</param>
/// <param name="Build">Result of the optional post-migration build, or null when not attempted.</param>
public sealed record Manifest(
    int Version,
    string Migrator,
    string CatalogName,
    bool Completed,
    IReadOnlyList<ManifestChange> Changes,
    ManifestBuild? Build);

/// <summary>One file entry in the manifest.</summary>
/// <param name="Path">Relative path in the output, with '/' separators.</param>
/// <param name="Kind">What happened to the file.</param>
/// <param name="Rules">Analyzer rule ids that caused the change, sorted.</param>
/// <param name="Reason">Why, mainly for manual work and skipped files.</param>
public sealed record ManifestChange(
    string Path,
    ChangeKind Kind,
    IReadOnlyList<string> Rules,
    string? Reason);

/// <summary>Result of building the migrated copy.</summary>
/// <param name="Attempted">Whether a build ran.</param>
/// <param name="Succeeded">Whether it succeeded.</param>
/// <param name="Errors">Build error lines, sorted.</param>
public sealed record ManifestBuild(
    bool Attempted,
    bool Succeeded,
    IReadOnlyList<string> Errors);
