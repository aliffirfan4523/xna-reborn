using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using XnaReborn.Migrator.Source;

namespace XnaReborn.App.Controls;

/// <summary>
/// Two read-only panes (original and migrated) for one <see cref="FileDiff"/>. Changed rows have a background color and a
/// +, - or ~ glyph in the gutter; rows with a <c>// XNAREBORN:</c> marker get a stronger color and a ! glyph.
/// The panes scroll together.
/// </summary>
public sealed partial class DiffViewer : UserControl
{
    /// <summary>Identifies <see cref="Diff"/>.</summary>
    public static readonly StyledProperty<FileDiff?> DiffProperty =
        AvaloniaProperty.Register<DiffViewer, FileDiff?>(nameof(Diff));

    /// <summary>Identifies <see cref="Reveal"/>.</summary>
    public static readonly StyledProperty<DiffViewerReveal?> RevealProperty =
        AvaloniaProperty.Register<DiffViewer, DiffViewerReveal?>(nameof(Reveal));

    /// <summary>Identifies <see cref="IsUnified"/>.</summary>
    public static readonly StyledProperty<bool> IsUnifiedProperty =
        AvaloniaProperty.Register<DiffViewer, bool>(nameof(IsUnified));

    private readonly DiffViewerBackground _unifiedBackground = new() { MarksMarkers = true };
    private readonly DiffViewerGutter _unifiedGutter = new() { MarksMarkers = true };
    private readonly DiffViewerBackground _oldBackground = new();
    private readonly DiffViewerBackground _newBackground = new() { MarksMarkers = true };
    private readonly DiffViewerGutter _oldGutter = new();
    private readonly DiffViewerGutter _newGutter = new() { MarksMarkers = true };
    private bool _syncing;

    /// <summary>Creates the viewer.</summary>
    public DiffViewer()
    {
        InitializeComponent();
        Attach(OldEditor, _oldBackground, _oldGutter);
        Attach(NewEditor, _newBackground, _newGutter);
        Attach(UnifiedEditor, _unifiedBackground, _unifiedGutter);
        OldEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => SyncScroll(OldEditor, NewEditor);
        NewEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => SyncScroll(NewEditor, OldEditor);

        // The panes are read-only but their text area still swallows Tab, which would trap a keyboard user inside.
        AddHandler(KeyDownEvent, OnPaneKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>The diff to show, or null to show empty panes.</summary>
    public FileDiff? Diff
    {
        get => GetValue(DiffProperty);
        set => SetValue(DiffProperty, value);
    }

    /// <summary>True to show one pane with removed and added rows in order, false for two panes side by side.</summary>
    public bool IsUnified
    {
        get => GetValue(IsUnifiedProperty);
        set => SetValue(IsUnifiedProperty, value);
    }

    /// <summary>Scrolls to a line of the new text and outlines its row. A new object scrolls again.</summary>
    public DiffViewerReveal? Reveal
    {
        get => GetValue(RevealProperty);
        set => SetValue(RevealProperty, value);
    }

    /// <summary>The pane with the original text.</summary>
    internal TextEditor OldPane => OldEditor;

    /// <summary>The pane with the migrated text.</summary>
    internal TextEditor NewPane => NewEditor;

    /// <summary>The single pane of unified mode.</summary>
    internal TextEditor UnifiedPane => UnifiedEditor;

    /// <summary>Zero-based row outlined as the current marker in the visible mode, or -1.</summary>
    internal int ActiveRow => IsUnified ? _unifiedBackground.ActiveRow : _newBackground.ActiveRow;

    /// <summary>
    /// The rows of unified mode: unchanged and added rows from the new text, removed rows from the old text.
    /// A modified row becomes a removed row followed by an added row. Padding rows are dropped.
    /// </summary>
    internal static List<DiffLine> BuildUnified(FileDiff diff)
    {
        var rows = new List<DiffLine>();
        for (var i = 0; i < diff.NewLines.Count; i++)
        {
            var old = diff.OldLines[i];
            var line = diff.NewLines[i];
            switch (line.Kind)
            {
                case DiffLineKind.Modified:
                    rows.Add(old with { Kind = DiffLineKind.Removed });
                    rows.Add(line with { Kind = DiffLineKind.Added });
                    break;
                case DiffLineKind.Imaginary:
                    rows.Add(old);
                    break;
                default:
                    rows.Add(line);
                    break;
            }
        }

        return rows;
    }

    /// <summary>The gutter glyph of a row kind: + added, - removed, ~ modified, a space otherwise.</summary>
    internal static string GlyphFor(DiffLineKind kind) => kind switch
    {
        DiffLineKind.Added => "+",
        DiffLineKind.Removed => "-",
        DiffLineKind.Modified => "~",
        _ => " ",
    };

    /// <summary>True when the line is a manual-work marker line.</summary>
    internal static bool IsMarker(DiffLine line) =>
        line.Kind is not (DiffLineKind.Imaginary or DiffLineKind.Removed) && MarkerPrefixes.ContainsMarker(line.Text);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DiffProperty)
        {
            Rebuild();
        }
        else if (change.Property == IsUnifiedProperty)
        {
            ApplyMode();
            ApplyReveal();
        }
        else if (change.Property == RevealProperty)
        {
            ApplyReveal();
        }
    }

    private void OnPaneKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab
            || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0
            || e.Source is not Visual source
            || !(OldEditor.IsVisualAncestorOf(source) || NewEditor.IsVisualAncestorOf(source) || UnifiedEditor.IsVisualAncestorOf(source)))
        {
            return;
        }

        var focus = TopLevel.GetTopLevel(this)?.FocusManager;
        var target = (e.KeyModifiers & KeyModifiers.Shift) != 0
            ? PreviousTabStop(source)
            : focus?.FindNextElement(NavigationDirection.Next);
        if (focus is not null && target is not null && !ReferenceEquals(target, source))
        {
            focus.Focus(target, NavigationMethod.Tab, e.KeyModifiers);
            e.Handled = true;
        }
    }

    // shortcut: the text area turns off Avalonia's own backwards search, so this scans the window in visual order and
    // ignores TabIndex. Fine while no page sets TabIndex; use FocusManager.FindNextElement again if that stops holding.
    private static InputElement? PreviousTabStop(Visual from)
    {
        InputElement? previous = null;
        foreach (var candidate in (TopLevel.GetTopLevel(from) as Visual ?? from).GetSelfAndVisualDescendants().OfType<InputElement>())
        {
            if (ReferenceEquals(candidate, from))
            {
                return previous;
            }

            if (candidate.Focusable && candidate.IsTabStop && candidate.IsEffectivelyVisible && candidate.IsEffectivelyEnabled)
            {
                previous = candidate;
            }
        }

        return null;
    }

    private static void Attach(TextEditor editor, DiffViewerBackground background, DiffViewerGutter gutter)
    {
        editor.TextArea.TextView.BackgroundRenderers.Add(background);
        editor.TextArea.LeftMargins.Insert(0, gutter);

        // Keyboard focus lands on the editor's text area, so that is the element a screen reader announces.
        AutomationProperties.SetName(editor.TextArea, AutomationProperties.GetName(editor));
    }

    private void Rebuild()
    {
        var diff = Diff;
        var oldLines = diff?.OldLines ?? [];
        var newLines = diff?.NewLines ?? [];
        OldEditor.Document = new TextDocument(string.Join('\n', oldLines.Select(l => l.Text)));
        NewEditor.Document = new TextDocument(string.Join('\n', newLines.Select(l => l.Text)));
        _oldBackground.Lines = oldLines;
        _newBackground.Lines = newLines;
        _oldGutter.Lines = oldLines;
        _newGutter.Lines = newLines;
        var unified = diff is null ? [] : BuildUnified(diff);
        UnifiedEditor.Document = new TextDocument(string.Join('\n', unified.Select(l => l.Text)));
        _unifiedBackground.Lines = unified;
        _unifiedGutter.Lines = unified;
        _unifiedBackground.ActiveRow = -1;
        _newBackground.ActiveRow = -1;
        Redraw();
        ApplyReveal();
    }

    private void ApplyReveal()
    {
        var row = Reveal is { } reveal && Diff is { } diff
            ? IndexOfNewLine(diff, reveal.NewLine)
            : -1;
        _newBackground.ActiveRow = row;
        var unifiedRow = -1;
        if (row >= 0 && Reveal is { } r)
        {
            unifiedRow = _unifiedBackground.Lines.ToList().FindIndex(l => l.Kind != DiffLineKind.Removed && l.Number == r.NewLine);
        }

        _unifiedBackground.ActiveRow = unifiedRow;
        Redraw();
        if (row >= 0)
        {
            // Wait for layout: a freshly set document has no visual lines yet.
            var (pane, target) = IsUnified ? (UnifiedEditor, unifiedRow) : (NewEditor, row);
            Dispatcher.UIThread.Post(() => pane.ScrollToLine(target + 1), DispatcherPriority.Loaded);
        }
    }

    private void ApplyMode()
    {
        var unified = IsUnified;
        UnifiedEditor.IsVisible = unified;
        OldEditor.IsVisible = !unified;
        NewEditor.IsVisible = !unified;
        PaneSplitter.IsVisible = !unified;
        OldHeader.IsVisible = !unified;
        NewHeader.IsVisible = !unified;
    }

    private static int IndexOfNewLine(FileDiff diff, int newLine)
    {
        for (var i = 0; i < diff.NewLines.Count; i++)
        {
            if (diff.NewLines[i].Number == newLine)
            {
                return i;
            }
        }

        return -1;
    }

    private void Redraw()
    {
        OldEditor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
        NewEditor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
        _oldGutter.InvalidateVisual();
        _newGutter.InvalidateVisual();
        UnifiedEditor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
        _unifiedGutter.InvalidateVisual();
    }

    private void SyncScroll(TextEditor from, TextEditor to)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            var offset = from.TextArea.TextView.ScrollOffset;
            to.ScrollToVerticalOffset(offset.Y);
            to.ScrollToHorizontalOffset(offset.X);
        }
        finally
        {
            _syncing = false;
        }
    }
}
