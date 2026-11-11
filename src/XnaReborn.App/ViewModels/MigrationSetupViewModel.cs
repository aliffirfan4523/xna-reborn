using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XnaReborn.App.Services;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;

namespace XnaReborn.App.ViewModels;

/// <summary>What the live check of the output folder found.</summary>
public enum OutputCheckState
{
    /// <summary>No folder to check yet.</summary>
    Unknown,

    /// <summary>The folder can be used.</summary>
    Ok,

    /// <summary>The folder has content. The user must say yes before it is used.</summary>
    NeedsConfirmation,

    /// <summary>The folder cannot be used.</summary>
    Refused,
}

/// <summary>
/// The "Migration setup" page: output folder, target, options, plan checklist, Preview and Migrate.
/// The page never writes anything itself. It never starts a run for a refused folder, and it asks the user before
/// it lets a run use a folder that already has content.
/// </summary>
public sealed partial class MigrationSetupViewModel : PageViewModel
{
    // Plan items of these rules may be switched off. Every other plan item is always applied.
    private static readonly HashSet<string> _excludableRuleIds = new(StringComparer.Ordinal)
    {
        "XNA005", "XNA022", "XNA024", "XNA039", "XNA040", "XNA049",
    };

    private readonly IMigrationService _migration;
    private object? _loadedReport;
    private string? _loadedSource;
    private CancellationTokenSource? _runCancellation;

    [ObservableProperty]
    private string _outputFolder = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDesktopGL))]
    private bool _isWindowsDx;

    [ObservableProperty]
    private bool _enableShims;

    [ObservableProperty]
    private bool _substituteFonts;

    [ObservableProperty]
    private bool _allowWindowResize;

    [ObservableProperty]
    private bool _fullscreenToggle;

    [ObservableProperty]
    private bool _highDpi;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputIsOk), nameof(OutputNeedsConfirmation), nameof(OutputIsRefused))]
    private OutputCheckState _outputState = OutputCheckState.Unknown;

    [ObservableProperty]
    private string _outputMessage = "Choose a folder for the migrated copy.";

    [ObservableProperty]
    private string _planSummary = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    private bool _isBusy;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private bool _progressIsIndeterminate;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>Creates the page.</summary>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="navigator">Moves to other pages.</param>
    /// <param name="dialogs">File pickers and message dialogs.</param>
    /// <param name="migration">Plans and runs the migration and checks the output folder.</param>
    public MigrationSetupViewModel(WorkflowSession session, INavigator navigator, IDialogService dialogs, IMigrationService migration)
        : base("Migration setup", session, navigator, dialogs)
    {
        _migration = migration;
    }

    /// <summary>The plan checklist.</summary>
    public ObservableCollection<PlanItemViewModel> PlanItems { get; } = [];

    /// <summary>True when the plan has at least one item.</summary>
    public bool HasPlanItems => PlanItems.Count > 0;

    /// <summary>True when MonoGame DesktopGL is the chosen target (the default, except for a SharpDX Toolkit project, which starts on WindowsDX).</summary>
    public bool IsDesktopGL
    {
        get => !IsWindowsDx;
        set
        {
            if (value)
            {
                IsWindowsDx = false;
            }
        }
    }

    /// <summary>The chosen MonoGame target.</summary>
    public MonoGameTarget Target => IsWindowsDx ? MonoGameTarget.WindowsDX : MonoGameTarget.DesktopGL;

    /// <summary>True when the last live check allowed the folder.</summary>
    public bool OutputIsOk => OutputState == OutputCheckState.Ok;

    /// <summary>True when the folder has content and a run will ask the user first.</summary>
    public bool OutputNeedsConfirmation => OutputState == OutputCheckState.NeedsConfirmation;

    /// <summary>True when the folder cannot be used.</summary>
    public bool OutputIsRefused => OutputState == OutputCheckState.Refused;

    /// <summary>True while a run can be cancelled.</summary>
    public bool CanCancel => IsBusy;

    /// <summary>True when the plan item of this rule may be switched off.</summary>
    /// <param name="ruleId">The rule id, such as "XNA022".</param>
    public static bool IsExcludable(string ruleId) => _excludableRuleIds.Contains(ruleId);

    /// <inheritdoc />
    public override Task OnNavigatedToAsync()
    {
        var report = Session.Report;
        var source = Session.SourcePath;
        if (!ReferenceEquals(report, _loadedReport) || source != _loadedSource)
        {
            _loadedReport = report;
            _loadedSource = source;
            IsWindowsDx = MigrationDefaults.TargetFor(report) == MonoGameTarget.WindowsDX;
            EnableShims = false;
            SubstituteFonts = false;
            AllowWindowResize = false;
            FullscreenToggle = false;
            HighDpi = false;
            StatusMessage = null;
            ProgressText = string.Empty;
            OutputFolder = DefaultOutputFolder(source);
        }

        RefreshPlan();
        Validate();
        return Task.CompletedTask;
    }

    partial void OnOutputFolderChanged(string value) => Validate();

    partial void OnIsBusyChanged(bool value) => NotifyCommands();

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var picked = await Dialogs.PickFolderAsync("Choose the output folder");
        if (!string.IsNullOrWhiteSpace(picked))
        {
            OutputFolder = picked;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task PreviewAsync() => RunAsync(dryRun: true);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task MigrateAsync() => RunAsync(dryRun: false);

    /// <inheritdoc />
    public override Task ShutdownAsync()
    {
        _runCancellation?.Cancel();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void CancelRun() => _runCancellation?.Cancel();

    private bool CanRun() =>
        !IsBusy
        && Session.Report is not null
        && Session.SourcePath is not null
        && !string.IsNullOrWhiteSpace(OutputFolder)
        && OutputState != OutputCheckState.Refused;

    private async Task RunAsync(bool dryRun)
    {
        if (Session.Report is not { } report || Session.SourcePath is not { } source)
        {
            return;
        }

        var root = _migration.SourceRootOf(source);
        var folder = OutputFolder.Trim();
        Session.ClearError();
        StatusMessage = null;

        // The folder is checked again here, not only live, because the disk may have changed since the last keystroke.
        var verdict = _migration.CheckOutput(root, folder, false);
        var confirmed = false;
        if (!verdict.Allowed && verdict.Problem == GuardProblem.OutputNotEmpty)
        {
            var yes = await Dialogs.ConfirmAsync(
                "Use a folder that already has content?",
                $"The folder '{folder}' already has content. " + (dryRun
                    ? "The preview writes nothing, but the real migration will replace it. The old content is kept in a backup folder."
                    : "The migration will replace it. The old content is kept in a backup folder."),
                "Use this folder",
                "Choose another");
            if (!yes)
            {
                Validate();
                StatusMessage = "Nothing was done. Choose an empty or new folder, or answer yes to use this one.";
                return;
            }

            verdict = _migration.CheckOutput(root, folder, true);
            confirmed = true;
        }

        if (!verdict.Allowed)
        {
            Validate();
            return;
        }

        var excluded = PlanItems.Where(i => !i.IsIncluded).Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        var choices = new MigrationOptions(
            folder, Target, EnableShims, DryRun: false, excluded, SubstituteFonts,
            AllowWindowResize, FullscreenToggle, HighDpi);

        IsBusy = true;
        ProgressText = dryRun ? "Preparing the preview..." : "Starting the migration...";
        ProgressPercent = 0;
        ProgressIsIndeterminate = true;
        using var cancellation = new CancellationTokenSource();
        _runCancellation = cancellation;
        try
        {
            var progress = new Progress<MigrationProgress>(OnProgress);
            var result = await _migration.MigrateAsync(source, report, choices with { DryRun = dryRun }, confirmed, progress, cancellation.Token);

            // A preview must not replace the options of an earlier real migration: Build and run reads the output folder from them.
            if (!dryRun || Session.MigrationResult is null)
            {
                Session.MigrationOptions = choices;
            }

            if (dryRun)
            {
                Session.DryRunResult = result;
            }
            else
            {
                Session.MigrationResult = result;
                if (!result.Completed)
                {
                    Session.ShowError("The migration did not finish. The output folder was not replaced.");
                    return;
                }
            }

            Navigator.GoTo(WorkflowStep.Diff);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = dryRun ? "The preview was cancelled." : "The migration was cancelled. The output folder was not changed. A partly written copy stays in a separate folder that holds INCOMPLETE.txt and can be deleted.";
        }
        catch (OutputGuardException e)
        {
            Session.ShowError(e.Result.Message);
            Validate();
        }
        catch (Exception e)
        {
            Session.ShowError(dryRun ? "The preview failed." : "The migration failed.", e.ToString());
        }
        finally
        {
            _runCancellation = null;
            IsBusy = false;
            ProgressIsIndeterminate = false;
        }
    }

    private void OnProgress(MigrationProgress p)
    {
        if (!IsBusy)
        {
            return;
        }

        ProgressText = string.IsNullOrEmpty(p.Current) ? p.Stage : $"{p.Stage}: {p.Current}";
        ProgressIsIndeterminate = p.Total <= 0;
        ProgressPercent = p.Total > 0 ? Math.Clamp(100.0 * p.Completed / p.Total, 0, 100) : 0;
    }

    private void RefreshPlan()
    {
        PlanItems.Clear();
        if (Session.Report is { } report)
        {
            var excluded = Session.ExcludedPlanItemIds;
            var plan = _migration.CreatePlan(report, excluded);
            foreach (var item in plan.Items.Concat(plan.ExcludedItems).OrderBy(i => i.RuleId, StringComparer.Ordinal).ThenBy(i => i.Id, StringComparer.Ordinal))
            {
                var canExclude = IsExcludable(item.RuleId);
                PlanItems.Add(new PlanItemViewModel(item, canExclude, !(canExclude && excluded.Contains(item.Id)), OnItemToggled));
            }
        }

        UpdatePlanSummary();
    }

    private void OnItemToggled(PlanItemViewModel item, bool included)
    {
        Session.SetExcluded(item.Id, !included);
        UpdatePlanSummary();
    }

    private void UpdatePlanSummary()
    {
        PlanSummary = PlanItems.Count == 0
            ? "No automatic steps are needed."
            : $"{PlanItems.Count(i => i.IsIncluded)} of {PlanItems.Count} steps will run.";
        OnPropertyChanged(nameof(HasPlanItems));
    }

    private void Validate()
    {
        if (Session.SourcePath is not { } source || string.IsNullOrWhiteSpace(OutputFolder))
        {
            OutputState = OutputCheckState.Unknown;
            OutputMessage = "Choose a folder for the migrated copy.";
        }
        else
        {
            var verdict = _migration.CheckOutput(_migration.SourceRootOf(source), OutputFolder.Trim(), false);
            if (verdict.Allowed)
            {
                OutputState = OutputCheckState.Ok;
                OutputMessage = "OK: this folder can be used.";
            }
            else if (verdict.Problem == GuardProblem.OutputNotEmpty)
            {
                OutputState = OutputCheckState.NeedsConfirmation;
                OutputMessage = "Warning: " + verdict.Message + " You will be asked to confirm before it is used.";
            }
            else
            {
                OutputState = OutputCheckState.Refused;
                OutputMessage = "Problem: " + verdict.Message;
            }
        }

        NotifyCommands();
    }

    private void NotifyCommands()
    {
        PreviewCommand.NotifyCanExecuteChanged();
        MigrateCommand.NotifyCanExecuteChanged();
    }

    private string DefaultOutputFolder(string? source)
    {
        if (source is null)
        {
            return string.Empty;
        }

        var root = Path.TrimEndingDirectorySeparator(_migration.SourceRootOf(source));
        var parent = Path.GetDirectoryName(root);
        var name = Path.GetFileName(root);
        return string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name) ? string.Empty : Path.Combine(parent, name + "-monogame");
    }
}
