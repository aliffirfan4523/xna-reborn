using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace XnaReborn.App.Views;

/// <summary>The "Analysis report" page. All behavior is in <see cref="ViewModels.ReportViewModel"/>.</summary>
public sealed partial class ReportView : UserControl
{
    private const double _minFindingsHeight = 520;

    /// <summary>Creates the view.</summary>
    public ReportView()
    {
        InitializeComponent();

        // The dashboard scrolls, so the findings block is given a height instead of a star row: the visible page height, but never less than a usable minimum.
        DashScroll.SizeChanged += (_, e) => FindingsHost.Height = Math.Max(_minFindingsHeight, e.NewSize.Height - 8);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Start at the top; focus moving into the findings must not leave the dashboard scrolled down.
        Dispatcher.UIThread.Post(() => DashScroll.Offset = default, DispatcherPriority.Loaded);
    }
}
