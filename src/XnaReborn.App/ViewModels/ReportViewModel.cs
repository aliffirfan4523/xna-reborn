using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XnaReborn.App.Services;
using XnaReborn.Core.Models;

namespace XnaReborn.App.ViewModels;

/// <summary>
/// The "Analysis report" page. Shows the score, the findings grouped by classification with filters and search,
/// the details of the selected finding, and exports the report. It shows <see cref="WorkflowSession.Report"/> and follows its changes.
/// </summary>
public sealed partial class ReportViewModel : PageViewModel
{
    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly Classification[] _classificationOrder = [Classification.AutoFixable, Classification.Manual, Classification.Unsupported];

    private readonly IAnalysisService _analysis;
    private readonly IClipboardService _clipboard;
    private IReadOnlyList<ReportFindingItem> _all = [];
    private bool _loading;

    [ObservableProperty]
    private AnalysisReport? _report;

    [ObservableProperty]
    private ReportFilterChoice<Severity> _selectedSeverity;

    [ObservableProperty]
    private ReportFilterChoice<Classification> _selectedClassification;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<ReportGroup> _groups = [];

    [ObservableProperty]
    private IReadOnlyList<ReportRow> _rows = [];

    [ObservableProperty]
    private ReportRow? _selectedRow;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Creates the page.</summary>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="navigator">Moves to other pages.</param>
    /// <param name="dialogs">File pickers and message dialogs.</param>
    /// <param name="analysis">Renders the report as Markdown and JSON.</param>
    /// <param name="clipboard">Copies text.</param>
    public ReportViewModel(WorkflowSession session, INavigator navigator, IDialogService dialogs, IAnalysisService analysis, IClipboardService clipboard)
        : base("Analysis report", session, navigator, dialogs)
    {
        _analysis = analysis;
        _clipboard = clipboard;
        SeverityChoices =
        [
            new ReportFilterChoice<Severity>("All severities", null),
            .. Enum.GetValues<Severity>().Select(s => new ReportFilterChoice<Severity>(s.ToString(), s)),
        ];
        ClassificationChoices =
        [
            new ReportFilterChoice<Classification>("All classifications", null),
            .. _classificationOrder.Select(c => new ReportFilterChoice<Classification>(ReportGroup.LabelOf(c), c)),
        ];
        _selectedSeverity = SeverityChoices[0];
        _selectedClassification = ClassificationChoices[0];
        session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkflowSession.Report))
            {
                LoadReport(session.Report);
            }
        };
        LoadReport(session.Report);
    }

    /// <summary>The entries of the severity filter. The first one keeps every severity.</summary>
    public IReadOnlyList<ReportFilterChoice<Severity>> SeverityChoices { get; }

    /// <summary>The entries of the classification filter. The first one keeps every classification.</summary>
    public IReadOnlyList<ReportFilterChoice<Classification>> ClassificationChoices { get; }

    /// <summary>True when there is a report to show.</summary>
    public bool HasReport => Report is not null;

    /// <summary>True when the report found at least one XNA project.</summary>
    public bool HasProject => Report is { NoProjectReason: null };

    /// <summary>True when analysis found no XNA project. <see cref="NoProjectReason"/> says why.</summary>
    public bool ShowNoProject => Report is { NoProjectReason: not null };

    /// <summary>Why no XNA project was found, or an empty string.</summary>
    public string NoProjectReason => Report?.NoProjectReason ?? string.Empty;

    /// <summary>Compatibility score from 0 to 100.</summary>
    public int Score => Report?.Score ?? 0;

    /// <summary>Score from 0 to 100 if the auto-fixable findings were fixed.</summary>
    public int ProjectedScore => Report?.ProjectedScore ?? 0;

    /// <summary>Number of auto-fixable findings in the report.</summary>
    public int AutoFixableCount => CountOf(Classification.AutoFixable);

    /// <summary>Number of manual findings in the report.</summary>
    public int ManualCount => CountOf(Classification.Manual);

    /// <summary>Number of unsupported findings in the report.</summary>
    public int UnsupportedCount => CountOf(Classification.Unsupported);

    /// <summary>Problems met during analysis.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics => Report?.Diagnostics ?? [];

    /// <summary>True when analysis reported problems.</summary>
    public bool HasDiagnostics => Diagnostics.Count > 0;

    /// <summary>Number of findings that pass the filters.</summary>
    public int VisibleCount => Rows.Count;

    /// <summary>Number of findings before filtering.</summary>
    public int TotalCount => _all.Count;

    /// <summary>"Showing 3 of 12 findings".</summary>
    public string FilterSummary => $"Showing {VisibleCount} of {TotalCount} findings";

    /// <summary>True when a filter or the search text narrows the list.</summary>
    public bool HasActiveFilter => SelectedSeverity.Value is not null || SelectedClassification.Value is not null || SearchText.Trim().Length > 0;

    /// <summary>True when the report has no findings at all.</summary>
    public bool ShowNoFindings => HasProject && TotalCount == 0;

    /// <summary>True when the report has findings but the filters hide all of them.</summary>
    public bool ShowNoMatches => TotalCount > 0 && VisibleCount == 0;

    /// <summary>The selected finding, or null.</summary>
    public ReportFindingItem? SelectedFinding => SelectedRow?.Item;

    /// <summary>True when a finding is selected.</summary>
    public bool HasSelection => SelectedRow is not null;

    /// <summary>True when the project has findings and none is selected, so the detail pane shows a hint.</summary>
    public bool ShowSelectionHint => HasProject && !HasSelection;

    /// <summary>The "Start here" tip for the current report, or null when there is no project.</summary>
    public StartHereTip? StartHere => StartHereTip.From(Report);

    /// <summary>True when the tip is shown.</summary>
    public bool HasStartHere => StartHere is not null;

    /// <summary>True when the tip shows its lines. The choice lasts as long as the app runs, because this page is created once.</summary>
    [ObservableProperty]
    private bool _isStartHereExpanded = true;

    /// <summary>Shows only the findings of one classification. Used by the tip's buttons.</summary>
    /// <param name="classification">The classification to show.</param>
    [RelayCommand]
    public void ShowClassification(Classification classification) =>
        SelectedClassification = ClassificationChoices.Single(c => c.Value == classification);

    /// <summary>Sets the filters and the search text back to "show everything".</summary>
    [RelayCommand(CanExecute = nameof(HasActiveFilter))]
    public void ClearFilters()
    {
        _loading = true;
        SelectedSeverity = SeverityChoices[0];
        SelectedClassification = ClassificationChoices[0];
        SearchText = string.Empty;
        _loading = false;
        ApplyFilters();
    }

    /// <summary>Asks for a file name, then saves the report as Markdown.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    [RelayCommand(CanExecute = nameof(HasReport))]
    public Task ExportMarkdownAsync(CancellationToken cancellationToken) => ExportAsync("Markdown", "md", _analysis.ToMarkdown, cancellationToken);

    /// <summary>Asks for a file name, then saves the report as JSON.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    [RelayCommand(CanExecute = nameof(HasReport))]
    public Task ExportJsonAsync(CancellationToken cancellationToken) => ExportAsync("JSON", "json", _analysis.ToJson, cancellationToken);

    /// <summary>Copies the whole report as Markdown.</summary>
    [RelayCommand(CanExecute = nameof(HasReport))]
    public Task CopyReportAsync() => CopyAsync(Report is { } r ? _analysis.ToMarkdown(r) : null, "Report copied as Markdown.");

    /// <summary>Copies the selected finding as plain text.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    public Task CopyFindingAsync() => CopyAsync(SelectedFinding?.ToClipboardText(), "Finding copied.");

    /// <summary>Goes to the migration setup page.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    public void Continue() => Navigator.GoTo(WorkflowStep.MigrationSetup);

    /// <summary>Goes back to the page where a project is chosen.</summary>
    [RelayCommand]
    public void ChooseAnotherProject() => Navigator.GoTo(WorkflowStep.Open);

    partial void OnSelectedSeverityChanged(ReportFilterChoice<Severity> value) => ApplyFilters();

    partial void OnSelectedClassificationChanged(ReportFilterChoice<Classification> value) => ApplyFilters();

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedRowChanged(ReportRow? value)
    {
        OnPropertyChanged(nameof(SelectedFinding));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowSelectionHint));
        CopyFindingCommand.NotifyCanExecuteChanged();
    }

    private void LoadReport(AnalysisReport? report)
    {
        var rules = new Dictionary<string, TriggeredRule>(StringComparer.Ordinal);
        foreach (var rule in report?.TriggeredRules ?? [])
        {
            rules.TryAdd(rule.Id, rule);
        }

        _all = report?.Findings.Select(f => new ReportFindingItem(f, rules.GetValueOrDefault(f.RuleId))).ToList() ?? [];
        _loading = true;
        Report = report;
        SelectedSeverity = SeverityChoices[0];
        SelectedClassification = ClassificationChoices[0];
        SearchText = string.Empty;
        SelectedRow = null;
        StatusMessage = string.Empty;
        _loading = false;
        ApplyFilters();
    }

    partial void OnReportChanged(AnalysisReport? value)
    {
        foreach (var name in new[]
        {
            nameof(HasReport), nameof(HasProject), nameof(ShowNoProject), nameof(NoProjectReason), nameof(Score), nameof(ProjectedScore),
            nameof(AutoFixableCount), nameof(ManualCount), nameof(UnsupportedCount), nameof(Diagnostics), nameof(HasDiagnostics),
            nameof(StartHere), nameof(HasStartHere),
        })
        {
            OnPropertyChanged(name);
        }

        ExportMarkdownCommand.NotifyCanExecuteChanged();
        ExportJsonCommand.NotifyCanExecuteChanged();
        CopyReportCommand.NotifyCanExecuteChanged();
        ContinueCommand.NotifyCanExecuteChanged();
    }

    private void ApplyFilters()
    {
        if (_loading)
        {
            return;
        }

        var severity = SelectedSeverity.Value;
        var classification = SelectedClassification.Value;
        var text = SearchText.Trim();
        var matches = _all
            .Where(i => (severity is null || i.Severity == severity) && (classification is null || i.Classification == classification) && i.Matches(text))
            .ToList();

        var groups = new List<ReportGroup>();
        var rows = new List<ReportRow>();
        foreach (var c in _classificationOrder)
        {
            var items = matches.Where(i => i.Classification == c).ToList();
            if (items.Count == 0)
            {
                continue;
            }

            var group = new ReportGroup(c, items);
            groups.Add(group);
            rows.AddRange(items.Select((item, index) => new ReportRow(item, index == 0 ? group : null)));
        }

        var selected = SelectedFinding;
        Groups = groups;
        Rows = rows;
        SelectedRow = selected is null ? null : rows.FirstOrDefault(r => ReferenceEquals(r.Item, selected));
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(FilterSummary));
        OnPropertyChanged(nameof(HasActiveFilter));
        OnPropertyChanged(nameof(ShowNoFindings));
        OnPropertyChanged(nameof(ShowNoMatches));
        OnPropertyChanged(nameof(ShowSelectionHint));
        ClearFiltersCommand.NotifyCanExecuteChanged();
    }

    private int CountOf(Classification classification) =>
        Report is not null && Report.CountsByClassification.TryGetValue(classification, out var n) ? n : 0;

    private async Task ExportAsync(string kind, string extension, Func<AnalysisReport, string> render, CancellationToken cancellationToken)
    {
        if (Report is not { } report)
        {
            return;
        }

        try
        {
            var path = await Dialogs.SaveFileAsync($"Export report as {kind}", "xna-reborn-report", extension).ConfigureAwait(true);
            if (path is null)
            {
                return;
            }

            if (IsInsideSource(path))
            {
                // The original project is never modified, and a report file counts as a change.
                Session.ShowError("Choose a place outside the original project. XNA Reborn never writes into it.", path);
                return;
            }

            await File.WriteAllTextAsync(path, render(report), _utf8NoBom, cancellationToken).ConfigureAwait(true);
            StatusMessage = $"Report saved to {path}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Export cancelled.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Session.ShowError("The report could not be saved.", ex.Message);
        }
    }

    private async Task CopyAsync(string? text, string doneMessage)
    {
        if (text is null)
        {
            return;
        }

        try
        {
            await _clipboard.SetTextAsync(text).ConfigureAwait(true);
            StatusMessage = doneMessage;
        }
        catch (Exception ex)
        {
            // Another program can lock the clipboard. Tell the user instead of failing silently.
            Session.ShowError("The text could not be copied.", ex.Message);
        }
    }

    // shortcut: compares full paths only, so a link or junction that points into the project is not detected. Resolve links if that ever matters.
    private bool IsInsideSource(string path)
    {
        if (Session.SourcePath is not { Length: > 0 } source)
        {
            return false;
        }

        var root = Directory.Exists(source) ? source : Path.GetDirectoryName(source);
        if (root is null)
        {
            return false;
        }

        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }
}
