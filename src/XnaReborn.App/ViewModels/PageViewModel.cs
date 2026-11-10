using XnaReborn.App.Services;

namespace XnaReborn.App.ViewModels;

/// <summary>Base class of the six page view models.</summary>
public abstract class PageViewModel : ViewModelBase
{
    /// <summary>Creates a page.</summary>
    /// <param name="title">Heading of the page.</param>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="navigator">Moves to other pages.</param>
    /// <param name="dialogs">File pickers and message dialogs.</param>
    protected PageViewModel(string title, WorkflowSession session, INavigator navigator, IDialogService dialogs)
    {
        Title = title;
        Session = session;
        Navigator = navigator;
        Dialogs = dialogs;
    }

    /// <summary>Heading of the page.</summary>
    public string Title { get; }

    /// <summary>The shared workflow data. Show an error with <see cref="WorkflowSession.ShowError"/>.</summary>
    protected WorkflowSession Session { get; }

    /// <summary>Ask for another page with <see cref="INavigator.GoTo"/>.</summary>
    protected INavigator Navigator { get; }

    /// <summary>File pickers and message dialogs.</summary>
    protected IDialogService Dialogs { get; }

    /// <summary>Called every time this page becomes the current page. Refresh from <see cref="Session"/> here.</summary>
    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when the app window is closing. Cancel work that is running and stop anything this page started.
    /// Return a completed task when there is nothing to wait for, so the window can close at once.
    /// </summary>
    public virtual Task ShutdownAsync() => Task.CompletedTask;
}
