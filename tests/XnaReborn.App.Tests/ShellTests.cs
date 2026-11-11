using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.Controls;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using AppBanner = XnaReborn.App.Controls.Banner;

namespace XnaReborn.App.Tests;

/// <summary>The redesigned shell: header, theme toggle and the shared controls.</summary>
public sealed class ShellTests
{
    [AvaloniaFact]
    public async Task The_header_shows_the_project_name_and_score_once_a_report_exists()
    {
        using var t = TestServices.Create();
        Assert.False(t.Main.HasProject);
        Assert.False(t.Main.HasScore);

        await t.OpenPageAsync(WorkflowStep.Report);

        Assert.True(t.Main.HasProject);
        Assert.Equal("xna40-basic", t.Main.ProjectName);
        Assert.True(t.Main.HasScore);
        Assert.StartsWith("Score ", t.Main.ScoreText, StringComparison.Ordinal);
        Assert.Equal("Step 2 of 6: Report", t.Main.StatusText);
    }

    [AvaloniaFact]
    public void The_theme_toggle_cycles_light_dark_system()
    {
        using var t = TestServices.Create();
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var app = Application.Current!;
        app.RequestedThemeVariant = ThemeVariant.Default;
        var toggle = window.FindControl<Button>("ThemeToggle")!;

        toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(ThemeVariant.Light, app.RequestedThemeVariant);
        toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);
        toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(ThemeVariant.Default, app.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void Chip_and_banner_always_carry_a_word_and_an_icon_besides_color()
    {
        var host = new StackPanel
        {
            Children =
            {
                new Chip { Kind = StatusKind.Warning, Text = "3 warnings" },
                new AppBanner { Kind = StatusKind.Error, Title = "Build failed", Message = "See the log." },
            },
        };
        var window = new Window { Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var chip = host.GetVisualDescendants().OfType<Chip>().Single();
        Assert.Equal("Warning: 3 warnings", Avalonia.Automation.AutomationProperties.GetName(chip));
        Assert.NotNull(chip.GetVisualDescendants().OfType<PathIcon>().Single().Data);
        var banner = host.GetVisualDescendants().OfType<AppBanner>().Single();
        Assert.Equal("Error. Build failed. See the log.", Avalonia.Automation.AutomationProperties.GetName(banner));
        Assert.NotNull(banner.IconData);
    }
}
