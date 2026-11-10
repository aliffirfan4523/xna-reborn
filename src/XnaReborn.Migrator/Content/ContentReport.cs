namespace XnaReborn.Migrator.Content;

/// <summary>Summary of a content conversion for the report.</summary>
/// <param name="Assets">All assets, sorted by asset name.</param>
/// <param name="CountsByStatus">Count per status. Every status is present, in enum order.</param>
/// <param name="CountsByType">Count per asset type. Every type is present, in the order Texture, Font, SoundEffect, Song, Model, Effect, Xact, Xml, Prebuilt, Other.</param>
/// <param name="Notes">Free-form notes, sorted and distinct.</param>
public sealed record ContentReport(
    IReadOnlyList<ConvertedAsset> Assets,
    IReadOnlyDictionary<AssetStatus, int> CountsByStatus,
    IReadOnlyDictionary<string, int> CountsByType,
    IReadOnlyList<string> Notes)
{
    private static readonly string[] _types =
        ["Texture", "Font", "SoundEffect", "Song", "Model", "Effect", "Xact", "Xml", "Prebuilt", "Other"];

    private static readonly string[] _xactExtensions = [".xap", ".xgs", ".xwb", ".xsb"];

    /// <summary>Builds a report.</summary>
    /// <param name="assets">The assets.</param>
    /// <param name="notes">Notes to include.</param>
    /// <returns>The report.</returns>
    public static ContentReport Create(IEnumerable<ConvertedAsset> assets, IEnumerable<string> notes)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(notes);

        var sorted = assets
            .OrderBy(a => a.AssetName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.AssetName, StringComparer.Ordinal)
            .ThenBy(a => a.Item.SourcePath, StringComparer.Ordinal)
            .ToList();

        var byStatus = new Dictionary<AssetStatus, int>();
        foreach (var status in Enum.GetValues<AssetStatus>())
        {
            byStatus[status] = sorted.Count(a => a.Status == status);
        }

        var typeCounts = _types.ToDictionary(t => t, _ => 0);
        foreach (var asset in sorted)
        {
            typeCounts[TypeOf(asset)]++;
        }

        var sortedNotes = notes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return new ContentReport(sorted, byStatus, typeCounts, sortedNotes);
    }

    private static string TypeOf(ConvertedAsset asset)
    {
        var extension = Path.GetExtension(asset.Item.SourcePath);
        if (asset.Status == AssetStatus.Prebuilt)
        {
            return "Prebuilt";
        }

        if (asset.MonoGameProcessor == "XactProcessor" || _xactExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return "Xact";
        }

        return asset.MonoGameProcessor switch
        {
            "TextureProcessor" => "Texture",
            "FontDescriptionProcessor" => "Font",
            "SoundEffectProcessor" => "SoundEffect",
            "SongProcessor" => "Song",
            "ModelProcessor" => "Model",
            "EffectProcessor" => "Effect",
            "PassThroughProcessor" when extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) => "Xml",
            _ => "Other",
        };
    }
}
