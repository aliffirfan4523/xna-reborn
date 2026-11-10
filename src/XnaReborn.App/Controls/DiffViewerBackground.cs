using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Rendering;
using XnaReborn.Migrator.Source;

namespace XnaReborn.App.Controls;

/// <summary>Paints a background color behind each row of one <see cref="DiffViewer"/> pane, by how the row changed.</summary>
internal sealed class DiffViewerBackground : IBackgroundRenderer
{
    // Immutable brushes belong to no thread, so the static fields work on any UI thread (and in tests).
    private static readonly IBrush _added = new ImmutableSolidColorBrush(Color.FromArgb(0x50, 0x2E, 0xA0, 0x43));
    private static readonly IBrush _removed = new ImmutableSolidColorBrush(Color.FromArgb(0x50, 0xD1, 0x24, 0x2F));
    private static readonly IBrush _modified = new ImmutableSolidColorBrush(Color.FromArgb(0x50, 0xE0, 0xA0, 0x10));
    private static readonly IBrush _padding = new ImmutableSolidColorBrush(Color.FromArgb(0x28, 0x80, 0x80, 0x80));
    private static readonly IBrush _marker = new ImmutableSolidColorBrush(Color.FromArgb(0x90, 0xFF, 0x8C, 0x00));
    private static readonly IPen _active = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)), 2);

    /// <summary>The rows of this pane.</summary>
    public IReadOnlyList<DiffLine> Lines { get; set; } = [];

    /// <summary>True for the pane of the new text, where marker rows get the marker color.</summary>
    public bool MarksMarkers { get; init; }

    /// <summary>Zero-based row to outline as the current marker, or -1.</summary>
    public int ActiveRow { get; set; } = -1;

    /// <inheritdoc />
    public KnownLayer Layer => KnownLayer.Background;

    /// <summary>The brush for a row, or null for an unchanged row.</summary>
    public static IBrush? BrushFor(DiffLine line, bool marksMarkers) =>
        marksMarkers && DiffViewer.IsMarker(line)
            ? _marker
            : line.Kind switch
            {
                DiffLineKind.Added => _added,
                DiffLineKind.Removed => _removed,
                DiffLineKind.Modified => _modified,
                DiffLineKind.Imaginary => _padding,
                _ => null,
            };

    /// <inheritdoc />
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid)
        {
            return;
        }

        foreach (var visual in textView.VisualLines)
        {
            var row = visual.FirstDocumentLine.LineNumber - 1;
            if ((uint)row >= (uint)Lines.Count)
            {
                continue;
            }

            var rect = new Rect(0, visual.VisualTop - textView.VerticalOffset, textView.Bounds.Width, visual.Height);
            var brush = BrushFor(Lines[row], MarksMarkers);
            if (brush is not null)
            {
                drawingContext.DrawRectangle(brush, null, rect);
            }

            if (row == ActiveRow)
            {
                drawingContext.DrawRectangle(null, _active, rect);
            }
        }
    }
}
