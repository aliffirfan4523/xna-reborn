namespace XnaReborn.Migrator.Content;

/// <summary>What the converter did with one content item.</summary>
public enum AssetStatus
{
    /// <summary>MonoGame can build the item with the importer and processor named in the result.</summary>
    Converted,

    /// <summary>The item needs work by hand, such as XACT audio or a custom pipeline assembly.</summary>
    ManualWork,

    /// <summary>The source file does not exist.</summary>
    Missing,

    /// <summary>Another item has the same asset name.</summary>
    Conflict,

    /// <summary>A prebuilt .xnb file with no source. It is copied but cannot be rebuilt.</summary>
    Prebuilt,
}
