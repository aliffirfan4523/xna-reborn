using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using XnaReborn.App.ViewModels;

namespace XnaReborn.App.Controls;

/// <summary>A scrolling list of build output lines that follows the newest line.</summary>
public sealed partial class BuildLogView : UserControl
{
    /// <summary>The lines to show. When the list raises collection changes, the view scrolls to the newest line.</summary>
    public static readonly StyledProperty<IReadOnlyList<BuildRunLogLine>?> LinesProperty =
        AvaloniaProperty.Register<BuildLogView, IReadOnlyList<BuildRunLogLine>?>(nameof(Lines));

    private bool _scrollPending;

    /// <summary>Creates the view.</summary>
    public BuildLogView()
    {
        InitializeComponent();
    }

    /// <summary>The lines to show.</summary>
    public IReadOnlyList<BuildRunLogLine>? Lines
    {
        get => GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != LinesProperty)
        {
            return;
        }

        if (change.OldValue is INotifyCollectionChanged old)
        {
            old.CollectionChanged -= OnLinesChanged;
        }

        if (change.NewValue is INotifyCollectionChanged added)
        {
            added.CollectionChanged += OnLinesChanged;
        }

        LogItems.ItemsSource = Lines;
        ScrollToNewest();
    }

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToNewest();

    private void ScrollToNewest()
    {
        if (_scrollPending)
        {
            return;
        }

        _scrollPending = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _scrollPending = false;
                if (Lines is { Count: > 0 } lines)
                {
                    LogItems.ScrollIntoView(lines.Count - 1);
                }
            },
            DispatcherPriority.Background);
    }
}
