using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Services;

namespace XnaReborn.App.ViewModels;

/// <summary>Owns the stepper, the current page and the error banner. Also the app's <see cref="INavigator"/>.</summary>
public sealed partial class MainViewModel : ViewModelBase, INavigator
{
    private static readonly (WorkflowStep Step, string Label, Type Page)[] _definitions =
    [
        (WorkflowStep.Open, "Open", typeof(OpenProjectViewModel)),
        (WorkflowStep.Report, "Report", typeof(ReportViewModel)),
        (WorkflowStep.MigrationSetup, "Migration setup", typeof(MigrationSetupViewModel)),
        (WorkflowStep.Diff, "Review changes", typeof(DiffViewModel)),
        (WorkflowStep.BuildRun, "Build and run", typeof(BuildRunViewModel)),
        (WorkflowStep.Summary, "Summary", typeof(SummaryViewModel)),
    ];

    private readonly IClipboardService _clipboard;
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settings;
    private readonly Dictionary<WorkflowStep, PageViewModel> _pages = [];
    private SettingsViewModel? _settingsPage;

    [ObservableProperty]
    private WorkflowStep _currentStep = WorkflowStep.Open;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage), nameof(StatusText), nameof(SettingsAutomationName))]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private bool _isDetailsExpanded;

    /// <summary>Creates the main view model. Page view models are resolved from <paramref name="services"/> when first needed.</summary>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="clipboard">Used by the Copy button of the error banner.</param>
    /// <param name="services">Resolves the page view models. Not used in the constructor, because pages depend on <see cref="INavigator"/>.</param>
    /// <param name="settings">The saved settings. The theme toggle of the rail reads and writes the theme.</param>
    public MainViewModel(WorkflowSession session, IClipboardService clipboard, IServiceProvider services, ISettingsService settings)
    {
        Session = session;
        _clipboard = clipboard;
        _services = services;
        _settings = settings;
        settings.Changed += (_, _) => OnPropertyChanged(nameof(ThemeText));
        Steps = _definitions.Select((d, i) => new StepItem(d.Step, d.Label, i + 1, GoTo)).ToList();
        RefreshSteps();
        session.PropertyChanged += OnSessionChanged;
    }

    /// <summary>The shared workflow data.</summary>
    public WorkflowSession Session { get; }

    /// <summary>The stepper buttons, in order. The items change in place, the list does not.</summary>
    public IReadOnlyList<StepItem> Steps { get; }

    /// <summary>The saved settings. The window listens to <see cref="ISettingsService.Changed"/> to apply the theme and motion choice.</summary>
    public ISettingsService Settings => _settings;

    /// <summary>The text of the theme toggle in the rail, such as "Theme: System".</summary>
    public string ThemeText => "Theme: " + _settings.Current.Theme;

    /// <summary>Screen reader text of the Settings button of the rail.</summary>
    public string SettingsAutomationName => IsSettingsOpen ? "Settings, current" : "Settings";

    /// <summary>The Settings page while it is open, otherwise the page view model for <see cref="CurrentStep"/>. Resolved from the container once and then kept.</summary>
    public PageViewModel CurrentPage => IsSettingsOpen ? GetSettingsPage() : GetPage(CurrentStep);

    /// <summary>Name of the opened project (folder or file name without extension), or null before a project is opened.</summary>
    public string? ProjectName => Session.SourcePath is { Length: > 0 } path
        ? Path.GetFileNameWithoutExtension(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        : null;

    /// <summary>True when a project is open, so the header can show its name.</summary>
    public bool HasProject => !string.IsNullOrEmpty(ProjectName);

    /// <summary>True when the analysis has a score to show in the header.</summary>
    public bool HasScore => Session.Report is { NoProjectReason: null };

    /// <summary>The compatibility score text for the header chip, such as "Score 72".</summary>
    public string ScoreText => $"Score {Session.Report?.Score ?? 0}";

    /// <summary>The text of the status bar: where the user is in the flow.</summary>
    public string StatusText => IsSettingsOpen ? "Settings" : $"Step {Steps.Single(s => s.IsCurrent).Number} of {Steps.Count}: {Steps.Single(s => s.IsCurrent).Label}";

    /// <summary>The short error message, or null.</summary>
    public string? ErrorMessage => Session.ErrorMessage;

    /// <summary>The technical details of the error, or null.</summary>
    public string? ErrorDetails => Session.ErrorDetails;

    /// <summary>True while the error banner is visible.</summary>
    public bool HasError => Session.HasError;

    /// <summary>True when the error has details to expand.</summary>
    public bool HasErrorDetails => !string.IsNullOrWhiteSpace(Session.ErrorDetails);

    /// <inheritdoc />
    public bool CanGoTo(WorkflowStep step)
    {
        // Going back is always allowed, so the user is never stuck on a page.
        if (step <= CurrentStep)
        {
            return true;
        }

        return step switch
        {
            WorkflowStep.Open => true,
            WorkflowStep.Report or WorkflowStep.MigrationSetup => Session.Report is not null,
            WorkflowStep.Diff => Session.DryRunResult is not null || Session.MigrationResult is not null,
            WorkflowStep.BuildRun => Session.MigrationResult is { Completed: true },
            WorkflowStep.Summary => Session.MigrationResult is not null,
            _ => false,
        };
    }

    /// <inheritdoc />
    [RelayCommand(CanExecute = nameof(CanGoTo))]
    public void GoTo(WorkflowStep step) => _ = GoToAsync(step);

    /// <summary>
    /// Like <see cref="GoTo"/> but completes after the page's <see cref="PageViewModel.OnNavigatedToAsync"/> finished.
    /// A failure in that method is shown in the error banner and does not fault the task.
    /// </summary>
    /// <param name="step">The screen to show.</param>
    public async Task GoToAsync(WorkflowStep step)
    {
        if (!CanGoTo(step))
        {
            return;
        }

        var page = GetPage(step);

        // The step is set while Settings may still be showing, so the page changes once, when Settings closes.
        CurrentStep = step;
        IsSettingsOpen = false;
        await RefreshPageAsync(page).ConfigureAwait(true);
    }

    /// <summary>Shows the Settings page. The workflow step and the session stay as they are.</summary>
    [RelayCommand]
    public async Task OpenSettingsAsync()
    {
        if (IsSettingsOpen)
        {
            return;
        }

        var page = GetSettingsPage();
        IsSettingsOpen = true;
        await RefreshPageAsync(page).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public void CloseSettings() => IsSettingsOpen = false;

    [RelayCommand]
    private void CycleTheme()
    {
        var next = _settings.Current.Theme switch
        {
            ThemeChoice.System => ThemeChoice.Light,
            ThemeChoice.Light => ThemeChoice.Dark,
            _ => ThemeChoice.System,
        };
        _settings.Save(_settings.Current with { Theme = next });
    }

    private async Task RefreshPageAsync(PageViewModel page)
    {
        try
        {
            await page.OnNavigatedToAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The page cancelled its own refresh. Nothing to report.
        }
        catch (Exception ex)
        {
            // A page that fails to refresh must not crash the app: show it in the banner instead.
            Session.ShowError($"The \"{page.Title}\" page could not be refreshed.", ex.ToString());
        }
    }

    /// <summary>
    /// Cancels running operations and stops a game started from the Build and run page. Call it when the window closes.
    /// The returned task is already completed when nothing is running.
    /// </summary>
    public Task ShutdownAsync() => Task.WhenAll(_pages.Values.Select(p => p.ShutdownAsync()));

    [RelayCommand]
    private void Dismiss() => Session.ClearError();

    [RelayCommand]
    private async Task CopyDetailsAsync()
    {
        if (Session.ErrorMessage is not { } message)
        {
            return;
        }

        var text = HasErrorDetails ? message + Environment.NewLine + Environment.NewLine + Session.ErrorDetails : message;
        try
        {
            await _clipboard.SetTextAsync(text).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // The clipboard can be locked by another program. Keep the original error and add this one.
            Session.ShowError(message, Session.ErrorDetails + Environment.NewLine + "Copy failed: " + ex.Message);
        }
    }

    partial void OnIsSettingsOpenChanged(bool value) => RefreshSteps();

    partial void OnCurrentStepChanged(WorkflowStep value)
    {
        if (!IsSettingsOpen)
        {
            OnPropertyChanged(nameof(CurrentPage));
        }

        RefreshSteps();
        OnPropertyChanged(nameof(StatusText));
    }

    private SettingsViewModel GetSettingsPage() => _settingsPage ??= _services.GetRequiredService<SettingsViewModel>();

    private PageViewModel GetPage(WorkflowStep step)
    {
        if (!_pages.TryGetValue(step, out var page))
        {
            var type = _definitions.Single(d => d.Step == step).Page;
            page = (PageViewModel)_services.GetRequiredService(type);
            _pages[step] = page;
        }

        return page;
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshSteps();
        if (e.PropertyName is nameof(WorkflowSession.SourcePath) or nameof(WorkflowSession.Report))
        {
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(HasProject));
            OnPropertyChanged(nameof(HasScore));
            OnPropertyChanged(nameof(ScoreText));
        }

        if (e.PropertyName is nameof(WorkflowSession.ErrorMessage) or nameof(WorkflowSession.ErrorDetails))
        {
            IsDetailsExpanded = false;
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(ErrorDetails));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(HasErrorDetails));
        }
    }

    private void RefreshSteps()
    {
        foreach (var item in Steps)
        {
            item.Update(!IsSettingsOpen && item.Step == CurrentStep, CanGoTo(item.Step), item.Step < CurrentStep);
        }

        GoToCommand.NotifyCanExecuteChanged();
    }
}
