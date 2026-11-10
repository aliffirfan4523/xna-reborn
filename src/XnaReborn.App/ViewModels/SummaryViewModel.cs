using XnaReborn.App.Services;

namespace XnaReborn.App.ViewModels;

/// <summary>Placeholder view model for the "Summary" page. A screen worker replaces the body.</summary>
public sealed class SummaryViewModel : PageViewModel
{
    private readonly IRunService _runner;

    /// <summary>Creates the page.</summary>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="navigator">Moves to other pages.</param>
    /// <param name="dialogs">File pickers and message dialogs.</param>
    /// <param name="runner">Service used by this page.</param>
    public SummaryViewModel(WorkflowSession session, INavigator navigator, IDialogService dialogs, IRunService runner)
        : base("Summary", session, navigator, dialogs)
    {
        _runner = runner;
    }
}
