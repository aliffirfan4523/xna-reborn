using CommunityToolkit.Mvvm.ComponentModel;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Runner;

namespace XnaReborn.App.ViewModels;

/// <summary>
/// The data of one run through the flow, shared by all pages. Singleton. Changing <see cref="Report"/> clears everything
/// that depends on it, so set the report first and the later results after it.
/// </summary>
public sealed partial class WorkflowSession : ObservableObject
{
    /// <summary>The folder, .sln or .csproj the user opened.</summary>
    [ObservableProperty]
    private string? _sourcePath;

    /// <summary>The analysis of <see cref="SourcePath"/>. Setting a different report clears the options and results below.</summary>
    [ObservableProperty]
    private AnalysisReport? _report;

    /// <summary>Ids of plan items the user switched off. Replaced, never changed in place. Use <see cref="SetExcluded"/>.</summary>
    [ObservableProperty]
    private IReadOnlySet<string> _excludedPlanItemIds = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>What the user chose on the setup page, or null before.</summary>
    [ObservableProperty]
    private MigrationOptions? _migrationOptions;

    /// <summary>The result of a real migration run, or null before.</summary>
    [ObservableProperty]
    private MigrationResult? _migrationResult;

    /// <summary>The result of a dry run (nothing written), or null before.</summary>
    [ObservableProperty]
    private MigrationResult? _dryRunResult;

    /// <summary>The result of building and running the migrated copy, or null before.</summary>
    [ObservableProperty]
    private RunnerResult? _runnerResult;

    /// <summary>The short error message shown in the banner, or null when there is no error.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>Technical details of the error, or null.</summary>
    [ObservableProperty]
    private string? _errorDetails;

    /// <summary>True while an error is shown in the banner.</summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>Shows an error in the banner.</summary>
    /// <param name="message">What went wrong, in plain words.</param>
    /// <param name="details">Technical details, or null.</param>
    public void ShowError(string message, string? details = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ErrorDetails = details;
        ErrorMessage = message;
    }

    /// <summary>Hides the error banner.</summary>
    public void ClearError()
    {
        ErrorMessage = null;
        ErrorDetails = null;
    }

    /// <summary>Switches one plan item on or off. Raises a change notification for <see cref="ExcludedPlanItemIds"/>.</summary>
    /// <param name="planItemId">The plan item id.</param>
    /// <param name="excluded">True to exclude the item.</param>
    public void SetExcluded(string planItemId, bool excluded)
    {
        ArgumentNullException.ThrowIfNull(planItemId);
        var set = new HashSet<string>(ExcludedPlanItemIds, StringComparer.Ordinal);
        if (excluded ? set.Add(planItemId) : set.Remove(planItemId))
        {
            ExcludedPlanItemIds = set;
        }
    }

    partial void OnReportChanged(AnalysisReport? value)
    {
        ExcludedPlanItemIds = new HashSet<string>(StringComparer.Ordinal);
        MigrationOptions = null;
        DryRunResult = null;
        MigrationResult = null;
        RunnerResult = null;
    }
}
