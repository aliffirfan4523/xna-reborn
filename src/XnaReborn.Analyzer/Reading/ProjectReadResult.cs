using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reading;

/// <summary>The result of reading one project file.</summary>
/// <param name="Info">What the reader learned about the project.</param>
/// <param name="Properties">Project properties by name. The first value wins. Keys ignore case.</param>
/// <param name="Items">All items in document order, including one Import item per top-level Import. Items from imported .projitems files follow, with paths relative to the project folder.</param>
/// <param name="Diagnostics">Problems found while reading, such as ANA001 and ANA002.</param>
/// <param name="Packages">Entries of the project's packages.config, or null when none were read.</param>
/// <param name="MgcbFiles">Parsed Content.mgcb files of a MonoGame project, in the order they were found, or null when none were read.</param>
public sealed record ProjectReadResult(
    ProjectInfo Info,
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyList<ProjectItem> Items,
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<PackageEntry>? Packages = null,
    IReadOnlyList<ParsedMgcb>? MgcbFiles = null);
