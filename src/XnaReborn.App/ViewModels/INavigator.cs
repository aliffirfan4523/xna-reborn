namespace XnaReborn.App.ViewModels;

/// <summary>Moves between screens. Page view models ask for navigation through this and never touch each other.</summary>
public interface INavigator
{
    /// <summary>Shows <paramref name="step"/> when <see cref="CanGoTo"/> allows it. Does nothing otherwise.</summary>
    /// <param name="step">The screen to show.</param>
    void GoTo(WorkflowStep step);

    /// <summary>True when the screen's prerequisites exist in the session, or the screen is the current one or an earlier one.</summary>
    /// <param name="step">The screen to test.</param>
    bool CanGoTo(WorkflowStep step);

    /// <summary>Leaves the Settings page and shows the screen that was current before. Does nothing when Settings is not open.</summary>
    void CloseSettings();
}
