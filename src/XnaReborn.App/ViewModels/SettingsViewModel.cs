using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XnaReborn.App.Services;
using XnaReborn.Migrator;
using XnaReborn.Runner.Publish;

namespace XnaReborn.App.ViewModels;

/// <summary>
/// The "Settings" page. Each change is saved at once through <see cref="ISettingsService"/>. The page only edits defaults:
/// Migration setup and Build and run read them when a project is opened, and a choice made there for one run still wins.
/// There is no setting here that skips the launch question, overwrites an output folder without asking, or uploads anything.
/// </summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    private static readonly HashSet<string> _saved = new(StringComparer.Ordinal)
    {
        nameof(SelectedTheme), nameof(SelectedMotion), nameof(SelectedTarget), nameof(EnableShims), nameof(SubstituteFonts),
        nameof(AllowWindowResize), nameof(FullscreenToggle), nameof(HighDpi), nameof(OutputParentFolder), nameof(SelectedPublishPlatform),
        nameof(KeepRecentProjects), nameof(MaxRecentProjects), nameof(ShowHints), nameof(AutoFollowLog), nameof(CheckSdkOnStart),
    };

    private readonly ISettingsService _settings;
    private readonly IRecentProjectsStore _recent;
    private bool _loading;

    [ObservableProperty]
    private SettingChoice<ThemeChoice> _selectedTheme = ThemeChoices[0];

    [ObservableProperty]
    private SettingChoice<MotionChoice> _selectedMotion = MotionChoices[0];

    [ObservableProperty]
    private SettingChoice<MonoGameTarget> _selectedTarget = TargetChoices[0];

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
    [NotifyPropertyChangedFor(nameof(HasOutputParentFolder), nameof(OutputParentText))]
    [NotifyCanExecuteChangedFor(nameof(ClearOutputParentCommand))]
    private string? _outputParentFolder;

    [ObservableProperty]
    private SettingChoice<string> _selectedPublishPlatform = PublishChoices[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSetMaxRecent))]
    private bool _keepRecentProjects = true;

    [ObservableProperty]
    private int _maxRecentProjects = AppSettings.MaxRecentLimit;

    [ObservableProperty]
    private bool _showHints = true;

    [ObservableProperty]
    private bool _autoFollowLog = true;

    [ObservableProperty]
    private bool _checkSdkOnStart = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    private string? _notice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    private string? _problem;

    /// <summary>Creates the page.</summary>
    /// <param name="session">The shared workflow data.</param>
    /// <param name="navigator">Moves to other pages.</param>
    /// <param name="dialogs">File pickers and message dialogs.</param>
    /// <param name="settings">Holds and saves the settings.</param>
    /// <param name="recent">Forgets the recent projects when the user asks.</param>
    public SettingsViewModel(WorkflowSession session, INavigator navigator, IDialogService dialogs, ISettingsService settings, IRecentProjectsStore recent)
        : base("Settings", session, navigator, dialogs)
    {
        _settings = settings;
        _recent = recent;
        Load(settings.Current);
        Problem = settings.LoadProblem;

        // The rail's theme toggle changes the same setting, so the page follows it.
        settings.Changed += (_, _) => Load(_settings.Current);
    }

    /// <summary>The entries of the theme drop-down.</summary>
    public static IReadOnlyList<SettingChoice<ThemeChoice>> ThemeChoices { get; } =
    [
        new(ThemeChoice.System, "System"),
        new(ThemeChoice.Light, "Light"),
        new(ThemeChoice.Dark, "Dark"),
    ];

    /// <summary>The entries of the reduce motion drop-down.</summary>
    public static IReadOnlyList<SettingChoice<MotionChoice>> MotionChoices { get; } =
    [
        new(MotionChoice.System, "System"),
        new(MotionChoice.On, "On (skip animations)"),
        new(MotionChoice.Off, "Off (play animations)"),
    ];

    /// <summary>The entries of the default target drop-down.</summary>
    public static IReadOnlyList<SettingChoice<MonoGameTarget>> TargetChoices { get; } =
    [
        new(MonoGameTarget.DesktopGL, "DesktopGL (Windows, Linux, macOS)"),
        new(MonoGameTarget.WindowsDX, "WindowsDX (Windows only)"),
    ];

    /// <summary>The entries of the default publish platform drop-down.</summary>
    public static IReadOnlyList<SettingChoice<string>> PublishChoices { get; } =
        [.. PublishTarget.All.Select(t => new SettingChoice<string>(t.Rid, t.DisplayName))];

    /// <summary>The entries of the maximum recent projects drop-down.</summary>
    public static IReadOnlyList<int> RecentCounts { get; } = [.. Enumerable.Range(1, AppSettings.MaxRecentLimit)];

    /// <summary>Always shown: nothing leaves this computer.</summary>
    public string PrivacyNote => "AI repair is off. Nothing is uploaded.";

    /// <summary>Always shown: the questions that cannot be switched off.</summary>
    public string SafetyNote =>
        "The app always asks before it starts a game and before it uses an output folder that already has content. Your original project is never changed.";

    /// <summary>True when a default output parent folder is set.</summary>
    public bool HasOutputParentFolder => !string.IsNullOrEmpty(OutputParentFolder);

    /// <summary>The default output parent folder in words.</summary>
    public string OutputParentText => OutputParentFolder ?? "Not set. Each copy is placed next to its original project.";

    /// <summary>True when the maximum number of recent projects can be changed.</summary>
    public bool CanSetMaxRecent => KeepRecentProjects;

    /// <summary>True when <see cref="Notice"/> has text.</summary>
    public bool HasNotice => !string.IsNullOrEmpty(Notice);

    /// <summary>True when <see cref="Problem"/> has text.</summary>
    public bool HasProblem => !string.IsNullOrEmpty(Problem);

    /// <inheritdoc />
    public override Task OnNavigatedToAsync()
    {
        Load(_settings.Current);
        Notice = null;
        Problem = _settings.LoadProblem;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_loading && e.PropertyName is { } name && _saved.Contains(name))
        {
            Persist();
        }
    }

    [RelayCommand]
    private async Task BrowseOutputParentAsync()
    {
        var picked = await Dialogs.PickFolderAsync("Choose the folder for migrated copies");
        if (!string.IsNullOrWhiteSpace(picked))
        {
            OutputParentFolder = picked;
        }
    }

    [RelayCommand(CanExecute = nameof(HasOutputParentFolder))]
    private void ClearOutputParent() => OutputParentFolder = null;

    [RelayCommand]
    private void ClearRecent()
    {
        _recent.Clear();
        Notice = "Recent projects cleared. The projects themselves were not touched.";
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        Persist(new AppSettings());
        Notice = "Settings are back to their defaults.";
    }

    [RelayCommand]
    private void Back() => Navigator.CloseSettings();

    private void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            SelectedTheme = ThemeChoices.First(c => c.Value == s.Theme);
            SelectedMotion = MotionChoices.First(c => c.Value == s.ReduceMotion);
            SelectedTarget = TargetChoices.First(c => c.Value == s.DefaultTarget);
            EnableShims = s.EnableShims;
            SubstituteFonts = s.SubstituteFonts;
            AllowWindowResize = s.AllowWindowResize;
            FullscreenToggle = s.FullscreenToggle;
            HighDpi = s.HighDpi;
            OutputParentFolder = s.OutputParentFolder;
            SelectedPublishPlatform = PublishChoices.First(c => c.Value == s.PublishRid);
            KeepRecentProjects = s.KeepRecentProjects;
            MaxRecentProjects = s.MaxRecentProjects;
            ShowHints = s.ShowHints;
            AutoFollowLog = s.AutoFollowLog;
            CheckSdkOnStart = s.CheckSdkOnStart;
        }
        finally
        {
            _loading = false;
        }
    }

    private void Persist() => Persist(_settings.Current with
    {
        Theme = SelectedTheme.Value,
        ReduceMotion = SelectedMotion.Value,
        DefaultTarget = SelectedTarget.Value,
        EnableShims = EnableShims,
        SubstituteFonts = SubstituteFonts,
        AllowWindowResize = AllowWindowResize,
        FullscreenToggle = FullscreenToggle,
        HighDpi = HighDpi,
        OutputParentFolder = OutputParentFolder,
        PublishRid = SelectedPublishPlatform.Value,
        KeepRecentProjects = KeepRecentProjects,
        MaxRecentProjects = MaxRecentProjects,
        ShowHints = ShowHints,
        AutoFollowLog = AutoFollowLog,
        CheckSdkOnStart = CheckSdkOnStart,
    });

    private void Persist(AppSettings next)
    {
        Notice = null;
        Problem = _settings.Save(next)
            ? null
            : "The settings could not be saved. They stay in effect until the app closes.";
    }
}
