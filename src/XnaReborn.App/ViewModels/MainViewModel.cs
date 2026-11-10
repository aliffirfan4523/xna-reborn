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
    private readonly Dictionary<WorkflowStep, PageViewModel> _pages = [];

    [ObservableProperty]
    private WorkflowStep _currentStep = WorkflowStep.Open;

    [ObservableProperty]
    private bool _isDetailsExpanded;

    /// <summary>Creates the main view model. Page view models are resolved from <paramref name="services"/> when first needed.</summary>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="clipboard">Used by the Copy button of the error banner.</param>
    /// <param name="services">Resolves the page view models. Not used in the constructor, because pages depend on <see cref="INavigator"/>.</param>
    public MainViewModel(WorkflowSession session, IClipboardService clipboard, IServiceProvider services)
    {
        Session = session;
        _clipboard = clipboard;
        _services = services;
        Steps = _definitions.Select((d, i) => new StepItem(d.Step, d.Label, i + 1, GoTo)).ToList();
        RefreshSteps();
        session.PropertyChanged += OnSessionChanged;
    }

    /// <summary>The shared workflow data.</summary>
    public WorkflowSession Session { get; }

    /// <summary>The stepper buttons, in order. The items change in place, the list does not.</summary>
    public IReadOnlyList<StepItem> Steps { get; }

    /// <summary>The page view model for <see cref="CurrentStep"/>. Resolved from the container once and then kept.</summary>
    public PageViewModel CurrentPage => GetPage(CurrentStep);

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
        CurrentStep = step;
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

    partial void OnCurrentStepChanged(WorkflowStep value)
    {
        OnPropertyChanged(nameof(CurrentPage));
        RefreshSteps();
    }

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
            item.Update(item.Step == CurrentStep, CanGoTo(item.Step), item.Step < CurrentStep);
        }

        GoToCommand.NotifyCanExecuteChanged();
    }
}
