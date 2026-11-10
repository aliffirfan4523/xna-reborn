namespace XnaReborn.Migrator.Output;

/// <summary>One planned change to the output folder. Nothing is written until all changes are computed.</summary>
/// <param name="RelativePath">Path inside the output folder, with '/' separators.</param>
/// <param name="Kind">What happens to the file.</param>
/// <param name="OriginalText">Source text for text files that are rewritten, otherwise null.</param>
/// <param name="NewText">Output text for created or modified text files, otherwise null.</param>
/// <param name="Reason">Why the change is made, or why the file needs manual work.</param>
/// <param name="SourcePath">
/// Absolute path of the source file whose bytes are copied, for <see cref="ChangeKind.Copied"/> and
/// <see cref="ChangeKind.ManualWork"/> changes that have no <paramref name="NewText"/>. Null otherwise.
/// It always points inside the source project, which is only ever read.
/// </param>
public sealed record FileChange(
    string RelativePath,
    ChangeKind Kind,
    string? OriginalText,
    string? NewText,
    string? Reason,
    string? SourcePath = null);
