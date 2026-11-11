using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using XnaReborn.App.Controls;
using XnaReborn.Migrator.Source;

namespace XnaReborn.App.Tests.Diff;

public sealed class DiffViewerTests
{
    private static readonly FileDiff _sample = DiffBuilder.BuildSideBySide(
        "Game1.cs",
        "keep\nold\nremoved\nlast\n",
        "keep\nnew\n// XNAREBORN: XNA022: look here\nlast\nadded\n");

    [AvaloniaFact]
    public void The_unified_rows_list_removed_before_added_and_drop_padding()
    {
        var rows = DiffViewer.BuildUnified(_sample);

        Assert.DoesNotContain(rows, r => r.Kind == DiffLineKind.Imaginary);
        var texts = rows.Select(r => r.Text).ToList();
        Assert.True(texts.IndexOf("old") < texts.IndexOf("new"));
        Assert.Equal(DiffLineKind.Removed, rows[texts.IndexOf("old")].Kind);
        Assert.Equal(DiffLineKind.Added, rows[texts.IndexOf("new")].Kind);
        Assert.Contains("removed", texts);
        Assert.Contains("added", texts);
    }
    [AvaloniaFact]
    public void Both_panes_show_the_text_of_their_side_and_are_read_only()
    {
        var viewer = Show(_sample);

        Assert.Equal(string.Join('\n', _sample.OldLines.Select(l => l.Text)), viewer.OldPane.Document.Text);
        Assert.Equal(string.Join('\n', _sample.NewLines.Select(l => l.Text)), viewer.NewPane.Document.Text);
        Assert.Contains("// XNAREBORN: XNA022: look here", viewer.NewPane.Document.Text, StringComparison.Ordinal);
        Assert.True(viewer.OldPane.IsReadOnly);
        Assert.True(viewer.NewPane.IsReadOnly);
        Assert.Equal(viewer.OldPane.Document.LineCount, viewer.NewPane.Document.LineCount);
    }

    [AvaloniaFact]
    public void Each_pane_has_a_gutter_with_the_diff_numbers_and_no_default_line_numbers()
    {
        var viewer = Show(_sample);

        Assert.False(viewer.OldPane.ShowLineNumbers);
        Assert.False(viewer.NewPane.ShowLineNumbers);
        Assert.Single(viewer.OldPane.TextArea.LeftMargins.OfType<DiffViewerGutter>());
        var gutter = Assert.Single(viewer.NewPane.TextArea.LeftMargins.OfType<DiffViewerGutter>());
        Assert.Same(_sample.NewLines, gutter.Lines);
        Assert.Single(viewer.NewPane.TextArea.TextView.BackgroundRenderers.OfType<DiffViewerBackground>());
    }

    [AvaloniaFact]
    public void The_viewer_draws_without_error_when_shown()
    {
        var viewer = Show(_sample);

        Assert.True(viewer.OldPane.TextArea.TextView.IsMeasureValid);
        Assert.True(viewer.NewPane.TextArea.TextView.IsMeasureValid);
    }

    [Fact]
    public void Changed_rows_get_a_background_and_a_glyph_and_unchanged_rows_get_neither()
    {
        var kinds = new[] { DiffLineKind.Added, DiffLineKind.Removed, DiffLineKind.Modified };
        foreach (var kind in kinds)
        {
            var line = new DiffLine(1, "x", kind);
            Assert.NotNull(DiffViewerBackground.BrushFor(line, marksMarkers: false));
            Assert.NotEqual(" ", DiffViewer.GlyphFor(kind));
            Assert.Contains(DiffViewer.GlyphFor(kind), DiffViewerGutter.RowText(line, false, 2), StringComparison.Ordinal);
        }

        Assert.Equal("+", DiffViewer.GlyphFor(DiffLineKind.Added));
        Assert.Equal("-", DiffViewer.GlyphFor(DiffLineKind.Removed));
        Assert.Equal("~", DiffViewer.GlyphFor(DiffLineKind.Modified));

        var same = new DiffLine(1, "x", DiffLineKind.Unchanged);
        Assert.Null(DiffViewerBackground.BrushFor(same, marksMarkers: true));
        Assert.Equal(" 1   ", DiffViewerGutter.RowText(same, true, 2));
    }

    [Fact]
    public void Added_removed_and_modified_backgrounds_are_three_different_colors()
    {
        var brushes = new[] { DiffLineKind.Added, DiffLineKind.Removed, DiffLineKind.Modified, DiffLineKind.Imaginary }
            .Select(k => DiffViewerBackground.BrushFor(new DiffLine(null, string.Empty, k), false))
            .Select(b => ((Avalonia.Media.ISolidColorBrush)b!).Color)
            .ToList();

        Assert.Equal(4, brushes.Distinct().Count());
    }

    [Fact]
    public void A_padding_row_has_no_line_number_and_no_glyph()
    {
        var padding = new DiffLine(null, string.Empty, DiffLineKind.Imaginary);

        Assert.Equal("     ", DiffViewerGutter.RowText(padding, true, 2));
        Assert.Equal("   ", DiffViewerGutter.RowText(padding, false, 0));
    }

    [Fact]
    public void A_marker_row_on_the_new_side_gets_its_own_color_and_a_bang_glyph()
    {
        var marker = new DiffLine(3, "    // XNAREBORN: XNA022: look here", DiffLineKind.Added);
        var plainAdded = new DiffLine(3, "    int x;", DiffLineKind.Added);

        Assert.NotEqual(DiffViewerBackground.BrushFor(plainAdded, true), DiffViewerBackground.BrushFor(marker, true));
        Assert.Equal(DiffViewerBackground.BrushFor(plainAdded, false), DiffViewerBackground.BrushFor(marker, false));
        Assert.Equal(" 3 +!", DiffViewerGutter.RowText(marker, true, 2));
        Assert.Equal(" 3 + ", DiffViewerGutter.RowText(marker, false, 2));
        Assert.Equal(" 3 + ", DiffViewerGutter.RowText(plainAdded, true, 2));
    }

    [AvaloniaFact]
    public void Reveal_outlines_the_row_of_that_new_line_and_clears_when_unknown_or_null()
    {
        var viewer = Show(_sample);
        var markerRow = _sample.NewLines.ToList().FindIndex(l => l.Text.Contains("XNAREBORN", StringComparison.Ordinal));
        Assert.True(markerRow >= 0);

        viewer.Reveal = new DiffViewerReveal(_sample.NewLines[markerRow].Number!.Value);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(markerRow, viewer.ActiveRow);

        viewer.Reveal = new DiffViewerReveal(999);
        Assert.Equal(-1, viewer.ActiveRow);

        viewer.Reveal = new DiffViewerReveal(_sample.NewLines[markerRow].Number!.Value);
        Assert.Equal(markerRow, viewer.ActiveRow);
        viewer.Reveal = null;
        Assert.Equal(-1, viewer.ActiveRow);
    }

    [AvaloniaFact]
    public void A_null_diff_shows_empty_panes()
    {
        var viewer = Show(_sample);

        viewer.Diff = null;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, viewer.OldPane.Document.Text);
        Assert.Equal(string.Empty, viewer.NewPane.Document.Text);
    }

    [AvaloniaFact]
    public void The_background_layer_is_the_one_that_is_redrawn()
    {
        var viewer = Show(_sample);

        var renderer = Assert.Single(viewer.NewPane.TextArea.TextView.BackgroundRenderers.OfType<DiffViewerBackground>());

        Assert.Equal(KnownLayer.Background, renderer.Layer);
        Assert.Same(_sample.NewLines, renderer.Lines);
    }

    private static DiffViewer Show(FileDiff diff)
    {
        var viewer = new DiffViewer { Diff = diff };
        var window = new Window { Width = 900, Height = 400, Content = viewer };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return viewer;
    }
}
