using XnaReborn.App.Controls;
using XnaReborn.Migrator.Output;

namespace XnaReborn.App.ViewModels;

/// <summary>One row of the changed files list on the Diff page.</summary>
/// <param name="Change">The planned or written change.</param>
/// <param name="MarkerCount">Number of <c>// XNAREBORN:</c> marker lines in the new text.</param>
public sealed record DiffFileItem(FileChange Change, int MarkerCount)
{
    /// <summary>Relative path inside the output folder.</summary>
    public string Path => Change.RelativePath;

    /// <summary>The kind of change.</summary>
    public ChangeKind Kind => Change.Kind;

    /// <summary>Plain-text name of the kind. Shown next to the path, so color or glyph is never the only cue.</summary>
    public string KindLabel => DiffKindInfo.Label(Kind);

    /// <summary>One-character symbol for the kind.</summary>
    public string KindGlyph => DiffKindInfo.Glyph(Kind);

    /// <summary>The chip tint for the kind. The label and glyph carry the meaning too.</summary>
    public StatusKind Status => Kind switch
    {
        ChangeKind.Created => StatusKind.Success,
        ChangeKind.Modified => StatusKind.Accent,
        ChangeKind.ManualWork => StatusKind.Warning,
        _ => StatusKind.Neutral,
    };

    /// <summary>True when the new text has marker lines.</summary>
    public bool HasMarkers => MarkerCount > 0;

    /// <summary>Text for the marker badge, for example "2 markers", or an empty string.</summary>
    public string MarkerText => MarkerCount switch
    {
        0 => string.Empty,
        1 => "1 marker",
        _ => $"{MarkerCount} markers",
    };

    /// <summary>The text a screen reader reads for the row.</summary>
    public string AccessibleName => HasMarkers ? $"{KindLabel}: {Path}, {MarkerText}" : $"{KindLabel}: {Path}";
}

/// <summary>The number of files of one kind, for the summary above the list.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Count">How many files.</param>
public sealed record DiffKindCount(ChangeKind Kind, int Count)
{
    /// <summary>True when this kind is the active filter.</summary>
    public bool IsActive { get; init; }

    /// <summary>Plain-text name of the kind.</summary>
    public string Label => DiffKindInfo.Label(Kind);

    /// <summary>One-character symbol for the kind.</summary>
    public string Glyph => DiffKindInfo.Glyph(Kind);

    /// <summary>For example "Modified: 3".</summary>
    public string Text => $"{Label}: {Count}";

    /// <summary>What a screen reader says for the filter button.</summary>
    public string AccessibleName => $"Show only {Label}, {Count} {(Count == 1 ? "file" : "files")}, {(IsActive ? "on" : "off")}";
}

/// <summary>One <c>// XNAREBORN:</c> marker line in the new text of a file.</summary>
/// <param name="Path">Relative path of the file.</param>
/// <param name="NewLine">1-based line number in the new text.</param>
/// <param name="Text">The trimmed marker line.</param>
public sealed record DiffMarker(string Path, int NewLine, string Text);

/// <summary>Names and symbols of the change kinds.</summary>
internal static class DiffKindInfo
{
    // shortcut: ChangeKind has no separate "generated" kind, so generated files (project file, shims) show as New. Split them when the manifest tells them apart.
    public static string Label(ChangeKind kind) => kind switch
    {
        ChangeKind.Created => "New",
        ChangeKind.Modified => "Modified",
        ChangeKind.Copied => "Copied",
        ChangeKind.Skipped => "Skipped",
        ChangeKind.ManualWork => "Manual work",
        _ => kind.ToString(),
    };

    public static string Glyph(ChangeKind kind) => kind switch
    {
        ChangeKind.Created => "+",
        ChangeKind.Modified => "~",
        ChangeKind.Copied => "=",
        ChangeKind.Skipped => "-",
        ChangeKind.ManualWork => "!",
        _ => "?",
    };

    /// <summary>Sort order of the list: changed files first, then files that only moved.</summary>
    public static int Order(ChangeKind kind) => kind switch
    {
        ChangeKind.Modified => 0,
        ChangeKind.Created => 1,
        ChangeKind.ManualWork => 2,
        ChangeKind.Copied => 3,
        ChangeKind.Skipped => 4,
        _ => 5,
    };
}
