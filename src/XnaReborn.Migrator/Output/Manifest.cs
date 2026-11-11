using System.Text.Json.Serialization;

namespace XnaReborn.Migrator.Output;

/// <summary>The record of a migration: what changed, what was skipped, and what needs a person.</summary>
/// <param name="Version">Manifest format version. Currently 1.</param>
/// <param name="Migrator">Version of the migrator that wrote it.</param>
/// <param name="CatalogName">Name of the rule catalog the analysis used.</param>
/// <param name="Completed">False when the run stopped before the output was moved into place.</param>
/// <param name="Changes">One entry per output file, sorted by path.</param>
/// <param name="Build">Result of the optional post-migration build, or null when not attempted.</param>
/// <param name="Content">Totals for the converted content, or null when no content was converted.</param>
/// <param name="Shims">The shim library and every use of it, or null when shims were off. Left out of the JSON when null.</param>
/// <param name="Modern">The opt-in window extras and where they were hooked in, or null when all were off. Left out of the JSON when null.</param>
/// <param name="SourceFrameworks">The frameworks of the migrated projects (Xna, SharpDxToolkit), sorted, or null when the source had no SharpDX Toolkit project. Left out of the JSON when null, so XNA manifests do not change.</param>
/// <param name="RulesFired">Every rule id that had a finding in the analysis, sorted, or null when <paramref name="SourceFrameworks"/> is null. Left out of the JSON when null.</param>
public sealed record Manifest(
    int Version,
    string Migrator,
    string CatalogName,
    bool Completed,
    IReadOnlyList<ManifestChange> Changes,
    ManifestBuild? Build,
    ManifestContent? Content = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ManifestShims? Shims = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ManifestModern? Modern = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? SourceFrameworks = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? RulesFired = null);

/// <summary>Shims added to the migrated copy: offline stand-ins for dead XNA APIs, not real XNA services.</summary>
/// <param name="Project">Path of the shim project file, relative to the output, with '/' separators.</param>
/// <param name="Files">Paths of the generated shim files, sorted.</param>
/// <param name="ReferencedBy">Game projects that reference the shim project, sorted.</param>
/// <param name="Uses">Every shim use in the game source, sorted by file and namespace.</param>
public sealed record ManifestShims(
    string Project,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> ReferencedBy,
    IReadOnlyList<ManifestShimUse> Uses);

/// <summary>Opt-in window extras added to the migrated copy.</summary>
/// <param name="Features">The extras that are on: AllowWindowResize, FullscreenToggle, HighDpi.</param>
/// <param name="Files">Paths of the generated helper files, sorted, with '/' separators.</param>
/// <param name="HookedFiles">Game source files that got the one-line hook, sorted. Empty when no entry point could take it.</param>
/// <param name="Limits">Plain statements of what the extras do not do.</param>
public sealed record ManifestModern(
    IReadOnlyList<string> Features,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> HookedFiles,
    IReadOnlyList<string> Limits);

/// <summary>One source file that uses a shimmed namespace.</summary>
/// <param name="File">Relative path of the game source file, with '/' separators.</param>
/// <param name="Namespace">The XNA namespace whose shim the file uses.</param>
/// <param name="Types">Shimmed type names the file mentions, sorted.</param>
public sealed record ManifestShimUse(string File, string Namespace, IReadOnlyList<string> Types);

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
