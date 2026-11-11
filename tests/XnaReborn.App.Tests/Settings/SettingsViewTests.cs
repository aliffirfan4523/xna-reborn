using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.Controls;
using XnaReborn.App.Services;
using XnaReborn.App.Tests.Accessibility;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;

namespace XnaReborn.App.Tests.Settings;

/// <summary>The Settings page in a headless window: the rail button, the shortcuts, the tab order, names, and the theme and motion it applies.</summary>
public sealed class SettingsViewTests
{
    private static MainWindow Show(TestServices t)
    {
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static List<string> PageStops(MainWindow window) =>
        AccessibilityProbe.TabOrder(window)
            .Select(AccessibilityProbe.Label)
            .Where(l => !l.StartsWith("Step_", StringComparison.Ordinal) && l is not ("ThemeToggle" or "Nav_Settings"))
            .ToList();

    [AvaloniaFact]
    public async Task Ctrl_7_and_Ctrl_comma_open_the_Settings_page()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Open);

        window.KeyPressQwerty(PhysicalKey.Digit7, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.True(t.Main.IsSettingsOpen);
        Assert.IsType<SettingsViewModel>(t.Main.CurrentPage);

        t.Main.CloseSettings();
        Assert.False(t.Main.IsSettingsOpen);

        window.KeyPressQwerty(PhysicalKey.Comma, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.True(t.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    public async Task Ctrl_1_to_6_jump_to_a_step_and_leave_Settings()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.MigrationSetup);
        await t.Main.OpenSettingsAsync();

        window.KeyPressQwerty(PhysicalKey.Digit2, RawInputModifiers.Control);
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();

        Assert.False(t.Main.IsSettingsOpen);
        Assert.Equal(WorkflowStep.Report, t.Main.CurrentStep);

        window.KeyPressQwerty(PhysicalKey.Digit1, RawInputModifiers.Control);
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(WorkflowStep.Open, t.Main.CurrentStep);
    }

    [AvaloniaFact]
    public async Task The_gear_button_opens_Settings_and_a_step_button_leaves_it()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Report);
        var gear = window.FindControl<Button>("SettingsButton")!;
        Assert.Equal("Settings", AutomationProperties.GetName(gear));
        Assert.Equal("Nav_Settings", AutomationProperties.GetAutomationId(gear));

        gear.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Settings, current", AutomationProperties.GetName(gear));
        Assert.Equal("Settings", t.Main.StatusText);
        Assert.DoesNotContain(t.Main.Steps, s => s.IsCurrent);
        Assert.Single(window.GetVisualDescendants().OfType<SettingsView>());

        t.Main.Steps.Single(s => s.Step == WorkflowStep.Report).GoCommand.Execute(null);
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();

        Assert.False(t.Main.IsSettingsOpen);
        Assert.Equal("Settings", AutomationProperties.GetName(gear));
        Assert.Equal("Step 2 of 6: Report", t.Main.StatusText);
        Assert.Empty(window.GetVisualDescendants().OfType<SettingsView>());
    }

    [AvaloniaFact]
    public async Task A_locked_step_stays_locked_while_Settings_is_open()
    {
        using var t = TestServices.Create();
        await t.OpenPageAsync(WorkflowStep.Open);
        await t.Main.OpenSettingsAsync();

        Assert.False(t.Main.CanGoTo(WorkflowStep.Diff));
        await t.Main.GoToAsync(WorkflowStep.Diff);

        Assert.True(t.Main.IsSettingsOpen);
        Assert.Equal(WorkflowStep.Open, t.Main.CurrentStep);
    }

    [AvaloniaFact]
    public async Task Settings_tab_stops_follow_the_page_from_top_to_bottom_and_every_control_is_named()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        await t.Main.OpenSettingsAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            [
                "SettingTheme", "SettingMotion", "SettingTarget", "SettingShims", "SettingFonts", "SettingResize", "SettingFullscreen",
                "SettingHighDpi", "SettingOutputParent", "SettingOutputParentBrowse", "SettingPublishPlatform", "SettingHints",
                "SettingFollowLog", "SettingCheckSdk", "SettingKeepRecent", "SettingMaxRecent", "SettingClearRecent", "SettingsBack", "SettingsReset",
            ],
            PageStops(window));

        var unnamed = AccessibilityProbe.InteractiveControls(window)
            .Where(c => string.IsNullOrWhiteSpace(AutomationProperties.GetName(c)))
            .Select(AccessibilityProbe.Describe)
            .ToList();
        Assert.True(unnamed.Count == 0, "Controls without an AutomationProperties.Name: " + string.Join("; ", unnamed));
    }

    [AvaloniaFact]
    public async Task Every_setting_control_has_a_help_text_line_next_to_it()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        await t.Main.OpenSettingsAsync();
        Dispatcher.UIThread.RunJobs();

        var view = window.GetVisualDescendants().OfType<SettingsView>().Single();
        var captions = view.GetVisualDescendants().OfType<TextBlock>().Where(b => b.Classes.Contains("caption") && b.Classes.Contains("muted")).ToList();

        // 15 settings, plus the output parent folder and Clear recent which share a block with another setting's help.
        Assert.True(captions.Count >= 15, "Found only " + captions.Count + " help lines.");
        Assert.All(captions, c => Assert.False(string.IsNullOrWhiteSpace(c.Text)));
    }

    [AvaloniaFact]
    public async Task The_privacy_note_is_plain_text_and_not_a_control_you_can_switch()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        await t.Main.OpenSettingsAsync();
        Dispatcher.UIThread.RunJobs();

        var view = window.GetVisualDescendants().OfType<SettingsView>().Single();
        var note = view.GetVisualDescendants().OfType<TextBlock>().Single(b => AutomationProperties.GetAutomationId(b) == "PrivacyNote");

        Assert.Equal("AI repair is off. Nothing is uploaded.", note.Text);
        Assert.False(note.Focusable);
    }

    [AvaloniaFact]
    public async Task Changing_the_theme_setting_changes_the_app_theme_and_so_does_the_rail_toggle()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        try
        {
            await t.Main.OpenSettingsAsync();
            t.Get<SettingsViewModel>().SelectedTheme = SettingsViewModel.ThemeChoices.Single(c => c.Value == ThemeChoice.Dark);
            Assert.Equal(ThemeVariant.Dark, Application.Current.RequestedThemeVariant);
            Assert.Equal("Theme: Dark", window.FindControl<TextBlock>("ThemeToggleText")!.Text);

            window.FindControl<Button>("ThemeToggle")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(ThemeVariant.Default, Application.Current.RequestedThemeVariant);
            Assert.Equal(ThemeChoice.System, t.Settings.Current.Theme);
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }
    }

    [AvaloniaFact]
    public async Task The_motion_setting_decides_whether_motion_is_reduced()
    {
        using var t = TestServices.Create();
        var window = Show(t);
        Assert.NotNull(window);
        try
        {
            await t.Main.OpenSettingsAsync();
            var vm = t.Get<SettingsViewModel>();

            vm.SelectedMotion = SettingsViewModel.MotionChoices.Single(c => c.Value == MotionChoice.On);
            Assert.True(MotionPreference.Reduce());

            vm.SelectedMotion = SettingsViewModel.MotionChoices.Single(c => c.Value == MotionChoice.Off);
            Assert.Equal(Environment.GetEnvironmentVariable("XNAREBORN_REDUCE_MOTION") == "1", MotionPreference.Reduce());
        }
        finally
        {
            MotionPreference.Mode = MotionChoice.System;
        }
    }
}
