using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using XnaReborn.Migrator.Source;

namespace XnaReborn.App.Controls;

/// <summary>
/// The gutter of one <see cref="DiffViewer"/> pane. It draws the line number of the original or new text (padding rows have
/// none), a +, - or ~ glyph for a changed row, and a ! for a manual-work marker row, so color is never the only cue.
/// </summary>
internal sealed class DiffViewerGutter : AbstractMargin
{
    private IReadOnlyList<DiffLine> _lines = [];
    private int _digits = 2;

    /// <summary>The rows of this pane. Setting it redraws and resizes the gutter.</summary>
    public IReadOnlyList<DiffLine> Lines
    {
        get => _lines;
        set
        {
            _lines = value;
            _digits = Math.Max(2, _lines.Max(l => l.Number)?.ToString(CultureInfo.InvariantCulture).Length ?? 2);
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>True for the pane of the new text, where marker rows get a ! glyph.</summary>
    public bool MarksMarkers { get; init; }

    /// <summary>The text drawn for a row: right-aligned number, a space, the change glyph and the marker glyph.</summary>
    public static string RowText(DiffLine line, bool marksMarkers, int digits)
    {
        var number = line.Number?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var glyph = DiffViewer.GlyphFor(line.Kind);
        var marker = marksMarkers && DiffViewer.IsMarker(line) ? "!" : " ";
        return number.PadLeft(digits) + " " + glyph + marker;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var text = Format(new string('9', _digits + 3));
        return new Size(text.Width + 6, 0);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var textView = TextView;
        if (textView is not { VisualLinesValid: true })
        {
            return;
        }

        using var opacity = context.PushOpacity(0.75);
        foreach (var visual in textView.VisualLines)
        {
            var row = visual.FirstDocumentLine.LineNumber - 1;
            if ((uint)row >= (uint)_lines.Count)
            {
                continue;
            }

            var text = Format(RowText(_lines[row], MarksMarkers, _digits));
            var y = visual.GetTextLineVisualYPosition(visual.TextLines[0], VisualYPosition.TextTop);
            context.DrawText(text, new Point(3, y - textView.VerticalOffset));
        }
    }

    private FormattedText Format(string text) => new(
        text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        new Typeface(GetValue(TextBlock.FontFamilyProperty)),
        GetValue(TextBlock.FontSizeProperty),
        GetValue(TextBlock.ForegroundProperty));
}
