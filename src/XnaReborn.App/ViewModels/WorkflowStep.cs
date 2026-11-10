namespace XnaReborn.App.ViewModels;

/// <summary>The screens of the guided flow, in order.</summary>
public enum WorkflowStep
{
    /// <summary>Choose a project.</summary>
    Open,

    /// <summary>Read the analysis report.</summary>
    Report,

    /// <summary>Choose how to migrate.</summary>
    MigrationSetup,

    /// <summary>Review the changes.</summary>
    Diff,

    /// <summary>Build and run the migrated copy.</summary>
    BuildRun,

    /// <summary>Before and after summary.</summary>
    Summary,
}
