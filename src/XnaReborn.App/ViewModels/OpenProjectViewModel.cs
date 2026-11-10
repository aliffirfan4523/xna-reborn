using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XnaReborn.App.Services;
using XnaReborn.Core.Models;

namespace XnaReborn.App.ViewModels;

/// <summary>The "Open a project" page: pick or drop a folder, .sln or .csproj, analyze it, then go to the report.</summary>
public sealed partial class OpenProjectViewModel : PageViewModel
{
    private readonly IAnalysisService _analysis;
    private readonly IRecentProjectsStore _recentProjects;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private bool _isProgressKnown;

    [ObservableProperty]
    private string? _message;

    /// <summary>Creates the page.</summary>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="navigator">Moves to other pages.</param>
    /// <param name="dialogs">File pickers and message dialogs.</param>
    /// <param name="analysis">Analyzes the chosen project.</param>
    /// <param name="recentProjects">Remembers opened projects.</param>
    public OpenProjectViewModel(WorkflowSession session, INavigator navigator, IDialogService dialogs, IAnalysisService analysis, IRecentProjectsStore recentProjects)
        : base("Open a project", session, navigator, dialogs)
    {
        _analysis = analysis;
        _recentProjects = recentProjects;
    }

    /// <summary>Recently opened projects, most recent first.</summary>
    public ObservableCollection<RecentProject> RecentProjects { get; } = [];

    /// <summary>True when no analysis is running.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>True when there is at least one recent project.</summary>
    public bool HasRecentProjects => RecentProjects.Count > 0;

    /// <inheritdoc />
    public override Task OnNavigatedToAsync()
    {
        RefreshRecent();
        return Task.CompletedTask;
    }

    /// <summary>Analyzes <paramref name="path"/> (a folder, .sln or .csproj). On success stores it, sets the session and goes to the report.</summary>
    /// <param name="path">The path to open.</param>
    /// <returns>True when the report page was opened.</returns>
    public async Task<bool> OpenPathAsync(string? path)
    {
        if (IsBusy)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        Session.ClearError();
        Message = null;
        var problem = CheckPath(path);
        if (problem is not null)
        {
            Fail(problem, path);
            return false;
        }

        using var cts = new CancellationTokenSource();
        _cts = cts;
        IsBusy = true;
        IsProgressKnown = false;
        ProgressText = "Starting analysis...";
        try
        {
            var report = await _analysis.AnalyzeAsync(path, new Progress<AnalysisProgress>(ShowProgress), cts.Token);
            if (report.NoProjectReason is { } reason)
            {
                Fail("No XNA project was found. " + reason, path);
                return false;
            }

            Session.SourcePath = path;
            Session.Report = report;
            _recentProjects.Add(path);
            RefreshRecent();
            Navigator.GoTo(WorkflowStep.Report);
            return true;
        }
        catch (OperationCanceledException)
        {
            Message = "Analysis canceled.";
            return false;
        }
        catch (Exception ex)
        {
            Fail("The project could not be analyzed: " + ex.Message, ex.ToString());
            return false;
        }
        finally
        {
            IsBusy = false;
            ProgressText = string.Empty;
            _cts = null;
        }
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        await OpenPathAsync(await Dialogs.PickFolderAsync("Choose an XNA project folder"));
    }

    [RelayCommand]
    private async Task BrowseFileAsync()
    {
        await OpenPathAsync(await Dialogs.PickProjectFileAsync());
    }

    [RelayCommand]
    private Task OpenRecentAsync(RecentProject? project) => project is null ? Task.CompletedTask : OpenPathAsync(project.Path);

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    private static string? CheckPath(string path)
    {
        if (Directory.Exists(path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            return "That path does not exist anymore. Choose another folder or file.";
        }

        var extension = Path.GetExtension(path);
        return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            ? null
            : "Choose a folder, a .sln file or a .csproj file.";
    }

    private void Fail(string message, string details)
    {
        Message = message;
        Session.ShowError(message, details);
    }

    private void ShowProgress(AnalysisProgress p)
    {
        IsProgressKnown = p.Total > 0;
        ProgressPercent = p.Total > 0 ? 100.0 * p.Completed / p.Total : 0;
        ProgressText = p.Current is null ? p.Stage : p.Stage + ": " + p.Current;
    }

    private void RefreshRecent()
    {
        RecentProjects.Clear();
        foreach (var item in _recentProjects.Load())
        {
            RecentProjects.Add(item);
        }

        OnPropertyChanged(nameof(HasRecentProjects));
    }
}
