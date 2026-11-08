using System.Security.Cryptography;
using System.Text.Json;
using XnaReborn.Analyzer;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Reporting;

namespace XnaReborn.Tests.Analysis;

/// <summary>End-to-end tests of <see cref="ProjectAnalyzer"/> over the synthetic fixtures.</summary>
[Trait("Category", "Analyzer")]
public sealed class ProjectAnalyzerTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xnareborn-analyzer-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the temp folder.</summary>
    public ProjectAnalyzerTests()
    {
        Directory.CreateDirectory(_temp);
    }

    /// <summary>Deletes the temp folder.</summary>
    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static Task<AnalysisReport> Analyze(string path, RuleCatalog? catalog = null, IProgress<AnalysisProgress>? progress = null, CancellationToken token = default) =>
        new ProjectAnalyzer().AnalyzeAsync(path, catalog ?? SampleCatalog.Create(), progress, token);

    private static string Serialize(AnalysisReport report) => JsonSerializer.Serialize(report);

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
    }

    private static Dictionary<string, string> HashAll(string folder) =>
        Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .ToDictionary(
                f => Path.GetRelativePath(folder, f),
                f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))),
                StringComparer.Ordinal);

    /// <summary>The basic fixture has two findings and known scores.</summary>
    [Fact]
    public async Task Basic_fixture_has_known_findings_and_scores()
    {
        var report = await Analyze(FixturePaths.Of("xna40-basic"));

        Assert.Equal(1, report.SchemaVersion);
        Assert.Equal("xna-test-sample", report.CatalogName);
        Assert.Equal(["XNA903", "XNA910"], report.Findings.Select(f => f.RuleId).Order(StringComparer.Ordinal));
        Assert.Equal(98, report.Score);
        Assert.Equal(99, report.ProjectedScore);
        Assert.Null(report.NoProjectReason);
        Assert.Single(report.Projects);
        Assert.Contains(report.Findings, f => f.RuleId == "XNA903" && f.Location is null);
    }

    /// <summary>A non-XNA project yields an empty, perfect report with the reason.</summary>
    [Fact]
    public async Task Non_xna_fixture_has_no_projects()
    {
        var report = await Analyze(FixturePaths.Of("not-xna"));

        Assert.Empty(report.Projects);
        Assert.Empty(report.Findings);
        Assert.Empty(report.TriggeredRules);
        Assert.Equal("Found 1 project(s), but none use XNA.", report.NoProjectReason);
        Assert.Equal(100, report.Score);
        Assert.Equal(100, report.ProjectedScore);
        AssertCountsComplete(report);
    }

    /// <summary>An empty folder yields the "no .csproj" reason.</summary>
    [Fact]
    public async Task Empty_folder_reports_no_csproj()
    {
        var report = await Analyze(FixturePaths.Of("empty-folder"));

        Assert.Empty(report.Projects);
        Assert.Equal("No .csproj files found.", report.NoProjectReason);
        Assert.Equal(100, report.Score);
    }

    /// <summary>A missing path produces a "Path not found" reason.</summary>
    [Fact]
    public async Task Missing_path_reports_not_found()
    {
        var missing = Path.Combine(_temp, "nope");
        var report = await Analyze(missing);

        Assert.Empty(report.Projects);
        Assert.Equal($"Path not found: {missing}", report.NoProjectReason);
    }

    /// <summary>A multi-project solution lists both projects and keeps its diagnostic.</summary>
    [Fact]
    public async Task Multi_project_solution_lists_projects_and_diagnostic()
    {
        var report = await Analyze(FixturePaths.Of("multi-project-sln"));

        Assert.Equal(["Game/Game.csproj", "Library/Library.csproj"], report.Projects.Select(p => p.Path));
        Assert.Single(report.Diagnostics, d => d.Code == "ANA006");
    }

    /// <summary>Gamer services blockers come first.</summary>
    [Fact]
    public async Task Gamer_services_fixture_orders_blockers_first()
    {
        var report = await Analyze(FixturePaths.Of("xna40-gamerservices"));

        var ids = report.Findings.Select(f => f.RuleId).Distinct().ToList();
        Assert.Equal(["XNA901", "XNA909"], ids.Take(2).Order(StringComparer.Ordinal));
        Assert.Contains("XNA902", ids);
        Assert.Equal(Severity.Blocker, report.Findings[0].Severity);
    }

    /// <summary>The legacy 3.1 fixture triggers its four rules.</summary>
    [Fact]
    public async Task Legacy_fixture_triggers_expected_rules()
    {
        var report = await Analyze(FixturePaths.Of("xna31-legacy"));

        var ids = report.TriggeredRules.Select(r => r.Id).ToHashSet();
        Assert.Superset(new HashSet<string> { "XNA903", "XNA904", "XNA905", "XNA906" }, ids);
    }

    /// <summary>The custom content fixture triggers its four rules.</summary>
    [Fact]
    public async Task Custom_content_fixture_triggers_expected_rules()
    {
        var report = await Analyze(FixturePaths.Of("xna40-custom-content"));

        var ids = report.TriggeredRules.Select(r => r.Id).ToHashSet();
        Assert.Superset(new HashSet<string> { "XNA903", "XNA907", "XNA908", "XNA910" }, ids);
    }

    /// <summary>Folder, solution and project inputs all work.</summary>
    [Fact]
    public async Task Folder_sln_and_csproj_inputs_work()
    {
        var folder = FixturePaths.Of("multi-project-sln");
        var byFolder = await Analyze(folder);
        var bySln = await Analyze(Path.Combine(folder, "MultiProject.sln"));
        var byProject = await Analyze(Path.Combine(folder, "Game", "Game.csproj"));

        Assert.Equal(byFolder.Projects.Select(p => p.Path), bySln.Projects.Select(p => p.Path));
        Assert.Single(byProject.Projects);
        Assert.Equal("Game.csproj", byProject.Projects[0].Path);
    }

    /// <summary>An empty catalog never produces findings.</summary>
    [Theory]
    [InlineData("xna31-legacy")]
    [InlineData("xna40-basic")]
    [InlineData("xna40-custom-content")]
    [InlineData("xna40-gamerservices")]
    public async Task Empty_catalog_gives_no_findings(string fixture)
    {
        var report = await Analyze(FixturePaths.Of(fixture), new RuleCatalog(1, "empty", null, []));

        Assert.NotEmpty(report.Projects);
        Assert.Empty(report.Findings);
        Assert.Empty(report.TriggeredRules);
        Assert.Equal(100, report.Score);
        AssertCountsComplete(report);
    }

    /// <summary>Findings are sorted by severity, rule, file, line and column.</summary>
    [Theory]
    [MemberData(nameof(FixturePaths.NameRows), MemberType = typeof(FixturePaths))]
    public async Task Findings_are_sorted(string fixture)
    {
        var report = await Analyze(FixturePaths.Of(fixture));

        var expected = report.Findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ThenBy(f => f.Location is null ? 0 : 1)
            .ThenBy(f => f.Location is null ? f.Project : f.Location.File, StringComparer.Ordinal)
            .ThenBy(f => f.Location?.Line ?? 0)
            .ThenBy(f => f.Location?.Column ?? 0)
            .ToList();
        Assert.Equal(expected.Select(f => (f.RuleId, f.Project, f.Location)), report.Findings.Select(f => (f.RuleId, f.Project, f.Location)));
    }

    /// <summary>Triggered rules are unique, sorted, and match the finding rule ids.</summary>
    [Theory]
    [MemberData(nameof(FixturePaths.NameRows), MemberType = typeof(FixturePaths))]
    public async Task Triggered_rules_are_unique_and_sorted(string fixture)
    {
        var report = await Analyze(FixturePaths.Of(fixture));

        var ids = report.TriggeredRules.Select(r => r.Id).ToList();
        Assert.Equal(ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), ids);
        Assert.Equal(report.Findings.Select(f => f.RuleId).Distinct().Order(StringComparer.Ordinal), ids);
    }

    /// <summary>Count dictionaries hold every enum key in order and sum to the finding count.</summary>
    [Theory]
    [MemberData(nameof(FixturePaths.NameRows), MemberType = typeof(FixturePaths))]
    public async Task Counts_have_all_keys_and_sum_to_findings(string fixture)
    {
        var report = await Analyze(FixturePaths.Of(fixture));

        AssertCountsComplete(report);
    }

    /// <summary>Diagnostics are unique and ordered.</summary>
    [Theory]
    [MemberData(nameof(FixturePaths.NameRows), MemberType = typeof(FixturePaths))]
    public async Task Diagnostics_are_unique_and_sorted(string fixture)
    {
        var report = await Analyze(FixturePaths.Of(fixture));

        Assert.Equal(report.Diagnostics.Distinct().Count(), report.Diagnostics.Count);
        Assert.Equal(
            report.Diagnostics.OrderBy(d => d.Code, StringComparer.Ordinal).ThenBy(d => d.File, StringComparer.Ordinal).ThenBy(d => d.Message, StringComparer.Ordinal),
            report.Diagnostics);
    }

    /// <summary>The same fixture in two different folders gives equal reports without absolute paths.</summary>
    [Fact]
    public async Task Report_is_independent_of_location()
    {
        var a = Path.Combine(_temp, "one", "alpha");
        var b = Path.Combine(_temp, "two", "beta", "deeper");
        CopyDirectory(FixturePaths.Of("xna40-custom-content"), a);
        CopyDirectory(FixturePaths.Of("xna40-custom-content"), b);

        var first = Serialize(await Analyze(a));
        var second = Serialize(await Analyze(b));

        Assert.Equal(first, second);
        Assert.DoesNotContain(_temp.Replace("\\", "\\\\", StringComparison.Ordinal), first, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Two runs on the same input give equal reports.</summary>
    [Fact]
    public async Task Two_runs_are_equal()
    {
        var path = FixturePaths.Of("multi-project-sln");

        Assert.Equal(Serialize(await Analyze(path)), Serialize(await Analyze(path)));
    }

    /// <summary>Progress passes through the stages and ends with Done.</summary>
    [Fact]
    public async Task Progress_reaches_done()
    {
        var updates = new List<AnalysisProgress>();
        await Analyze(FixturePaths.Of("xna40-basic"), progress: new ListProgress(updates));

        Assert.Equal(new AnalysisProgress("Done", 1, 1, null), updates[^1]);
        Assert.Contains(updates, u => u.Stage == "Discovering projects");
        Assert.Contains(updates, u => u.Stage == "Reading projects" && u.Current == "Xna40Basic.csproj");
        Assert.Contains(updates, u => u.Stage == "Scanning source");
        Assert.Single(updates, u => u.Stage == "Done");
    }

    /// <summary>An already-cancelled token throws.</summary>
    [Fact]
    public async Task Cancelled_token_throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Analyze(FixturePaths.Of("xna40-basic"), token: cts.Token));
    }

    /// <summary>Analysis leaves every file byte-identical and adds no files.</summary>
    [Fact]
    [Trait("Category", "Safety")]
    public async Task Analysis_leaves_files_unchanged()
    {
        var folder = Path.Combine(_temp, "copy");
        CopyDirectory(FixturePaths.Of("xna40-custom-content"), folder);
        var before = HashAll(folder);
        var entriesBefore = Directory.GetFileSystemEntries(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();

        await Analyze(folder);

        Assert.Equal(before, HashAll(folder));
        Assert.Equal(entriesBefore, Directory.GetFileSystemEntries(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
    }

    private static void AssertCountsComplete(AnalysisReport report)
    {
        Assert.Equal(Enum.GetValues<Severity>(), report.CountsBySeverity.Keys);
        Assert.Equal(Enum.GetValues<Classification>(), report.CountsByClassification.Keys);
        Assert.Equal(report.Findings.Count, report.CountsBySeverity.Values.Sum());
        Assert.Equal(report.Findings.Count, report.CountsByClassification.Values.Sum());
    }

    private sealed class ListProgress(List<AnalysisProgress> sink) : IProgress<AnalysisProgress>
    {
        public void Report(AnalysisProgress value) => sink.Add(value);
    }
}
