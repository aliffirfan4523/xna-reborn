namespace XnaReborn.Migrator.Source;

/// <summary>How a line of a side-by-side diff relates to the other side.</summary>
public enum DiffLineKind
{
    /// <summary>The line is the same on both sides.</summary>
    Unchanged,

    /// <summary>The line exists only on the new side.</summary>
    Added,

    /// <summary>The line exists only on the old side.</summary>
    Removed,

    /// <summary>The line differs between the sides.</summary>
    Modified,

    /// <summary>Padding opposite a one-sided change. It has no number.</summary>
    Imaginary,
}
