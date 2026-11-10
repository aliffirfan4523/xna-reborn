using Avalonia.Controls;
using XnaReborn.App.ViewModels;

namespace XnaReborn.App.Views;

/// <summary>The only window: stepper, error banner and the current page.</summary>
public sealed partial class MainWindow : Window
{
    private bool _shutdownStarted;
    private bool _shutdownDone;

    /// <summary>Creates the window. Set <c>DataContext</c> to a <c>MainViewModel</c>.</summary>
    public MainWindow()
    {
        InitializeComponent();
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
