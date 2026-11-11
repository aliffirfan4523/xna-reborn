using System.Reflection;
using XnaReborn.App.Services;
using XnaReborn.App.ViewModels;
using XnaReborn.Migrator;

namespace XnaReborn.App.Tests.Settings;

public sealed class SettingsViewModelTests
{
    private static async Task<SettingsViewModel> OpenAsync(TestServices t)
    {
        await t.Main.OpenSettingsAsync();
        return t.Get<SettingsViewModel>();
    }

    [Fact]
    public async Task The_page_shows_the_saved_settings_when_it_opens()
    {
        using var t = TestServices.Create();
        t.Settings.Current = new AppSettings
        {
            Theme = ThemeChoice.Dark,
            ReduceMotion = MotionChoice.Off,
            DefaultTarget = MonoGameTarget.WindowsDX,
            EnableShims = true,
            OutputParentFolder = "C:/copies",
            PublishRid = "osx-arm64",
            KeepRecentProjects = false,
            MaxRecentProjects = 4,
            ShowHints = false,
        };

        var vm = await OpenAsync(t);

        Assert.Equal(ThemeChoice.Dark, vm.SelectedTheme.Value);
        Assert.Equal(MotionChoice.Off, vm.SelectedMotion.Value);
        Assert.Equal(MonoGameTarget.WindowsDX, vm.SelectedTarget.Value);
        Assert.True(vm.EnableShims);
        Assert.Equal("C:/copies", vm.OutputParentFolder);
        Assert.Equal("osx-arm64", vm.SelectedPublishPlatform.Value);
        Assert.False(vm.KeepRecentProjects);
        Assert.False(vm.CanSetMaxRecent);
        Assert.Equal(4, vm.MaxRecentProjects);
        Assert.False(vm.ShowHints);
        Assert.Equal(0, t.Settings.SaveCount);
    }

    [Fact]
    public async Task Every_change_is_saved_at_once()
    {
        using var t = TestServices.Create();
        var vm = await OpenAsync(t);

        vm.SelectedTheme = SettingsViewModel.ThemeChoices.Single(c => c.Value == ThemeChoice.Light);
        vm.SelectedMotion = SettingsViewModel.MotionChoices.Single(c => c.Value == MotionChoice.On);
        vm.SelectedTarget = SettingsViewModel.TargetChoices.Single(c => c.Value == MonoGameTarget.WindowsDX);
        vm.EnableShims = true;
        vm.SubstituteFonts = true;
        vm.AllowWindowResize = true;
        vm.FullscreenToggle = true;
        vm.HighDpi = true;
        vm.SelectedPublishPlatform = SettingsViewModel.PublishChoices.Single(c => c.Value == "linux-x64");
        vm.KeepRecentProjects = false;
        vm.MaxRecentProjects = 2;
        vm.ShowHints = false;
        vm.AutoFollowLog = false;
        vm.CheckSdkOnStart = false;

        var saved = t.Settings.Current;
        Assert.Equal(ThemeChoice.Light, saved.Theme);
        Assert.Equal(MotionChoice.On, saved.ReduceMotion);
        Assert.Equal(MonoGameTarget.WindowsDX, saved.DefaultTarget);
        Assert.True(saved.EnableShims && saved.SubstituteFonts && saved.AllowWindowResize && saved.FullscreenToggle && saved.HighDpi);
        Assert.Equal("linux-x64", saved.PublishRid);
        Assert.False(saved.KeepRecentProjects);
        Assert.Equal(2, saved.MaxRecentProjects);
        Assert.False(saved.ShowHints || saved.AutoFollowLog || saved.CheckSdkOnStart);
        Assert.Equal(14, t.Settings.SaveCount);
    }

    [Fact]
    public async Task The_rail_theme_toggle_and_the_page_stay_in_sync_both_ways()
    {
        using var t = TestServices.Create();
        var vm = await OpenAsync(t);

        t.Main.CycleThemeCommand.Execute(null);

        Assert.Equal(ThemeChoice.Light, t.Settings.Current.Theme);
        Assert.Equal(ThemeChoice.Light, vm.SelectedTheme.Value);
        Assert.Equal("Theme: Light", t.Main.ThemeText);

        vm.SelectedTheme = SettingsViewModel.ThemeChoices.Single(c => c.Value == ThemeChoice.Dark);

        Assert.Equal("Theme: Dark", t.Main.ThemeText);
        t.Main.CycleThemeCommand.Execute(null);
        Assert.Equal(ThemeChoice.System, vm.SelectedTheme.Value);
        Assert.Equal("Theme: System", t.Main.ThemeText);
    }

    [Fact]
    public async Task The_output_parent_folder_is_picked_and_cleared()
    {
        using var t = TestServices.Create();
        var vm = await OpenAsync(t);
        Assert.False(vm.ClearOutputParentCommand.CanExecute(null));

        t.Dialogs.FolderToPick = "C:/copies";
        await vm.BrowseOutputParentCommand.ExecuteAsync(null);

        Assert.Equal("C:/copies", t.Settings.Current.OutputParentFolder);
        Assert.Equal("C:/copies", vm.OutputParentText);
        Assert.True(vm.ClearOutputParentCommand.CanExecute(null));

        t.Dialogs.FolderToPick = null;
        await vm.BrowseOutputParentCommand.ExecuteAsync(null);
        Assert.Equal("C:/copies", t.Settings.Current.OutputParentFolder);

        vm.ClearOutputParentCommand.Execute(null);

        Assert.Null(t.Settings.Current.OutputParentFolder);
        Assert.Contains("next to its original", vm.OutputParentText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clear_recent_forgets_the_list_and_leaves_the_settings_alone()
    {
        using var t = TestServices.Create();
        t.Recent.Add("C:/projects/Old");
        var vm = await OpenAsync(t);
        var saves = t.Settings.SaveCount;

        vm.ClearRecentCommand.Execute(null);

        Assert.Empty(t.Recent.Load());
        Assert.True(vm.HasNotice);
        Assert.Equal(saves, t.Settings.SaveCount);
    }

    [Fact]
    public async Task Reset_puts_every_default_back()
    {
        using var t = TestServices.Create();
        t.Settings.Current = new AppSettings { Theme = ThemeChoice.Dark, EnableShims = true, MaxRecentProjects = 2, OutputParentFolder = "C:/x" };
        var vm = await OpenAsync(t);

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Equal(new AppSettings(), t.Settings.Current);
        Assert.Equal(ThemeChoice.System, vm.SelectedTheme.Value);
        Assert.False(vm.EnableShims);
        Assert.Equal(AppSettings.MaxRecentLimit, vm.MaxRecentProjects);
        Assert.Null(vm.OutputParentFolder);
        Assert.True(vm.HasNotice);
    }

    [Fact]
    public async Task A_save_that_fails_is_shown_and_a_load_problem_is_shown_on_open()
    {
        using var t = TestServices.Create();
        t.Settings.LoadProblem = "The settings file could not be read, so the defaults are in use.";
        var vm = await OpenAsync(t);
        Assert.True(vm.HasProblem);

        t.Settings.SaveSucceeds = false;
        vm.EnableShims = true;

        Assert.Contains("could not be saved", vm.Problem, StringComparison.Ordinal);
        Assert.True(t.Settings.Current.EnableShims);

        t.Settings.SaveSucceeds = true;
        vm.EnableShims = false;
        Assert.False(vm.HasProblem);
    }

    [Fact]
    public async Task Back_returns_to_the_screen_that_was_open_before()
    {
        using var t = TestServices.Create();
        await t.OpenPageAsync(WorkflowStep.Report);
        var report = t.Main.CurrentPage;

        var vm = await OpenAsync(t);
        Assert.Same(vm, t.Main.CurrentPage);

        vm.BackCommand.Execute(null);

        Assert.False(t.Main.IsSettingsOpen);
        Assert.Same(report, t.Main.CurrentPage);
        Assert.Equal(WorkflowStep.Report, t.Main.CurrentStep);
    }

    [Fact]
    public async Task The_page_always_says_nothing_is_uploaded()
    {
        using var t = TestServices.Create();
        var vm = await OpenAsync(t);

        Assert.Equal("AI repair is off. Nothing is uploaded.", vm.PrivacyNote);
        Assert.Contains("always asks before it starts a game", vm.SafetyNote, StringComparison.Ordinal);
    }

    [Fact]
    public void No_setting_can_skip_a_question_overwrite_a_folder_or_upload()
    {
        string[] forbidden = ["skip", "overwrite", "upload", "send", "telemetry", "confirm", "ai", "repair", "silent"];
        var names = typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name)
            .Concat(typeof(SettingsViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(bool) || p.PropertyType.IsGenericType).Select(p => p.Name))
            .ToList();

        foreach (var name in names)
        {
            var words = System.Text.RegularExpressions.Regex.Split(name, "(?<=[a-z])(?=[A-Z])").Select(w => w.ToLowerInvariant());
            Assert.True(!words.Intersect(forbidden).Any(), "Setting name suggests a safety bypass: " + name);
        }
    }
}
