using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XnaReborn.App.Controls;
using XnaReborn.App.Services;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Source;

namespace XnaReborn.App.ViewModels;

/// <summary>
/// The "Review changes" page: the changed files of a migration (or of a dry run, before anything is written),
/// a side-by-side diff of the selected file, and navigation between <c>// XNAREBORN:</c> manual-work markers.
/// </summary>
public sealed partial class DiffViewModel : PageViewModel
{
    /// <summary>The text a marker line contains.</summary>
    public const string MarkerPrefix = "// XNAREBORN:";

    /// <summary>The banner shown above a dry run.</summary>
    public const string PreviewBannerText = "Preview, nothing written yet";

    /// <summary>
    /// Files with more characters than this (in the old or new text) show a placeholder instead of a diff.
    /// shortcut: the diff runs on the UI thread, so the limit keeps it quick; move it to a background task to raise the limit.
    /// </summary>
    public const int MaxDiffCharacters = 200_000;

    private readonly IMigrationService _migration;

    [ObservableProperty]
    private MigrationResult? _result;

    [ObservableProperty]
    private bool _isDryRun;

    [ObservableProperty]
    private IReadOnlyList<DiffFileItem> _files = [];

    [ObservableProperty]
    private IReadOnlyList<DiffKindCount> _kindCounts = [];

    [ObservableProperty]
    private IReadOnlyList<DiffMarker> _markers = [];

    [ObservableProperty]
    private DiffFileItem? _selectedFile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiff))]
    private FileDiff? _currentDiff;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlaceholder))]
    private string? _placeholder;

    [ObservableProperty]
    private DiffViewerReveal? _reveal;

    [ObservableProperty]
    private int _currentMarkerIndex = -1;

    /// <summary>Creates the page.</summary>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="navigator">Moves to other pages.</param>
    /// <param name="dialogs">File pickers and message dialogs.</param>
    /// <param name="migration">Service used by this page.</param>
    public DiffViewModel(WorkflowSession session, INavigator navigator, IDialogService dialogs, IMigrationService migration)
        : base("Review changes", session, navigator, dialogs)
    {
        _migration = migration;
    }

    /// <summary>True when <see cref="CurrentDiff"/> is shown.</summary>
    public bool HasDiff => CurrentDiff is not null;

    /// <summary>True when <see cref="Placeholder"/> is shown instead of a diff.</summary>
    public bool HasPlaceholder => Placeholder is not null;

    /// <summary>The banner text for a dry run.</summary>
    public string PreviewBanner => PreviewBannerText;

    /// <summary>True when the result is a real, finished migration, so Build and run can follow.</summary>
    public bool CanContinue => !IsDryRun && Result is { Completed: true };

    /// <summary>True when there is at least one marker.</summary>
    public bool HasMarkers => Markers.Count > 0;

    /// <summary>For example "Marker 2 of 5", or "No manual-work markers".</summary>
    public string MarkerStatus => Markers.Count == 0
        ? "No manual-work markers"
        : CurrentMarkerIndex < 0
            ? string.Create(CultureInfo.InvariantCulture, $"{Markers.Count} manual-work markers")
            : string.Create(CultureInfo.InvariantCulture, $"Marker {CurrentMarkerIndex + 1} of {Markers.Count}");

    /// <summary>One-line summary above the list, for example "5 files".</summary>
    public string FilesSummary => Files.Count == 1 ? "1 file" : string.Create(CultureInfo.InvariantCulture, $"{Files.Count} files");

    /// <summary>Reads the migration result (or else the dry run) from the session.</summary>
    public override Task OnNavigatedToAsync()
    {
        Load();
        return Task.CompletedTask;
    }

    /// <summary>Selects the next marker (wraps around) and scrolls to it.</summary>
    [RelayCommand(CanExecute = nameof(HasMarkers))]
    private void NextMarker() => GoToMarker(CurrentMarkerIndex < 0 ? 0 : (CurrentMarkerIndex + 1) % Markers.Count);

    /// <summary>Selects the previous marker (wraps around) and scrolls to it.</summary>
    [RelayCommand(CanExecute = nameof(HasMarkers))]
    private void PreviousMarker() => GoToMarker(CurrentMarkerIndex <= 0 ? Markers.Count - 1 : CurrentMarkerIndex - 1);

    /// <summary>Goes back to Migration setup, for example to run a dry run for real.</summary>
    [RelayCommand]
    private void BackToSetup() => Navigator.GoTo(WorkflowStep.MigrationSetup);

    /// <summary>Goes on to Build and run. Only possible after a real migration.</summary>
    [RelayCommand(CanExecute = nameof(CanContinue))]
    private void Continue() => Navigator.GoTo(WorkflowStep.BuildRun);

    partial void OnSelectedFileChanged(DiffFileItem? value)
    {
        Reveal = null;
        ShowFile(value);
    }

    partial void OnFilesChanged(IReadOnlyList<DiffFileItem> value) => OnPropertyChanged(nameof(FilesSummary));

    partial void OnMarkersChanged(IReadOnlyList<DiffMarker> value)
    {
        OnPropertyChanged(nameof(HasMarkers));
        OnPropertyChanged(nameof(MarkerStatus));
        NextMarkerCommand.NotifyCanExecuteChanged();
        PreviousMarkerCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentMarkerIndexChanged(int value) => OnPropertyChanged(nameof(MarkerStatus));

    partial void OnIsDryRunChanged(bool value) => NotifyContinue();

    partial void OnResultChanged(MigrationResult? value) => NotifyContinue();

    private void NotifyContinue()
    {
        OnPropertyChanged(nameof(CanContinue));
        ContinueCommand.NotifyCanExecuteChanged();
    }

    private void Load()
    {
        // A real migration wins over an older dry run. Only the dry run case shows the preview banner.
        var result = Session.MigrationResult ?? Session.DryRunResult;
        IsDryRun = Session.MigrationResult is null;
        Result = result;

        var changes = result?.Changes ?? [];
        var items = changes
            .Select(c => new DiffFileItem(c, MarkersIn(c).Count))
            .OrderBy(i => DiffKindInfo.Order(i.Kind))
            .ThenBy(i => i.Path, StringComparer.Ordinal)
            .ToList();
        KindCounts = items
            .GroupBy(i => i.Kind)
            .OrderBy(g => DiffKindInfo.Order(g.Key))
            .Select(g => new DiffKindCount(g.Key, g.Count()))
            .ToList();
        Markers = items.SelectMany(i => MarkersIn(i.Change)).ToList();
        CurrentMarkerIndex = -1;
        Files = items;

        // Select by value so a reload keeps working when the first item did not change.
        SelectedFile = null;
        SelectedFile = items.FirstOrDefault();
    }

    private void GoToMarker(int index)
    {
        var marker = Markers[index];
        var file = Files.FirstOrDefault(f => string.Equals(f.Path, marker.Path, StringComparison.Ordinal));
        if (file is not null && !Equals(file, SelectedFile))
        {
            SelectedFile = file;
        }

        CurrentMarkerIndex = index;
        Reveal = new DiffViewerReveal(marker.NewLine);
    }

    private void ShowFile(DiffFileItem? item)
    {
        CurrentDiff = null;
        Placeholder = null;
        if (item is null)
        {
            return;
        }

        var change = item.Change;
        var original = change.OriginalText;
        var updated = change.NewText;
        if (original is null && updated is null)
        {
            Placeholder = NoTextPlaceholder(change);
            return;
        }

        if (HasNul(original) || HasNul(updated))
        {
            Placeholder = "Binary file. There is no text diff to show.";
            return;
        }

        var size = Math.Max(original?.Length ?? 0, updated?.Length ?? 0);
        if (size > MaxDiffCharacters)
        {
            Placeholder = string.Create(CultureInfo.InvariantCulture, $"This file is too large to compare here ({size:N0} characters). Open it in an editor to review it.");
            return;
        }

        CurrentDiff = DiffBuilder.BuildSideBySide(change.RelativePath, original ?? string.Empty, updated ?? string.Empty);
    }

    private static string NoTextPlaceholder(FileChange change) => change.Kind switch
    {
        ChangeKind.Copied => "This file is copied as it is. It is not a text file, or it did not change, so there is no diff to show.",
        ChangeKind.ManualWork => "This file is copied unchanged and needs manual work." + Reason(change),
        ChangeKind.Skipped => "This file is left out of the output." + Reason(change),
        _ => "There is no text to show for this file." + Reason(change),
    };

    private static string Reason(FileChange change) => string.IsNullOrWhiteSpace(change.Reason) ? string.Empty : " " + change.Reason;

    private static bool HasNul(string? text) => text is not null && text.Contains('\0', StringComparison.Ordinal);

    private static List<DiffMarker> MarkersIn(FileChange change)
    {
        var found = new List<DiffMarker>();
        if (change.NewText is not { Length: > 0 } text || !MarkerPrefixes.ContainsMarker(text))
        {
            return found;
        }

        // Same numbering as DiffBuilder: lines split on '\n', CR dropped.
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (MarkerPrefixes.ContainsMarker(lines[i]))
            {
                found.Add(new DiffMarker(change.RelativePath, i + 1, lines[i].Trim()));
            }
        }

        return found;
    }
}
