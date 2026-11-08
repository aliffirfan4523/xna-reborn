namespace XnaReborn.Core.Models;

/// <summary>One entry in an XNA content project.</summary>
/// <param name="SourcePath">Path of the source asset.</param>
/// <param name="NameOverride">Asset name set by the project, or null when the default name is used.</param>
/// <param name="Importer">Content importer name, or null.</param>
/// <param name="Processor">Content processor name, or null.</param>
/// <param name="ProcessorParams">Processor parameters keyed by parameter name.</param>
public sealed record ContentItem(
    string SourcePath,
    string? NameOverride,
    string? Importer,
    string? Processor,
    IReadOnlyDictionary<string, string> ProcessorParams);
