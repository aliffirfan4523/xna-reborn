using XnaReborn.App.Services;

namespace XnaReborn.App.ViewModels;

/// <summary>Placeholder view model for the "Open a project" page. A screen worker replaces the body.</summary>
public sealed class OpenProjectViewModel : PageViewModel
{
    private readonly IAnalysisService _analysis;
    private readonly IRecentProjectsStore _recentProjects;

    /// <summary>Creates the page.</summary>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="navigator">Moves to other pages.</param>
    /// <param name="dialogs">File pickers and message dialogs.</param>
    /// <param name="analysis">Service used by this page.</param>
    /// <param name="recentProjects">Service used by this page.</param>
    public OpenProjectViewModel(WorkflowSession session, INavigator navigator, IDialogService dialogs, IAnalysisService analysis, IRecentProjectsStore recentProjects)
        : base("Open a project", session, navigator, dialogs)
    {
        _analysis = analysis;
        _recentProjects = recentProjects;
    }
}
