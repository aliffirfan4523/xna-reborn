using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Planning;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator;

/// <summary>The planner turns auto-fixable findings into one checklist item per rule and project.</summary>
[Trait("Category", "Migrator")]
public class MigrationPlannerTests
{
    private static readonly IReadOnlySet<string> _none = new HashSet<string>();

    private static string CatalogPath => Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json"));

    private static Finding F(string rule, string project, string? file, Classification c = Classification.AutoFixable)
        => new(rule, "T-" + rule, Severity.Error, c, project, file is null ? null : new SourceLocation(file, 1, 1), null);

    private static TriggeredRule R(string id) => new(id, "Title " + id, "why", "Fix " + id, null);

    private static AnalysisReport Report(IReadOnlyList<Finding> findings, IReadOnlyList<TriggeredRule>? rules = null, string? noProject = null)
        => new(
            1,
            "test",
            [],
            findings,
            rules ?? findings.Select(f => f.RuleId).Distinct().Select(R).ToList(),
            new Dictionary<Severity, int>(),
            new Dictionary<Classification, int>(),
            0,
            0,
            [],
            noProject);

    private static async Task<AnalysisReport> AnalyzeAsync(string name)
    {
        var catalog = await RuleCatalogLoader.LoadAsync(CatalogPath, new ReadOnlyFileSystem(), CancellationToken.None);
        return await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of(name), catalog, null, CancellationToken.None);
    }

    [Fact]
    public void Findings_group_per_rule_and_project_with_places_text()
    {
        var report = Report([F("XNA022", "A/A.csproj", "x.cs"), F("XNA022", "A/A.csproj", "y.cs"), F("XNA022", "B/B.csproj", "x.cs")]);

        var plan = new MigrationPlanner().CreatePlan(report, _none);

        Assert.Equal(["XNA022:A/A.csproj", "XNA022:B/B.csproj"], plan.Items.Select(i => i.Id));
        Assert.Equal("Title XNA022: Fix XNA022 (2 places)", plan.Items[0].Description);
        Assert.Equal("Title XNA022: Fix XNA022", plan.Items[1].Description);
    }

    [Fact]
    public void Affected_files_are_distinct_sorted_and_project_level_uses_the_project_file()
    {
        var report = Report([F("XNA001", "G/G.csproj", null), F("XNA022", "G/G.csproj", "b.cs"), F("XNA022", "G/G.csproj", "a\\c.cs"), F("XNA022", "G/G.csproj", "b.cs")]);

        var plan = new MigrationPlanner().CreatePlan(report, _none);

        Assert.Equal(["G/G.csproj"], plan.Items[0].AffectedFiles);
        Assert.Equal(["a/c.cs", "b.cs"], plan.Items[1].AffectedFiles);
        Assert.EndsWith("(3 places)", plan.Items[1].Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Items_sort_by_rule_then_project_ordinally()
    {
        var report = Report([F("XNA010", "b.csproj", null), F("XNA002", "z.csproj", null), F("XNA002", "B.csproj", null), F("XNA002", "a.csproj", null)]);

        var plan = new MigrationPlanner().CreatePlan(report, _none);

        Assert.Equal(["XNA002:B.csproj", "XNA002:a.csproj", "XNA002:z.csproj", "XNA010:b.csproj"], plan.Items.Select(i => i.Id));
    }

    [Fact]
    public void Excluded_ids_move_items_and_unknown_ids_are_ignored()
    {
        var report = Report([F("XNA001", "a.csproj", null), F("XNA003", "a.csproj", null)]);

        var plan = new MigrationPlanner().CreatePlan(report, new HashSet<string> { "XNA001:a.csproj", "nope" });

        Assert.Equal(["XNA003:a.csproj"], plan.Items.Select(i => i.Id));
        Assert.Equal(["XNA001:a.csproj"], plan.ExcludedItems.Select(i => i.Id));
    }

    [Fact]
    public void Manual_and_unsupported_findings_are_never_planned()
    {
        var report = Report([F("XNA023", "a.csproj", "x.cs", Classification.Manual), F("XNA030", "a.csproj", "x.cs", Classification.Unsupported)]);

        var plan = new MigrationPlanner().CreatePlan(report, _none);

        Assert.Empty(plan.Items);
        Assert.Empty(plan.ExcludedItems);
    }

    [Fact]
    public void Missing_triggered_rule_falls_back_to_the_finding_title()
    {
        var report = Report([F("XNA022", "a.csproj", "x.cs")], []);

        var plan = new MigrationPlanner().CreatePlan(report, _none);

        Assert.Equal("T-XNA022", plan.Items[0].Description);
    }

    [Fact]
    public void Empty_and_no_project_reports_give_empty_plans()
    {
        var planner = new MigrationPlanner();

        Assert.Empty(planner.CreatePlan(Report([]), _none).Items);
        Assert.Empty(planner.CreatePlan(Report([], noProject: "none"), _none).Items);
    }

    [Fact]
    public void Same_input_gives_equal_output_through_both_overloads()
    {
        var report = Report([F("XNA022", "a.csproj", "x.cs"), F("XNA001", "a.csproj", null)]);
        var excluded = new HashSet<string> { "XNA001:a.csproj" };
        var planner = new MigrationPlanner();

        var first = planner.CreatePlan(report, excluded);
        var second = planner.CreatePlan(report, new MigrationOptions(@"C:\out", MonoGameTarget.DesktopGL, false, false, excluded));

        Assert.Equal(first.Items.Select(i => i.Id), second.Items.Select(i => i.Id));
        Assert.Equal(first.ExcludedItems.Select(i => i.Id), second.ExcludedItems.Select(i => i.Id));
        Assert.Equal(first.Items[0].AffectedFiles, second.Items[0].AffectedFiles);
        Assert.Equal(first.Items[0].Description, second.Items[0].Description);
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var planner = new MigrationPlanner();

        Assert.Throws<ArgumentNullException>(() => planner.CreatePlan(null!, _none));
        Assert.Throws<ArgumentNullException>(() => planner.CreatePlan(Report([]), (IReadOnlySet<string>)null!));
        Assert.Throws<ArgumentNullException>(() => planner.CreatePlan(Report([]), (MigrationOptions)null!));
    }

    [Theory]
    [InlineData("xna31-legacy")]
    [InlineData("xna40-basic")]
    public async Task Real_fixtures_plan_only_auto_fixable_rules(string name)
    {
        var report = await AnalyzeAsync(name);

        var plan = new MigrationPlanner().CreatePlan(report, _none);
        var again = new MigrationPlanner().CreatePlan(report, _none);

        Assert.NotEmpty(plan.Items);
        Assert.Equal(plan.Items.Select(i => i.Id), again.Items.Select(i => i.Id));
        var manual = report.Findings.Where(f => f.Classification != Classification.AutoFixable).Select(f => f.RuleId).ToHashSet();
        Assert.DoesNotContain(plan.Items, i => manual.Contains(i.RuleId) && !report.Findings.Any(f => f.RuleId == i.RuleId && f.Classification == Classification.AutoFixable));
    }

    [Fact]
    public async Task Legacy_fixture_plan_has_the_expected_rules()
    {
        var report = await AnalyzeAsync("xna31-legacy");

        var plan = new MigrationPlanner().CreatePlan(report, _none);

        var ids = plan.Items.Select(i => i.RuleId).ToList();
        foreach (var rule in new[] { "XNA001", "XNA003", "XNA004", "XNA022", "XNA024" })
        {
            Assert.Contains(rule, ids);
        }

        Assert.DoesNotContain("XNA023", ids);
        Assert.EndsWith("(2 places)", plan.Items.Single(i => i.RuleId == "XNA022").Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_project_of_a_solution_gets_its_own_item_and_can_be_excluded()
    {
        var report = await AnalyzeAsync("multi-project-sln");
        var planner = new MigrationPlanner();

        var plan = planner.CreatePlan(report, _none);
        var excluded = planner.CreatePlan(report, new HashSet<string> { "XNA001:Game/Game.csproj" });

        Assert.Equal(2, plan.Items.Count(i => i.RuleId == "XNA001"));
        Assert.Contains(plan.Items, i => i.Id == "XNA001:Game/Game.csproj");
        Assert.Equal(1, excluded.Items.Count(i => i.RuleId == "XNA001"));
        Assert.Contains(excluded.Items, i => i.Id.StartsWith("XNA001:Library/", StringComparison.Ordinal));
        Assert.Equal(["XNA001:Game/Game.csproj"], excluded.ExcludedItems.Where(i => i.RuleId == "XNA001").Select(i => i.Id));
    }
}
