using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.Controls;
using XnaReborn.Core.Models;

namespace XnaReborn.App.Tests.Report;

public sealed class ControlTests
{
    private static readonly Severity[] _all = Enum.GetValues<Severity>();

    // SeverityBadge

    [AvaloniaFact]
    public void Each_severity_badge_shows_an_icon_a_text_label_and_a_color_and_an_accessible_name()
    {
        var (window, badges) = ShowBadges();

        var shown = badges.Select(b => (Icon: Text(b, 0), Label: Text(b, 1), Brush: Assert.IsType<SolidColorBrush>(b.Foreground), Name: AutomationProperties.GetName(b))).ToList();

        Assert.Equal(_all.Select(s => s.ToString()), shown.Select(s => s.Label));
        Assert.Equal(_all.Select(s => "Severity: " + s), shown.Select(s => s.Name));
        Assert.All(shown, s => Assert.False(string.IsNullOrWhiteSpace(s.Icon)));

        // Color is never the only cue: icons differ and labels differ, not just the colors.
        Assert.Equal(4, shown.Select(s => s.Icon).Distinct().Count());
        Assert.Equal(4, shown.Select(s => s.Brush.Color).Distinct().Count());
        window.Close();
    }

    [AvaloniaFact]
    public void The_badge_follows_a_change_of_severity()
    {
        var badge = new SeverityBadge { Severity = Severity.Info };
        var window = new Window { Content = badge };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var before = Text(badge, 1);

        badge.Severity = Severity.Blocker;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Info", before);
        Assert.Equal("Blocker", Text(badge, 1));
        Assert.Equal("Severity: Blocker", AutomationProperties.GetName(badge));
        window.Close();
    }

    [AvaloniaFact]
    public void The_badge_color_changes_with_the_theme()
    {
        var (window, badges) = ShowBadges();
        var light = badges.Select(b => ((SolidColorBrush)b.Foreground!).Color).ToList();

        window.RequestedThemeVariant = ThemeVariant.Dark;
        Dispatcher.UIThread.RunJobs();
        var dark = badges.Select(b => ((SolidColorBrush)b.Foreground!).Color).ToList();

        Assert.All(light.Zip(dark), pair => Assert.NotEqual(pair.First, pair.Second));
        window.Close();
    }

    // ScoreGauge

    [AvaloniaTheory]
    [InlineData(0, 0.0)]
    [InlineData(40, 144.0)]
    [InlineData(50, 180.0)]
    public void The_gauge_prints_the_score_and_sweeps_the_ring_in_proportion(int score, double sweep)
    {
        var (window, gauge) = ShowGauge(score, null);

        Assert.Equal(score.ToString(System.Globalization.CultureInfo.InvariantCulture), Named<TextBlock>(gauge, "ScoreText").Text);
        Assert.Equal(sweep, Named<Arc>(gauge, "ScoreArc").SweepAngle, 3);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(150, "100")]
    [InlineData(-5, "0")]
    public void The_gauge_keeps_a_score_outside_the_range_between_0_and_100(int score, string text)
    {
        var (window, gauge) = ShowGauge(score, null);

        Assert.Equal(text, Named<TextBlock>(gauge, "ScoreText").Text);
        Assert.InRange(Named<Arc>(gauge, "ScoreArc").SweepAngle, 0, 359.9);
        window.Close();
    }

    [AvaloniaFact]
    public void A_full_score_still_draws_a_ring()
    {
        var (window, gauge) = ShowGauge(100, null);

        Assert.True(Named<Arc>(gauge, "ScoreArc").SweepAngle > 359);
        window.Close();
    }

    [AvaloniaFact]
    public void The_gauge_shows_the_projected_score_and_names_both_scores_for_screen_readers()
    {
        var (window, gauge) = ShowGauge(40, 90);

        Assert.True(Named<StackPanel>(gauge, "ProjectedPanel").IsVisible);
        Assert.Contains("90", Named<TextBlock>(gauge, "ProjectedText").Text, StringComparison.Ordinal);
        Assert.Equal(90, Named<ProgressBar>(gauge, "ProjectedBar").Value);
        Assert.Equal("Compatibility score 40 out of 100. After automatic fixes 90 out of 100", AutomationProperties.GetName(gauge));
        window.Close();
    }

    [AvaloniaFact]
    public void The_gauge_hides_the_projected_score_when_there_is_none()
    {
        var (window, gauge) = ShowGauge(40, null);

        Assert.False(Named<StackPanel>(gauge, "ProjectedPanel").IsVisible);
        Assert.Equal("Compatibility score 40 out of 100", AutomationProperties.GetName(gauge));
        window.Close();
    }

    [AvaloniaFact]
    public void The_gauge_follows_changes_of_its_properties()
    {
        var (window, gauge) = ShowGauge(10, null);

        gauge.Score = 75;
        gauge.ProjectedScore = 95;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("75", Named<TextBlock>(gauge, "ScoreText").Text);
        Assert.Equal(270.0, Named<Arc>(gauge, "ScoreArc").SweepAngle, 3);
        Assert.True(Named<StackPanel>(gauge, "ProjectedPanel").IsVisible);
        window.Close();
    }

    private static (Window Window, List<SeverityBadge> Badges) ShowBadges()
    {
        var badges = _all.Select(s => new SeverityBadge { Severity = s }).ToList();
        var panel = new StackPanel();
        panel.Children.AddRange(badges);
        var window = new Window { Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, badges);
    }

    private static (Window Window, ScoreGauge Gauge) ShowGauge(int score, int? projected)
    {
        var gauge = new ScoreGauge { Score = score, ProjectedScore = projected };
        var window = new Window { Content = gauge };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, gauge);
    }

    private static string? Text(SeverityBadge badge, int index) => badge.GetVisualDescendants().OfType<TextBlock>().ElementAt(index).Text;

    private static T Named<T>(Control root, string name)
        where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
}
