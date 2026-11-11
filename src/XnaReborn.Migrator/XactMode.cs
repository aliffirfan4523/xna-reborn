namespace XnaReborn.Migrator;

/// <summary>What the migrator does with XACT audio projects (<c>.xap</c> files) and compiled banks.</summary>
public enum XactMode
{
    /// <summary>Convert a <c>.xap</c> that can be read; copy compiled banks that have no <c>.xap</c> and warn about the MonoGame limits. The default.</summary>
    Auto,

    /// <summary>Convert every <c>.xap</c>. One that cannot be read is reported as an error instead of being copied quietly.</summary>
    ConvertOnly,

    /// <summary>Copy every XACT file unchanged. No assets, catalog, shim or source aliases are generated.</summary>
    CopyOnly,
}
