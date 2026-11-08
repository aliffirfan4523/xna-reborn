using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reading;

/// <summary>The result of reading one content project.</summary>
/// <param name="Items">Content entries in document order.</param>
/// <param name="Properties">Project properties by name. The first value wins. Keys ignore case.</param>
/// <param name="References">Reference items, including custom pipeline assemblies (HintPath in the metadata).</param>
/// <param name="Diagnostics">Problems found while reading, such as ANA001.</param>
public sealed record ContentProjectReadResult(
    IReadOnlyList<ContentItem> Items,
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyList<ProjectItem> References,
    IReadOnlyList<Diagnostic> Diagnostics);
