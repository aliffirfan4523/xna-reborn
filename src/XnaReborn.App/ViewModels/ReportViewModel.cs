using XnaReborn.App.Services;

namespace XnaReborn.App.ViewModels;

/// <summary>Placeholder view model for the "Analysis report" page. A screen worker replaces the body.</summary>
public sealed class ReportViewModel : PageViewModel
{
    private readonly IAnalysisService _analysis;
    private readonly IClipboardService _clipboard;

    /// <summary>Creates the page.</summary>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="navigator">Moves to other pages.</param>
    /// <param name="dialogs">File pickers and message dialogs.</param>
    /// <param name="analysis">Service used by this page.</param>
    /// <param name="clipboard">Service used by this page.</param>
    public ReportViewModel(WorkflowSession session, INavigator navigator, IDialogService dialogs, IAnalysisService analysis, IClipboardService clipboard)
        : base("Analysis report", session, navigator, dialogs)
    {
        _analysis = analysis;
        _clipboard = clipboard;
    }
}
