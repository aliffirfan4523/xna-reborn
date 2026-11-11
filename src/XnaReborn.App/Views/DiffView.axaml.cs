using Avalonia.Controls;

namespace XnaReborn.App.Views;

/// <summary>The "Review changes" page: changed files, side-by-side diff, marker navigation and the dry-run banner.</summary>
public sealed partial class DiffView : UserControl
{
    // Header, filters, a usable diff pane and the footer buttons. Below this the page scrolls.
    private const double _minPageHeight = 620;

    /// <summary>Creates the view.</summary>
    public DiffView()
    {
        InitializeComponent();
        PageScroll.SizeChanged += (_, e) => PageRoot.Height = Math.Max(e.NewSize.Height, _minPageHeight);
    }
}
