using Avalonia.Controls;

namespace XnaReborn.App.Views;

/// <summary>The "Review changes" page: changed files, side-by-side diff, marker navigation and the dry-run banner.</summary>
public sealed partial class DiffView : UserControl
{
    /// <summary>Creates the view.</summary>
    public DiffView()
    {
        InitializeComponent();
    }
}
