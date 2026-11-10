namespace XnaReborn.Migrator.Output;

/// <summary>The record of a migration: what changed, what was skipped, and what needs a person.</summary>
/// <param name="Version">Manifest format version. Currently 1.</param>
/// <param name="Migrator">Version of the migrator that wrote it.</param>
/// <param name="CatalogName">Name of the rule catalog the analysis used.</param>
/// <param name="Completed">False when the run stopped before the output was moved into place.</param>
/// <param name="Changes">One entry per output file, sorted by path.</param>
/// <param name="Build">Result of the optional post-migration build, or null when not attempted.</param>
/// <param name="Content">Totals for the converted content, or null when no content was converted.</param>
public sealed record Manifest(
    int Version,
    string Migrator,
    string CatalogName,
    bool Completed,
    IReadOnlyList<ManifestChange> Changes,
    ManifestBuild? Build,
    ManifestContent? Content = null);

/// <summary>Content conversion totals, summed over all projects.</summary>
/// <param name="CountsByStatus">Assets per status. Every status key is present, in enum order: Converted, ManualWork, Missing, Conflict, Prebuilt.</param>
/// <param name="CountsByType">Assets per type. Every type key is present, in the order Texture, Font, SoundEffect, Song, Model, Effect, Xact, Xml, Prebuilt, Other.</param>
public sealed record ManifestContent(
    IReadOnlyDictionary<string, int> CountsByStatus,
    IReadOnlyDictionary<string, int> CountsByType);

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
