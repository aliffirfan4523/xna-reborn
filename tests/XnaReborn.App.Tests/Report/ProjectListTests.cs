using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XnaReborn.App.Services;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using XnaReborn.Core.Models;

namespace XnaReborn.App.Tests.Report;

/// <summary>Monogame-upgrade task 16: the report page lists each project with family, version (with confidence) and platform.</summary>
public sealed class ProjectListTests
{
    private static ProjectInfo Xna() => new(
        "Game/Game.csproj", "Game", true, XnaVersion.V40, XnaPlatform.Windows, null, null, [], [], [], [], []);

    private static ProjectInfo Old(MonoGamePlatform platform, MonoGameVersionInfo? version, string path = "Desktop/Desktop.csproj") => new(
        path, "Desktop", false, XnaVersion.Unknown, XnaPlatform.Unknown, null, null, [], [], [], [], [],
        Family: ProjectFamily.MonoGameLegacy, MonoGameVersion: version, MonoGamePlatform: platform);

    private static AnalysisReport WithProjects(params ProjectInfo[] projects) => ReportTestData.Small() with { Projects = projects };

    [Fact]
    public void An_xna_project_shows_the_xna_version_and_platform()
    {
        var row = new ProjectRowItem(Xna());

        Assert.Equal("XNA", row.FamilyText);
        Assert.Equal("XNA 4.0", row.VersionText);
        Assert.Equal("Windows", row.PlatformText);
        Assert.Equal("XNA · XNA 4.0 · Windows", row.Summary);
        Assert.False(row.IsNotMigrated);
    }

    [Fact]
    public void An_old_monogame_project_shows_the_exact_version_with_its_confidence()
    {
        var row = new ProjectRowItem(Old(MonoGamePlatform.WindowsDX, new MonoGameVersionInfo("3.7.0.1708", MonoGameBand.V37, Confidence.High)));

        Assert.Equal("MonoGame (old)", row.FamilyText);
        Assert.Equal("3.7.0.1708 (High)", row.VersionText);
        Assert.Equal("Windows (DirectX)", row.PlatformText);
        Assert.False(row.IsNotMigrated);
    }

    [Fact]
    public void A_version_band_is_shown_as_a_range_with_its_confidence()
    {
        var row = new ProjectRowItem(Old(MonoGamePlatform.WindowsGL, new MonoGameVersionInfo(null, MonoGameBand.V32To36, Confidence.Medium)));

        Assert.Equal("3.2 to 3.6 (Medium)", row.VersionText);
    }

    [Fact]
    public void A_missing_or_unknown_version_says_so()
    {
        Assert.Equal("Version unknown", new ProjectRowItem(Old(MonoGamePlatform.DesktopGL, null)).VersionText);
        Assert.Equal("Version unknown", new ProjectRowItem(Old(MonoGamePlatform.DesktopGL, MonoGameVersionInfo.Unknown)).VersionText);
    }

    [Theory]
    [InlineData(MonoGamePlatform.Uwp, "UWP")]
    [InlineData(MonoGamePlatform.Android, "Android")]
    [InlineData(MonoGamePlatform.Ios, "iOS")]
    [InlineData(MonoGamePlatform.WindowsPhone, "Windows Phone")]
    public void A_platform_project_is_marked_not_migrated(MonoGamePlatform platform, string text)
    {
        var row = new ProjectRowItem(Old(platform, null));

        Assert.True(row.IsNotMigrated);
        Assert.Equal(text, row.PlatformText);
        Assert.Equal("Not migrated, see manual work", row.NotMigratedText);
        Assert.Contains("not migrated, see manual work", row.AutomationName, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MonoGamePlatform.WindowsDX)]
    [InlineData(MonoGamePlatform.DesktopGL)]
    [InlineData(MonoGamePlatform.Linux)]
    [InlineData(MonoGamePlatform.Unknown)]
    public void A_desktop_project_is_not_marked(MonoGamePlatform platform) =>
        Assert.False(new ProjectRowItem(Old(platform, null)).IsNotMigrated);

    [Fact]
    public void The_page_lists_the_projects_of_the_report_and_none_without_a_report()
    {
        using var t = TestServices.Create();
        t.Session.Report = WithProjects(Xna(), Old(MonoGamePlatform.Uwp, null, "Uwp/Uwp.csproj"));
        var vm = t.Get<ReportViewModel>();

        Assert.True(vm.HasProjects);
        Assert.Equal(["Game/Game.csproj", "Uwp/Uwp.csproj"], vm.Projects.Select(p => p.Path));

        t.Session.Report = null;

        Assert.False(vm.HasProjects);
        Assert.Empty(vm.Projects);
    }

    [AvaloniaFact]
    public void The_view_shows_each_project_and_the_not_migrated_line()
    {
        using var t = TestServices.Create();
        t.Session.Report = WithProjects(Old(MonoGamePlatform.WindowsDX, new MonoGameVersionInfo("3.7.0.1708", MonoGameBand.V37, Confidence.High)), Old(MonoGamePlatform.Android, null, "Android/Android.csproj"));
        var view = new ReportView { DataContext = t.Get<ReportViewModel>() };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var panel = view.GetVisualDescendants().OfType<Expander>().Single(e => Avalonia.Automation.AutomationProperties.GetAutomationId(e) == "ProjectsPanel");
        var texts = panel.GetVisualDescendants().OfType<TextBlock>().Where(b => b.IsVisible).Select(b => b.Text).ToList();

        Assert.True(panel.IsVisible);
        Assert.Contains("MonoGame (old) · 3.7.0.1708 (High) · Windows (DirectX)", texts);
        Assert.Contains("MonoGame (old) · Version unknown · Android", texts);
        Assert.Single(texts, x => x == "Not migrated, see manual work");
        window.Close();
    }

    [AvaloniaFact]
    public void The_panel_is_hidden_when_the_report_has_no_projects()
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var view = new ReportView { DataContext = t.Get<ReportViewModel>() };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var panel = view.GetVisualDescendants().OfType<Expander>().Single(e => Avalonia.Automation.AutomationProperties.GetAutomationId(e) == "ProjectsPanel");

        Assert.False(panel.IsVisible);
        window.Close();
    }

    [Fact]
    public async Task The_real_service_analyzes_an_old_monogame_fixture_with_its_catalog()
    {
        var report = await new AnalysisService().AnalyzeAsync(RepoPaths.Fixture("mg37-windowsdx-pkgconfig"), null, TestContext.Current.CancellationToken);

        Assert.Null(report.NoProjectReason);
        var project = Assert.Single(report.Projects);
        Assert.Equal(ProjectFamily.MonoGameLegacy, project.Family);
        Assert.Contains(report.Findings, f => f.RuleId == "MG001");
        Assert.DoesNotContain(report.Findings, f => f.RuleId.StartsWith("XNA", StringComparison.Ordinal));
        Assert.Contains("monogame-upgrade", report.CatalogName, StringComparison.Ordinal);
        Assert.Equal("MonoGame (old) · 3.7.0.1708 (High) · Windows (DirectX)", new ProjectRowItem(project).Summary);
    }

    [Fact]
    public async Task A_missing_monogame_catalog_does_not_stop_xna_analysis_and_is_reported()
    {
        var service = new AnalysisService(monoGameCatalogPath: Path.Combine(Path.GetTempPath(), "mg-catalog-" + Guid.NewGuid().ToString("N") + ".json"));

        var xna = await service.AnalyzeAsync(RepoPaths.Fixture("xna40-basic"), null, TestContext.Current.CancellationToken);

        Assert.NotEmpty(xna.Findings);
        var note = Assert.Single(xna.Diagnostics, d => d.Code == "ANA014");
        Assert.Contains("MonoGame", note.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_toolkit_project_is_labeled_as_sharpdx_toolkit()
    {
        var row = new ProjectRowItem(new ProjectInfo("T.csproj", "T", false, XnaVersion.Unknown, XnaPlatform.Unknown, null, null, [], [], [], [], [], Family: ProjectFamily.SharpDxToolkit));

        Assert.Equal("SharpDX Toolkit", row.FamilyText);
        Assert.Contains("SharpDX Toolkit", row.AutomationName, StringComparison.Ordinal);
        Assert.False(row.IsNotMigrated);
    }
}
