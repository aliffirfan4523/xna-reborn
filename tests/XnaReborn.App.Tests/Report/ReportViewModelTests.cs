using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Services;
using XnaReborn.App.ViewModels;
using XnaReborn.Core.Models;

namespace XnaReborn.App.Tests.Report;

public sealed class ReportViewModelTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xnareborn-report-tests-" + Guid.NewGuid().ToString("N"));

    public ReportViewModelTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static ReportViewModel Create(TestServices t, AnalysisReport? report = null)
    {
        t.Session.Report = report ?? ReportTestData.Small();
        return t.Get<ReportViewModel>();
    }

    private static ReportFilterChoice<Severity> Pick(ReportViewModel vm, Severity? value) =>
        vm.SeverityChoices.Single(c => c.Value == value);

    private static ReportFilterChoice<Classification> Pick(ReportViewModel vm, Classification? value) =>
        vm.ClassificationChoices.Single(c => c.Value == value);

    // Grouping

    [Fact]
    public void Findings_are_grouped_by_classification_in_a_fixed_order_with_counts_in_the_header()
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        Assert.Equal([Classification.AutoFixable, Classification.Manual, Classification.Unsupported], vm.Groups.Select(g => g.Classification));
        Assert.Equal(["Auto-fixable (3)", "Manual (2)", "Unsupported (1)"], vm.Groups.Select(g => g.Header));
        Assert.Equal(3, vm.AutoFixableCount);
        Assert.Equal(2, vm.ManualCount);
        Assert.Equal(1, vm.UnsupportedCount);
        Assert.Equal(42, vm.Score);
        Assert.Equal(77, vm.ProjectedScore);
    }

    [Fact]
    public void The_flat_list_marks_the_first_row_of_each_group_and_keeps_report_order()
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        Assert.Equal(["XNA001", "XNA022", "XNA004", "XNA027", "XNA099", "XNA050"], vm.Rows.Select(r => r.Item.RuleId));
        Assert.Equal([true, false, false, true, false, true], vm.Rows.Select(r => r.StartsGroup));
        Assert.Equal("Auto-fixable (3)", vm.Rows[0].GroupHeader);
        Assert.Equal(string.Empty, vm.Rows[1].GroupHeader);
    }

    [Fact]
    public void A_group_with_no_findings_is_left_out()
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        vm.SelectedClassification = Pick(vm, Classification.Manual);

        Assert.Equal([Classification.Manual], vm.Groups.Select(g => g.Classification));
    }

    [Fact]
    public void The_detail_joins_the_finding_with_the_explanation_and_resolution_of_its_rule()
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        vm.SelectedRow = vm.Rows.Single(r => r.Item.RuleId == "XNA022");

        var item = vm.SelectedFinding!;
        Assert.Equal("SpriteBlendMode", item.Title);
        Assert.Equal("SpriteBlendMode was replaced by BlendState.", item.Explanation);
        Assert.Equal("Pass BlendState.AlphaBlend.", item.Resolution);
        Assert.Equal("Game1.cs:44", item.LocationText);
        Assert.Equal("spriteBatch.Begin(SpriteBlendMode.AlphaBlend);", item.Snippet);
        Assert.True(item.HasSnippet);
        Assert.True(vm.HasSelection);
        Assert.False(vm.ShowSelectionHint);
    }

    [Fact]
    public void A_project_level_finding_without_a_snippet_says_so()
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        vm.SelectedRow = vm.Rows.Single(r => r.Item.RuleId == "XNA004");

        Assert.Equal("Project level", vm.SelectedFinding!.LocationText);
        Assert.False(vm.SelectedFinding.HasSnippet);
    }

    [Fact]
    public void A_rule_missing_from_the_report_gets_a_plain_fallback_text()
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        vm.SelectedRow = vm.Rows.Single(r => r.Item.RuleId == "XNA099");

        Assert.Contains("No explanation", vm.SelectedFinding!.Explanation, StringComparison.Ordinal);
        Assert.Contains("No resolution", vm.SelectedFinding.Resolution, StringComparison.Ordinal);
    }

    // Filters and search

    [Theory]
    [InlineData(Severity.Info, new[] { "XNA099" })]
    [InlineData(Severity.Warning, new[] { "XNA004" })]
    [InlineData(Severity.Error, new[] { "XNA001", "XNA022", "XNA027" })]
    [InlineData(Severity.Blocker, new[] { "XNA050" })]
    public void The_severity_filter_keeps_only_that_severity(Severity severity, string[] expected)
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        vm.SelectedSeverity = Pick(vm, severity);

        Assert.Equal(expected, vm.Rows.Select(r => r.Item.RuleId));
        Assert.Equal(expected.Length, vm.VisibleCount);
        Assert.Equal(6, vm.TotalCount);
        Assert.Equal($"Showing {expected.Length} of 6 findings", vm.FilterSummary);
    }

    [Theory]
    [InlineData(Classification.AutoFixable, new[] { "XNA001", "XNA022", "XNA004" })]
    [InlineData(Classification.Manual, new[] { "XNA027", "XNA099" })]
    [InlineData(Classification.Unsupported, new[] { "XNA050" })]
    public void The_classification_filter_keeps_only_that_classification(Classification classification, string[] expected)
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        vm.SelectedClassification = Pick(vm, classification);

        Assert.Equal(expected, vm.Rows.Select(r => r.Item.RuleId));
    }

    [Fact]
    public void Both_filters_apply_together()
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        vm.SelectedSeverity = Pick(vm, Severity.Error);
        vm.SelectedClassification = Pick(vm, Classification.Manual);

        Assert.Equal(["XNA027"], vm.Rows.Select(r => r.Item.RuleId));
    }

    [Theory]
    [InlineData("xna022", new[] { "XNA022" })] // rule id, any case
    [InlineData("resolve", new[] { "XNA027" })] // title
    [InlineData("level.cs", new[] { "XNA099" })] // file
    [InlineData("gamer.signin", new[] { "XNA050" })] // snippet
    [InlineData("  Game1.cs  ", new[] { "XNA022", "XNA027" })] // trimmed
    [InlineData("nothing-matches-this", new string[0])]
    public void Search_matches_rule_id_title_file_and_snippet_ignoring_case(string text, string[] expected)
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        vm.SearchText = text;

        Assert.Equal(expected, vm.Rows.Select(r => r.Item.RuleId));
    }

    [Fact]
    public void Search_does_not_match_the_explanation_text()
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        vm.SearchText = "shut down";

        Assert.Empty(vm.Rows);
    }

    [Fact]
    public void Filters_that_hide_everything_show_the_no_matches_state_and_clear_brings_the_list_back()
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        vm.SearchText = "nothing-matches-this";

        Assert.True(vm.ShowNoMatches);
        Assert.False(vm.ShowNoFindings);
        Assert.True(vm.HasActiveFilter);
        Assert.True(vm.ClearFiltersCommand.CanExecute(null));

        vm.ClearFiltersCommand.Execute(null);

        Assert.Equal(6, vm.VisibleCount);
        Assert.False(vm.ShowNoMatches);
        Assert.False(vm.HasActiveFilter);
        Assert.False(vm.ClearFiltersCommand.CanExecute(null));
        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Null(vm.SelectedSeverity.Value);
        Assert.Null(vm.SelectedClassification.Value);
    }

    [Fact]
    public void The_selection_stays_while_the_finding_is_visible_and_goes_when_it_is_filtered_out()
    {
        using var t = TestServices.Create();
        var vm = Create(t);
        vm.SelectedRow = vm.Rows.Single(r => r.Item.RuleId == "XNA027");

        vm.SelectedClassification = Pick(vm, Classification.Manual);
        Assert.Equal("XNA027", vm.SelectedFinding?.RuleId);
        Assert.True(vm.SelectedRow!.StartsGroup);

        vm.SelectedClassification = Pick(vm, Classification.AutoFixable);
        Assert.Null(vm.SelectedFinding);
        Assert.True(vm.ShowSelectionHint);
    }

    // Loading, empty and no-project reports

    [Fact]
    public void A_new_report_replaces_the_old_one_and_resets_filters_and_selection()
    {
        using var t = TestServices.Create();
        var vm = Create(t);
        vm.SearchText = "xna022";
        vm.SelectedRow = vm.Rows.Single();

        t.Session.Report = SampleReportWithOneFinding();

        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Null(vm.SelectedFinding);
        Assert.Equal(["XNA010"], vm.Rows.Select(r => r.Item.RuleId));
        Assert.Equal(1, vm.TotalCount);
    }

    [Fact]
    public void A_project_without_findings_shows_the_no_findings_state()
    {
        using var t = TestServices.Create();
        var vm = Create(t, Fakes.SampleData.Report(score: 100, projectedScore: 100));

        Assert.True(vm.HasProject);
        Assert.True(vm.ShowNoFindings);
        Assert.False(vm.ShowNoMatches);
        Assert.Empty(vm.Rows);
        Assert.True(vm.ContinueCommand.CanExecute(null));
    }

    [Fact]
    public void A_report_with_no_XNA_project_shows_the_reason_and_cannot_continue()
    {
        using var t = TestServices.Create();
        var vm = Create(t, ReportTestData.Small(noProjectReason: "No .csproj with an XNA reference was found."));

        Assert.True(vm.ShowNoProject);
        Assert.False(vm.HasProject);
        Assert.Equal("No .csproj with an XNA reference was found.", vm.NoProjectReason);
        Assert.Empty(vm.Rows);
        Assert.False(vm.ShowNoFindings);
        Assert.False(vm.ContinueCommand.CanExecute(null));
        Assert.True(vm.ExportMarkdownCommand.CanExecute(null));
    }

    [Fact]
    public void Diagnostics_are_exposed_for_the_notes_panel()
    {
        using var t = TestServices.Create();
        var vm = Create(t, ReportTestData.Small(diagnostics: [new Diagnostic("ANA002", "Could not read a file.", "Game.csproj")]));

        Assert.True(vm.HasDiagnostics);
        Assert.Equal("ANA002", Assert.Single(vm.Diagnostics).Code);
    }

    [Fact]
    public void Without_a_report_nothing_can_be_exported_or_continued()
    {
        using var t = TestServices.Create();
        var vm = t.Get<ReportViewModel>();

        Assert.False(vm.HasReport);
        Assert.False(vm.ExportMarkdownCommand.CanExecute(null));
        Assert.False(vm.ExportJsonCommand.CanExecute(null));
        Assert.False(vm.CopyReportCommand.CanExecute(null));
        Assert.False(vm.ContinueCommand.CanExecute(null));
    }

    // Export

    [Fact]
    public async Task Export_Markdown_writes_the_service_text_to_the_chosen_file_without_a_byte_order_mark()
    {
        using var t = TestServices.Create();
        var vm = Create(t);
        var path = Path.Combine(_temp, "report.md");
        t.Dialogs.SavePathToReturn = path;

        await vm.ExportMarkdownCommand.ExecuteAsync(null);

        var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal("# Fake report, score 42", new UTF8Encoding(false).GetString(bytes));
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Contains(path, vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_JSON_writes_the_service_text_to_the_chosen_file()
    {
        using var t = TestServices.Create();
        var vm = Create(t);
        var path = Path.Combine(_temp, "report.json");
        t.Dialogs.SavePathToReturn = path;

        await vm.ExportJsonCommand.ExecuteAsync(null);

        Assert.Equal("{\"score\":42}", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cancelling_the_save_dialog_writes_nothing_and_shows_no_error()
    {
        using var t = TestServices.Create();
        var vm = Create(t);
        t.Dialogs.SavePathToReturn = null;

        await vm.ExportMarkdownCommand.ExecuteAsync(null);

        Assert.Empty(Directory.GetFileSystemEntries(_temp));
        Assert.False(t.Session.HasError);
        Assert.Equal(string.Empty, vm.StatusMessage);
    }

    [Fact]
    public async Task Export_refuses_a_path_inside_the_original_project_and_writes_nothing()
    {
        using var t = TestServices.Create();
        var project = Directory.CreateDirectory(Path.Combine(_temp, "project")).FullName;
        t.Session.SourcePath = project;
        var vm = Create(t);
        var inside = Path.Combine(project, "sub", "report.md");
        t.Dialogs.SavePathToReturn = inside;

        await vm.ExportMarkdownCommand.ExecuteAsync(null);

        Assert.False(File.Exists(inside));
        Assert.Empty(Directory.GetFileSystemEntries(project));
        Assert.True(t.Session.HasError);
    }

    [Fact]
    public async Task Export_refuses_a_path_next_to_the_project_file_when_the_project_is_a_csproj()
    {
        using var t = TestServices.Create();
        var project = Path.Combine(_temp, "Game.csproj");
        await File.WriteAllTextAsync(project, "<Project />", TestContext.Current.CancellationToken);
        t.Session.SourcePath = project;
        var vm = Create(t);
        t.Dialogs.SavePathToReturn = Path.Combine(_temp, "report.md");

        await vm.ExportMarkdownCommand.ExecuteAsync(null);

        Assert.False(File.Exists(Path.Combine(_temp, "report.md")));
        Assert.True(t.Session.HasError);
    }

    [Fact]
    public async Task Export_allows_a_sibling_folder_whose_name_starts_with_the_project_folder_name()
    {
        using var t = TestServices.Create();
        var project = Directory.CreateDirectory(Path.Combine(_temp, "game")).FullName;
        var sibling = Directory.CreateDirectory(Path.Combine(_temp, "game-reports")).FullName;
        t.Session.SourcePath = project;
        var vm = Create(t);
        t.Dialogs.SavePathToReturn = Path.Combine(sibling, "report.md");

        await vm.ExportMarkdownCommand.ExecuteAsync(null);

        Assert.True(File.Exists(Path.Combine(sibling, "report.md")));
        Assert.False(t.Session.HasError);
    }

    [Fact]
    public async Task A_file_that_cannot_be_written_shows_an_error_instead_of_crashing()
    {
        using var t = TestServices.Create();
        var vm = Create(t);
        t.Dialogs.SavePathToReturn = Path.Combine(_temp, "missing-folder", "report.md");

        await vm.ExportMarkdownCommand.ExecuteAsync(null);

        Assert.True(t.Session.HasError);
        Assert.Equal("The report could not be saved.", t.Session.ErrorMessage);
    }

    [Fact]
    public async Task A_cancelled_export_reports_it_and_leaves_no_file()
    {
        using var t = TestServices.Create();
        var vm = Create(t);
        var path = Path.Combine(_temp, "report.md");
        t.Dialogs.SavePathToReturn = path;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await vm.ExportMarkdownAsync(cts.Token);

        Assert.Equal("Export cancelled.", vm.StatusMessage);
        Assert.False(File.Exists(path));
    }

    // Copy and navigation

    [Fact]
    public async Task Copy_report_puts_the_Markdown_on_the_clipboard()
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        await vm.CopyReportCommand.ExecuteAsync(null);

        Assert.Equal("# Fake report, score 42", t.Clipboard.Text);
    }

    [Fact]
    public async Task Copy_finding_needs_a_selection_and_copies_title_explanation_resolution_and_snippet()
    {
        using var t = TestServices.Create();
        var vm = Create(t);
        Assert.False(vm.CopyFindingCommand.CanExecute(null));

        vm.SelectedRow = vm.Rows.Single(r => r.Item.RuleId == "XNA022");
        Assert.True(vm.CopyFindingCommand.CanExecute(null));
        await vm.CopyFindingCommand.ExecuteAsync(null);

        var text = t.Clipboard.Text!;
        Assert.Contains("XNA022: SpriteBlendMode", text, StringComparison.Ordinal);
        Assert.Contains("SpriteBlendMode was replaced by BlendState.", text, StringComparison.Ordinal);
        Assert.Contains("Resolution: Pass BlendState.AlphaBlend.", text, StringComparison.Ordinal);
        Assert.Contains("Game1.cs:44", text, StringComparison.Ordinal);
        Assert.Contains("spriteBatch.Begin(SpriteBlendMode.AlphaBlend);", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_locked_clipboard_shows_an_error()
    {
        using var t = TestServices.Create();
        var vm = Create(t);
        t.Clipboard.Failure = new InvalidOperationException("locked");

        await vm.CopyReportCommand.ExecuteAsync(null);

        Assert.Equal("The text could not be copied.", t.Session.ErrorMessage);
        Assert.Equal("locked", t.Session.ErrorDetails);
    }

    [Fact]
    public void Continue_goes_to_the_migration_setup_page()
    {
        using var t = TestServices.Create();
        var vm = Create(t);

        vm.ContinueCommand.Execute(null);

        Assert.Equal(WorkflowStep.MigrationSetup, t.Main.CurrentStep);
    }

    [Fact]
    public void Choose_another_project_goes_back_to_the_open_page()
    {
        using var t = TestServices.Create();
        var vm = Create(t, ReportTestData.Small(noProjectReason: "No XNA project."));
        t.Main.GoTo(WorkflowStep.Report);

        vm.ChooseAnotherProjectCommand.Execute(null);

        Assert.Equal(WorkflowStep.Open, t.Main.CurrentStep);
    }

    // A real report from the real analyzer

    [Fact]
    public async Task A_real_report_of_the_xna31_legacy_fixture_is_grouped_joined_and_filterable()
    {
        var real = await ReportTestData.RealLegacyAsync();
        using var t = TestServices.Create();
        var vm = Create(t, real);

        Assert.NotEmpty(real.Findings);
        Assert.Equal(real.Findings.Count, vm.TotalCount);
        Assert.Equal(real.Findings.Count, vm.Groups.Sum(g => g.Items.Count));
        Assert.Equal(real.CountsByClassification[Classification.AutoFixable], vm.AutoFixableCount);
        Assert.Equal(real.CountsByClassification[Classification.Manual], vm.ManualCount);
        Assert.Equal(real.Score, vm.Score);
        Assert.Equal(real.ProjectedScore, vm.ProjectedScore);
        Assert.True(vm.Groups.All(g => g.Items.All(i => i.Classification == g.Classification)));

        // Every finding of the real catalog has an explanation and a resolution joined from its rule.
        Assert.All(vm.Groups.SelectMany(g => g.Items), i =>
        {
            Assert.NotNull(i.Rule);
            Assert.False(string.IsNullOrWhiteSpace(i.Explanation));
            Assert.False(string.IsNullOrWhiteSpace(i.Resolution));
        });

        // A source-level finding carries file, line and snippet.
        var render = vm.Rows.First(r => r.Item.RuleId == "XNA024").Item;
        Assert.Matches(@"^Game1\.cs:\d+$", render.LocationText);
        Assert.Contains("RenderState", render.Snippet, StringComparison.Ordinal);

        vm.SearchText = "Game1.cs";
        Assert.NotEmpty(vm.Rows);
        Assert.All(vm.Rows, r => Assert.NotNull(r.Item.Finding.Location));

        vm.SearchText = string.Empty;
        vm.SelectedSeverity = Pick(vm, Severity.Warning);
        Assert.All(vm.Rows, r => Assert.Equal(Severity.Warning, r.Item.Severity));
        Assert.Equal(real.CountsBySeverity[Severity.Warning], vm.VisibleCount);
    }

    [Fact]
    public async Task Exporting_a_real_report_with_the_real_service_writes_what_the_service_renders()
    {
        var real = await ReportTestData.RealLegacyAsync();
        var service = new AnalysisService();
        using var t = TestServices.Create(s => s.AddSingleton<IAnalysisService>(service));
        var vm = Create(t, real);
        var md = Path.Combine(_temp, "legacy.md");
        var json = Path.Combine(_temp, "legacy.json");

        t.Dialogs.SavePathToReturn = md;
        await vm.ExportMarkdownCommand.ExecuteAsync(null);
        t.Dialogs.SavePathToReturn = json;
        await vm.ExportJsonCommand.ExecuteAsync(null);

        Assert.Equal(service.ToMarkdown(real), await File.ReadAllTextAsync(md, TestContext.Current.CancellationToken));
        Assert.Equal(service.ToJson(real), await File.ReadAllTextAsync(json, TestContext.Current.CancellationToken));
        Assert.StartsWith("# XNA Reborn compatibility report", await File.ReadAllTextAsync(md, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Contains("\"findings\"", await File.ReadAllTextAsync(json, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_real_report_of_a_folder_without_XNA_shows_the_analyzer_reason()
    {
        var real = await new AnalysisService().AnalyzeAsync(RepoPaths.Fixture("not-xna"), null, TestContext.Current.CancellationToken);
        using var t = TestServices.Create();
        var vm = Create(t, real);

        Assert.True(vm.ShowNoProject);
        Assert.Equal(real.NoProjectReason, vm.NoProjectReason);
        Assert.False(string.IsNullOrWhiteSpace(vm.NoProjectReason));
        Assert.Empty(vm.Rows);
        Assert.False(vm.ContinueCommand.CanExecute(null));
    }

    private static AnalysisReport SampleReportWithOneFinding() => Fakes.SampleData.Report() with
    {
        Findings = [new Finding("XNA010", "One", Severity.Warning, Classification.Manual, "A.csproj", null, null)],
    };
}
