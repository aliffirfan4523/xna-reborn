namespace XnaReborn.Migrator.Source;

/// <summary>One line of one side of a side-by-side diff.</summary>
/// <param name="Number">1-based line number in its text, or null for padding.</param>
/// <param name="Text">Line text without the line ending.</param>
/// <param name="Kind">How the line relates to the other side.</param>
public sealed record DiffLine(int? Number, string Text, DiffLineKind Kind);
