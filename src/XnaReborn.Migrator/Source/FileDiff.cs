namespace XnaReborn.Migrator.Source;

/// <summary>A side-by-side diff of one file. Both line lists have the same length.</summary>
/// <param name="Path">Relative path of the file.</param>
/// <param name="OldLines">Lines of the original text, padded to align with <paramref name="NewLines"/>.</param>
/// <param name="NewLines">Lines of the updated text, padded to align with <paramref name="OldLines"/>.</param>
/// <param name="Added">Number of lines that exist only in the new text.</param>
/// <param name="Removed">Number of lines that exist only in the old text.</param>
/// <param name="Modified">Number of lines that changed in place.</param>
public sealed record FileDiff(
    string Path,
    IReadOnlyList<DiffLine> OldLines,
    IReadOnlyList<DiffLine> NewLines,
    int Added,
    int Removed,
    int Modified)
{
    /// <summary>True when any new line carries a manual-work marker the UI should highlight.</summary>
    public bool ContainsManualMarker =>
        NewLines.Any(l => l.Kind != DiffLineKind.Imaginary && MarkerPrefixes.ContainsMarker(l.Text));
}
