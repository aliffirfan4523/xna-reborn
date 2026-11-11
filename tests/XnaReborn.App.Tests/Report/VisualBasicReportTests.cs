using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.Services;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using XnaReborn.Core.Models;

namespace XnaReborn.App.Tests.Report;

/// <summary>Older-and-vb task 22: a Visual Basic project shows a language badge and its XNA110 finding, and the diff recognizes Visual Basic markers.</summary>
public sealed class VisualBasicReportTests
{
    private static ProjectInfo Info(string path, SourceLanguage language) => new(
        path, "Game", true, XnaVersion.V40, XnaPlatform.Windows, null, null, [], [], [], [], [], Language: language);

    [Fact]
    public void A_visual_basic_project_row_has_the_badge_and_a_csharp_row_does_not()
    {
        var vb = new ProjectRowItem(Info("Game.vbproj", SourceLanguage.VisualBasic));
        var cs = new ProjectRowItem(Info("Game.csproj", SourceLanguage.CSharp));

        Assert.True(vb.IsVisualBasic);
        Assert.Equal("Visual Basic", vb.LanguageBadgeText);
        Assert.Contains("Visual Basic", vb.AutomationName, StringComparison.Ordinal);
        Assert.False(cs.IsVisualBasic);
        Assert.DoesNotContain("Visual Basic", cs.AutomationName, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_report_of_the_vb_fixture_shows_the_badge_and_the_vb_finding()
    {
        var report = await new AnalysisService().AnalyzeAsync(RepoPaths.Fixture("vb-xna40-basic"), null, TestContext.Current.CancellationToken);
        using var t = TestServices.Create();
        t.Session.Report = report;
        var view = new ReportView { DataContext = t.Get<ReportViewModel>() };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var panel = view.GetVisualDescendants().OfType<Expander>().Single(e => Avalonia.Automation.AutomationProperties.GetAutomationId(e) == "ProjectsPanel");
        var badge = panel.GetVisualDescendants().OfType<TextBlock>().Single(b => Avalonia.Automation.AutomationProperties.GetAutomationId(b) == "LanguageBadge");

        Assert.Equal(SourceLanguage.VisualBasic, Assert.Single(report.Projects).Language);
        Assert.True(badge.IsVisible);
        Assert.Equal("Visual Basic", badge.Text);
        Assert.Contains(report.Findings, f => f.RuleId == "XNA110");
        window.Close();
    }

    [Theory]
    [InlineData("' XNAREBORN: XNA024: look here")]
    [InlineData("    // XNAREBORN: XNA024: look here")]
    public void Marker_lines_of_both_languages_are_markers_in_the_diff(string text)
    {
        var line = new XnaReborn.Migrator.Source.DiffLine(2, text, XnaReborn.Migrator.Source.DiffLineKind.Added);

        Assert.True(XnaReborn.App.Controls.DiffViewer.IsMarker(line));
    }

    [Fact]
    public void A_plain_comment_is_not_a_marker_in_the_diff()
    {
        var line = new XnaReborn.Migrator.Source.DiffLine(2, "' just a comment", XnaReborn.Migrator.Source.DiffLineKind.Added);

        Assert.False(XnaReborn.App.Controls.DiffViewer.IsMarker(line));
    }
}
