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

/// <summary>One marker a Flash conversion left in the code, as a link.</summary>
/// <param name="File">Relative path of the original .as file.</param>
/// <param name="Line">1-based line in the original file.</param>
/// <param name="ConvertedFile">Relative path of the converted C# file in the output folder. The link opens this file.</param>
public sealed record FlashMarkerLink(string File, int Line, string ConvertedFile)
{
    /// <summary>The link text, such as "src/Player.as, line 12".</summary>
    public string Text => $"{File}, line {Line}";

    /// <summary>The name a screen reader reads for the link.</summary>
    public string AutomationName => $"Open the converted file for {File}, line {Line}";
}

/// <summary>All markers of one rule in a Flash conversion.</summary>
/// <param name="RuleId">The rule id, such as FL020.</param>
/// <param name="Title">The rule title, or an empty string when the report does not know the rule.</param>
/// <param name="Links">One link per marker, sorted by file and line.</param>
public sealed record FlashMarkerGroup(string RuleId, string Title, IReadOnlyList<FlashMarkerLink> Links)
{
    /// <summary>The header, such as "FL020 'with' statement: 2 markers".</summary>
    public string Header => $"{(Title.Length == 0 ? RuleId : RuleId + " " + Title)}: {Links.Count} {(Links.Count == 1 ? "marker" : "markers")}";
}

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFlashMarkers))]
    private IReadOnlyList<FlashMarkerGroup> _flashMarkers = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFlashApproximations))]
    private IReadOnlyList<string> _flashApproximations = [];

    [ObservableProperty]
    private bool _isFlash;

    [ObservableProperty]
    private int _assetsBuiltCount;

    [ObservableProperty]
    private int _fontsSwappedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BuildSucceeded))]
    [NotifyPropertyChangedFor(nameof(HeadlineText))]
    private bool _buildFailed;

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

    /// <summary>True when a Flash conversion left at least one marker in the code.</summary>
    public bool HasFlashMarkers => FlashMarkers.Count > 0;

    /// <summary>True when the page lists the behaviors a Flash conversion only approximates.</summary>
    public bool HasFlashApproximations => FlashApproximations.Count > 0;

    /// <summary>True when a build ran and succeeded. False when it failed or has not run.</summary>
    public bool BuildSucceeded => !BuildFailed && Session.RunnerResult?.Build is { Succeeded: true };

    /// <summary>The big line at the top: ready, failed, or finished without a build.</summary>
    public string HeadlineText => BuildFailed ? "The build did not succeed" : BuildSucceeded ? "Your project is migrated" : "Migration finished";

    /// <summary>The score gain as text, such as "+50 points". Empty without a report.</summary>
    public string GainText => Session.Report is { } r ? $"{(r.ProjectedScore >= r.Score ? "+" : string.Empty)}{r.ProjectedScore - r.Score} points" : string.Empty;

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
        BuildFailed = Session.RunnerResult?.Build is { Succeeded: false };
        OnPropertyChanged(nameof(BuildSucceeded));
        OnPropertyChanged(nameof(GainText));
        AssetsBuiltCount = result?.Manifest.Content?.CountsByStatus.GetValueOrDefault("Converted") ?? 0;

        var applied = result?.Plan.Items.Select(i => i.RuleId).ToHashSet(StringComparer.Ordinal) ?? [];
        var findings = report?.Findings ?? [];
        AutoFixedCount = findings.Count(f => f.Classification == Classification.AutoFixable && applied.Contains(f.RuleId));
        ManualCount = findings.Count(f => f.Classification == Classification.Manual
            || (f.Classification == Classification.AutoFixable && !applied.Contains(f.RuleId)));
        UnsupportedCount = findings.Count(f => f.Classification == Classification.Unsupported);
        ManualItems = result is null ? [] : ListManualItems(result.Manifest);
        FontsSwappedCount = ManualItems.Count(i => i.Kind == "Font swapped");
        IsFlash = result?.Manifest.SourceKind == "Flash";
        FlashMarkers = IsFlash ? ListFlashMarkers(result!.Manifest, report) : [];
        FlashApproximations = IsFlash ? result!.Manifest.Approximations ?? [] : [];
        OpenOutputFolderCommand.NotifyCanExecuteChanged();
        OpenManifestCommand.NotifyCanExecuteChanged();
        OpenMarkerCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private Task OpenOutputFolderAsync() => RunShellAsync(() => _shell.OpenFolderAsync(OutputFolder!));

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private Task OpenManifestAsync() =>
        RunShellAsync(() => _shell.OpenFileAsync(Path.Combine(OutputFolder!, ProjectMigrator.ManifestMarkdownName)));

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private Task OpenMarkerAsync(FlashMarkerLink? link) =>
        link is null ? Task.CompletedTask : RunShellAsync(() => _shell.OpenFileAsync(Path.Combine(OutputFolder!, link.ConvertedFile)));

    [RelayCommand]
    private void RunGame() => Navigator.GoTo(WorkflowStep.BuildRun);

    [RelayCommand]
    private void GoToDiff() => Navigator.GoTo(WorkflowStep.Diff);

    [RelayCommand]
    private void GoToReport() => Navigator.GoTo(WorkflowStep.Report);

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

    private static List<FlashMarkerGroup> ListFlashMarkers(Manifest manifest, AnalysisReport? report)
    {
        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rule in report?.TriggeredRules ?? [])
        {
            titles.TryAdd(rule.Id, rule.Title);
        }

        return (manifest.MarkerSites ?? [])
            .GroupBy(m => m.RuleId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new FlashMarkerGroup(g.Key, titles.GetValueOrDefault(g.Key, string.Empty), [.. g.Select(m => new FlashMarkerLink(m.File, m.Line, m.ConvertedFile))]))
            .ToList();
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
