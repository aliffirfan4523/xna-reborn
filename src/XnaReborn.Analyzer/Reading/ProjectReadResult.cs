using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reading;

/// <summary>The result of reading one project file.</summary>
/// <param name="Info">What the reader learned about the project.</param>
/// <param name="Properties">Project properties by name. The first value wins. Keys ignore case.</param>
/// <param name="Items">All items in document order, including one Import item per top-level Import.</param>
/// <param name="Diagnostics">Problems found while reading, such as ANA001 and ANA002.</param>
public sealed record ProjectReadResult(
    ProjectInfo Info,
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyList<ProjectItem> Items,
    IReadOnlyList<Diagnostic> Diagnostics);
