using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Content;

/// <summary>The outcome for one content item.</summary>
/// <param name="Item">The item as read from the content project.</param>
/// <param name="AssetName">The name the game passes to <c>Content.Load</c>, with '/' separators.</param>
/// <param name="MonoGameImporter">Importer name for the MGCB file, or empty when none applies.</param>
/// <param name="MonoGameProcessor">Processor name for the MGCB file, or empty when none applies.</param>
/// <param name="Status">What happened to the item.</param>
/// <param name="Note">A sentence for the report, or null.</param>
public sealed record ConvertedAsset(
    ContentItem Item,
    string AssetName,
    string MonoGameImporter,
    string MonoGameProcessor,
    AssetStatus Status,
    string? Note);
