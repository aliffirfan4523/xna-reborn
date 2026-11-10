using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XnaReborn.App.Services;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Runner;

namespace XnaReborn.App.ViewModels;

/// <summary>One thing a person still has to do after the migration.</summary>
/// <param name="Path">Relative path of the file in the migrated copy.</param>
/// <param name="Kind">Short label: "Manual work" or "Font swapped".</param>
/// <param name="Reason">What to do or what happened.</param>
public sealed record SummaryManualItem(string Path, string Kind, string Reason);

/// <summary>The "Summary" page: score before and after, build result, counts, manual work left and exit buttons.</summary>
public sealed partial class SummaryViewModel : PageViewModel
{
    private readonly IShellService _shell;

    [ObservableProperty]
    private int _beforeScore;

    [ObservableProperty]
    private int _afterScore;

    [ObservableProperty]
    private string _scoreText = string.Empty;

    [ObservableProperty]
    private string _buildText = "Not built yet.";

    [ObservableProperty]
    private int _autoFixedCount;

    [ObservableProperty]
    private int _manualCount;

    [ObservableProperty]
    private int _unsupportedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasManualItems))]
    private IReadOnlyList<SummaryManualItem> _manualItems = [];

    [ObservableProperty]
    private string? _outputFolder;

    /// <summary>Creates the page.</summary>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="navigator">Moves to other pages.</param>
    /// <param name="dialogs">File pickers and message dialogs.</param>
    /// <param name="shell">Opens the output folder and the manifest.</param>
    public SummaryViewModel(WorkflowSession session, INavigator navigator, IDialogService dialogs, IShellService shell)
        : base("Summary", session, navigator, dialogs)
    {
        _shell = shell;
    }

    /// <summary>True when there is manual work left to show.</summary>
    public bool HasManualItems => ManualItems.Count > 0;

    /// <inheritdoc />
    public override Task OnNavigatedToAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads the session again. Auto-fixed counts findings whose rule is in the plan that ran. Manual counts manual findings
    /// plus auto-fixable findings the plan did not include (the user switched them off). Unsupported counts unsupported findings.
    /// </summary>
    public void Refresh()
    {
        var report = Session.Report;
        var result = Session.MigrationResult;
        OutputFolder = Session.MigrationOptions?.OutputFolder;
        BeforeScore = report?.Score ?? 0;
        AfterScore = report?.ProjectedScore ?? 0;
        ScoreText = report is null ? string.Empty : $"{report.Score} to {report.ProjectedScore} (+{report.ProjectedScore - report.Score})";
        BuildText = DescribeBuild(Session.RunnerResult);

        var applied = result?.Plan.Items.Select(i => i.RuleId).ToHashSet(StringComparer.Ordinal) ?? [];
        var findings = report?.Findings ?? [];
        AutoFixedCount = findings.Count(f => f.Classification == Classification.AutoFixable && applied.Contains(f.RuleId));
        ManualCount = findings.Count(f => f.Classification == Classification.Manual
            || (f.Classification == Classification.AutoFixable && !applied.Contains(f.RuleId)));
        UnsupportedCount = findings.Count(f => f.Classification == Classification.Unsupported);
        ManualItems = result is null ? [] : ListManualItems(result.Manifest);
        OpenOutputFolderCommand.NotifyCanExecuteChanged();
        OpenManifestCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private Task OpenOutputFolderAsync() => RunShellAsync(() => _shell.OpenFolderAsync(OutputFolder!));

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private Task OpenManifestAsync() =>
        RunShellAsync(() => _shell.OpenFileAsync(Path.Combine(OutputFolder!, ProjectMigrator.ManifestMarkdownName)));

    [RelayCommand]
    private void StartOver()
    {
        Session.ClearError();
        Session.Report = null;
        Session.SourcePath = null;
        Navigator.GoTo(WorkflowStep.Open);
    }

    private bool HasOutput() => !string.IsNullOrEmpty(OutputFolder);

    private async Task RunShellAsync(Func<Task> open)
    {
        try
        {
            await open().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Session.ShowError("Could not open it: " + ex.Message, ex.ToString());
        }
    }

    private static string DescribeBuild(RunnerResult? run)
    {
        if (run is null)
        {
            return "Not built yet.";
        }

        if (run.Build is { Succeeded: true })
        {
            return run.StartupFailure is null && run.Problem is null ? "Build succeeded." : "Build succeeded. " + run.Problem;
        }

        return "Build did not succeed. " + (run.Problem ?? "See the Build and run page.");
    }

    private static List<SummaryManualItem> ListManualItems(Manifest manifest)
    {
        var items = new List<SummaryManualItem>();
        foreach (var change in manifest.Changes)
        {
            if (change.Kind == ChangeKind.ManualWork)
            {
                items.Add(new SummaryManualItem(change.Path, "Manual work", change.Reason ?? "A person must handle this file."));
            }
            else if (change.Kind == ChangeKind.Modified
                && change.Reason is not null
                && change.Path.EndsWith(".spritefont", StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new SummaryManualItem(change.Path, "Font swapped", change.Reason));
            }
        }

        return items;
    }
}
