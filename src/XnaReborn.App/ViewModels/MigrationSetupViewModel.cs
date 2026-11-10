using XnaReborn.App.Services;

namespace XnaReborn.App.ViewModels;

/// <summary>Placeholder view model for the "Migration setup" page. A screen worker replaces the body.</summary>
public sealed class MigrationSetupViewModel : PageViewModel
{
    private readonly IMigrationService _migration;

    /// <summary>Creates the page.</summary>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="navigator">Moves to other pages.</param>
    /// <param name="dialogs">File pickers and message dialogs.</param>
    /// <param name="migration">Service used by this page.</param>
    public MigrationSetupViewModel(WorkflowSession session, INavigator navigator, IDialogService dialogs, IMigrationService migration)
        : base("Migration setup", session, navigator, dialogs)
    {
        _migration = migration;
    }
}
