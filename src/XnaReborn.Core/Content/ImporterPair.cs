namespace XnaReborn.Core.Content;

/// <summary>A pair of content pipeline importer and processor names as they appear in a content project.</summary>
/// <param name="Importer">The importer class name, for example TextureImporter.</param>
/// <param name="Processor">The processor class name, for example TextureProcessor.</param>
/// <param name="Note">A short remark about the pair, or null.</param>
public sealed record ImporterPair(string Importer, string Processor, string? Note);
