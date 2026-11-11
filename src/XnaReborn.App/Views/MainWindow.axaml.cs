using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using XnaReborn.App.Controls;
using XnaReborn.App.ViewModels;

namespace XnaReborn.App.Views;

/// <summary>The only window: navigation rail, header, error banner, the current page and a status bar.</summary>
public sealed partial class MainWindow : Window
{
    private bool _shutdownStarted;
    private bool _shutdownDone;
    private CancellationTokenSource? _fade;

    /// <summary>Creates the window. Set <c>DataContext</c> to a <c>MainViewModel</c>.</summary>
    public MainWindow()
    {
        InitializeComponent();
        ThemeToggle.Click += (_, _) => (DataContext as MainViewModel)?.CycleThemeCommand.Execute(null);
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel main)
            {
                main.PropertyChanged += OnMainChanged;

                // The starting theme is applied by the app at start. From here on a change of the setting is applied at once,
                // whether it came from the rail toggle or from the Settings page.
                main.Settings.Changed += (_, _) => AppearanceApplier.Apply(main.Settings.Current);
            }
        };
    }

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            _ = FadeInPageAsync();
        }
    }

    // A short fade and 8 px rise for the new page. Skipped when the user asked for less motion.
    private async Task FadeInPageAsync()
    {
        if (MotionPreference.Reduce())
        {
            return;
        }

        _fade?.Cancel();
        var cts = _fade = new CancellationTokenSource();
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(180),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.None,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(OpacityProperty, 0.0), new Setter(RenderTransformProperty, TransformOperations.Parse("translateY(8px)")) },
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(OpacityProperty, 1.0), new Setter(RenderTransformProperty, TransformOperations.Parse("translateY(0px)")) },
                },
            },
        };

        try
        {
            await animation.RunAsync(PageHost, cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // A newer page replaced this one; its fade takes over.
        }
    }

    /// <summary>
    /// Cancels running work and stops a running game before the window closes. When nothing has to be waited for, the
    /// window closes at once. Otherwise the close is postponed until the work has stopped, then repeated.
    /// </summary>
    /// <param name="e">The closing event.</param>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || DataContext is not MainViewModel main)
        {
            return;
        }

        if (_shutdownStarted)
        {
            // The first close request is still waiting; the window closes by itself when it is done.
            e.Cancel = !_shutdownDone;
            return;
        }

        _shutdownStarted = true;
        var work = main.ShutdownAsync();
        if (work.IsCompleted)
        {
            _shutdownDone = true;
            return;
        }

        e.Cancel = true;
        _ = FinishCloseAsync(work);
    }

    private async Task FinishCloseAsync(Task work)
    {
        try
        {
            await work.ConfigureAwait(true);
        }
        catch (Exception)
        {
            // Closing goes on; a page that cannot stop its work has nowhere to report it.
        }

        _shutdownDone = true;
        Close();
    }
}
