namespace XnaReborn.Core.Content;

/// <summary>A processor name from XNA Game Studio 1.0 that a later version merged into another processor.</summary>
/// <param name="Importer">The importer name, for example TextureImporter.</param>
/// <param name="Processor">The legacy processor name, for example SpriteTextureProcessor.</param>
/// <param name="MapsTo">The current processor that replaces it.</param>
/// <param name="Note">A short remark for the content report, or null.</param>
public sealed record ImporterAlias(string Importer, string Processor, string MapsTo, string? Note);
