using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Migrator;
using XnaReborn.Tests.Safety;

namespace XnaReborn.Tests.Device;

/// <summary>Device-to-desktop task 5: choosing one project per group of platform copies, and migrating only that one.</summary>
[Trait("Category", "Migrator")]
public sealed class PlatformCopyPlanTests : IDisposable
{
    private static readonly IReadOnlySet<string> _none = new HashSet<string>();

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-copies-" + Guid.NewGuid().ToString("N"));

    public PlatformCopyPlanTests() => Directory.CreateDirectory(_temp);

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private static ProjectInfo P(string path, XnaPlatform platform, params string[] links) =>
        new(path, path, true, XnaVersion.V40, platform, null, null, [], [], [], [], [], false, links.Length == 0 ? null : links);

    private static Finding Copy(string project) =>
        new("XNA052", "Platform copy", Severity.Info, Classification.AutoFixable, project, null, null);

    private static Finding Device(string project) =>
        new("XNA007", "Device project", Severity.Warning, Classification.AutoFixable, project, null, null);

    private static AnalysisReport Report(IReadOnlyList<ProjectInfo> projects, params Finding[] findings) =>
        new(
            1,
            "test",
            projects,
            findings,
            findings.Select(f => f.RuleId).Distinct().Select(id => new TriggeredRule(id, "Title " + id, "why", "Fix " + id, null)).ToList(),
            new Dictionary<Severity, int>(),
            new Dictionary<Classification, int>(),
            0,
            0,
            [],
            null);

    // ---------- Find ----------
    [Fact]
    public void A_windows_project_and_its_copies_keep_the_windows_project()
    {
        var report = Report(
            [P("Game/Game.csproj", XnaPlatform.Windows), P("X360/X.csproj", XnaPlatform.Xbox360, "Game/Game.csproj"), P("Phone/P.csproj", XnaPlatform.WindowsPhone, "Game/Game.csproj")],
            Copy("X360/X.csproj"),
            Copy("Phone/P.csproj"));

        var plan = PlatformCopies.Find(report);

        Assert.Equal(["Phone/P.csproj", "X360/X.csproj"], plan.Skips.Select(s => s.Project));
        Assert.All(plan.Skips, s => Assert.Equal("Game/Game.csproj", s.Kept));
        Assert.All(plan.Skips, s => Assert.Equal("Game/Game.csproj", s.Duplicates));
        Assert.All(plan.Skips, s => Assert.StartsWith("platform copy of Game/Game.csproj", s.Reason, StringComparison.Ordinal));
        Assert.Empty(plan.Notes);
    }

    [Fact]
    public void Device_only_groups_choose_phone_then_xbox_then_zune_and_say_why()
    {
        var report = Report(
            [P("Z/Z.csproj", XnaPlatform.Zune, "O/O.csproj"), P("O/O.csproj", XnaPlatform.Zune), P("X/X.csproj", XnaPlatform.Xbox360, "O/O.csproj"), P("W/W.csproj", XnaPlatform.WindowsPhone, "O/O.csproj")],
            Copy("Z/Z.csproj"),
            Copy("X/X.csproj"),
            Copy("W/W.csproj"));

        var plan = PlatformCopies.Find(report);

        Assert.Equal(["O/O.csproj", "X/X.csproj", "Z/Z.csproj"], plan.Skips.Select(s => s.Project));
        Assert.All(plan.Skips, s => Assert.Equal("W/W.csproj", s.Kept));
        var note = Assert.Single(plan.Notes);
        Assert.StartsWith("W/W.csproj (WindowsPhone) was chosen from", note, StringComparison.Ordinal);
        Assert.Equal("shares its code with the platform copy W/W.csproj, which is migrated instead", plan.Skips.Single(s => s.Project == "O/O.csproj").Reason);
    }

    [Fact]
    public void Xbox_beats_zune_when_there_is_no_phone_project()
    {
        var report = Report([P("A/A.csproj", XnaPlatform.Zune), P("B/B.csproj", XnaPlatform.Xbox360, "A/A.csproj")], Copy("B/B.csproj"));

        var skip = Assert.Single(PlatformCopies.Find(report).Skips);

        Assert.Equal("A/A.csproj", skip.Project);
        Assert.Equal("B/B.csproj", skip.Kept);
    }

    [Fact]
    public void A_link_cycle_is_not_a_copy()
    {
        var report = Report(
            [P("A/A.csproj", XnaPlatform.Windows, "B/B.csproj"), P("B/B.csproj", XnaPlatform.Xbox360, "A/A.csproj")],
            Copy("A/A.csproj"),
            Copy("B/B.csproj"));

        Assert.Empty(PlatformCopies.Find(report).Skips);
    }

    [Fact]
    public void Linking_without_the_copy_rule_is_not_a_copy()
    {
        // The analyzer found links, but fewer than half of the Compile items (no XNA052), so the project stays.
        var report = Report([P("A/A.csproj", XnaPlatform.Windows), P("B/B.csproj", XnaPlatform.Windows, "A/A.csproj")]);

        Assert.Empty(PlatformCopies.Find(report).Skips);
    }

    [Fact]
    public void A_copy_whose_original_is_not_in_the_report_stays()
    {
        var report = Report([P("B/B.csproj", XnaPlatform.Xbox360, "Missing/M.csproj")], Copy("B/B.csproj"));

        Assert.Empty(PlatformCopies.Find(report).Skips);
    }

    [Fact]
    public void A_chain_of_copies_is_one_group()
    {
        var report = Report(
            [P("A/A.csproj", XnaPlatform.Windows), P("B/B.csproj", XnaPlatform.Xbox360, "A/A.csproj"), P("C/C.csproj", XnaPlatform.Zune, "B/B.csproj")],
            Copy("B/B.csproj"),
            Copy("C/C.csproj"));

        var plan = PlatformCopies.Find(report);

        Assert.Equal(["B/B.csproj", "C/C.csproj"], plan.Skips.Select(s => s.Project));
        Assert.All(plan.Skips, s => Assert.Equal("A/A.csproj", s.Kept));
        Assert.Equal("B/B.csproj", plan.Skips.Single(s => s.Project == "C/C.csproj").Duplicates);
    }

    // ---------- planner ----------
    [Fact]
    public void The_planner_adds_one_skip_item_per_skipped_project_and_hides_that_projects_other_items()
    {
        var report = Report(
            [P("Game/Game.csproj", XnaPlatform.Windows), P("X360/X.csproj", XnaPlatform.Xbox360, "Game/Game.csproj")],
            Copy("X360/X.csproj"),
            Device("X360/X.csproj"));

        var plan = new MigrationPlanner().CreatePlan(report, _none);

        var item = Assert.Single(plan.Items);
        Assert.Equal("XNA052:X360/X.csproj", item.Id);
        Assert.Equal("XNA052", item.RuleId);
        Assert.Equal(["X360/X.csproj"], item.AffectedFiles);
        Assert.Contains("platform copy of Game/Game.csproj", item.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Excluding_the_skip_item_migrates_the_copy_and_brings_its_items_back()
    {
        var report = Report(
            [P("Game/Game.csproj", XnaPlatform.Windows), P("X360/X.csproj", XnaPlatform.Xbox360, "Game/Game.csproj")],
            Copy("X360/X.csproj"),
            Device("X360/X.csproj"));

        var plan = new MigrationPlanner().CreatePlan(report, new HashSet<string> { "XNA052:X360/X.csproj" });

        Assert.Equal(["XNA007:X360/X.csproj"], plan.Items.Select(i => i.Id));
        Assert.Equal(["XNA052:X360/X.csproj"], plan.ExcludedItems.Select(i => i.Id));
    }

    [Fact]
    public void A_copy_rule_finding_without_a_group_gives_no_plan_item()
    {
        var report = Report([P("B/B.csproj", XnaPlatform.Xbox360, "Missing/M.csproj")], Copy("B/B.csproj"));

        Assert.Empty(new MigrationPlanner().CreatePlan(report, _none).Items);
    }

    [Fact]
    public void The_device_project_item_names_the_symbols_that_become_true()
    {
        var report = Report([P("P/P.csproj", XnaPlatform.WindowsPhone)], Device("P/P.csproj"));

        var item = Assert.Single(new MigrationPlanner().CreatePlan(report, _none).Items);

        Assert.EndsWith("Platform WindowsPhone: after conversion the #if symbols WINDOWS_PHONE are true and the other platform symbols are false.", item.Description, StringComparison.Ordinal);
    }

    // ---------- migration of the fixtures ----------
    [Fact]
    public async Task Migrating_the_copy_solution_skips_the_xbox_project_and_leaves_the_source_alone()
    {
        var source = FixturePaths.Of("device-xbox360-copy-solution");
        var before = FolderState.Capture(source);

        var result = await MigrationTestSupport.MigrateAsync(source, MigrationTestSupport.Options(Path.Combine(_temp, "out"), dryRun: true));

        var skipped = Assert.Single(result.Changes, c => c.Kind == ChangeKind.Skipped);
        Assert.Equal("Xbox 360 Copy of Game/Xbox 360 Copy of Game.csproj", skipped.RelativePath);
        Assert.Equal("platform copy of Game/Game.csproj; Game/Game.csproj is migrated instead", skipped.Reason);
        Assert.Equal(["XNA052"], result.Manifest.Changes.Single(c => c.Path == skipped.RelativePath).Rules);
        Assert.Equal(ChangeKind.Created, Assert.Single(result.Changes, c => c.RelativePath == "Game/Game.csproj").Kind);
        Assert.DoesNotContain(result.Changes, c => c.RelativePath.StartsWith("Xbox 360 Copy of Game/", StringComparison.Ordinal) && c.Kind != ChangeKind.Skipped);

        var program = Assert.Single(result.Changes, c => c.RelativePath == "Game/Program.cs");
        Assert.Equal(ChangeKind.Modified, program.Kind);
        Assert.DoesNotContain("#if", program.NewText, StringComparison.Ordinal);
        Assert.Equal(before, FolderState.Capture(source));
    }

    [Fact]
    public async Task Excluding_the_skip_item_migrates_both_projects()
    {
        var source = FixturePaths.Of("device-xbox360-copy-solution");

        var result = await MigrationTestSupport.MigrateAsync(
            source,
            MigrationTestSupport.Options(Path.Combine(_temp, "out"), dryRun: true, "XNA052:Xbox 360 Copy of Game/Xbox 360 Copy of Game.csproj"));

        Assert.DoesNotContain(result.Changes, c => c.Kind == ChangeKind.Skipped);
        Assert.Contains(result.Changes, c => c.RelativePath == "Xbox 360 Copy of Game/Xbox 360 Copy of Game.csproj" && c.Kind == ChangeKind.Created);
    }

    [Fact]
    public async Task A_phone_project_has_its_phone_branches_kept_when_migrated()
    {
        var source = FixturePaths.Of("device-wp7-xna");

        var result = await MigrationTestSupport.MigrateAsync(source, MigrationTestSupport.Options(Path.Combine(_temp, "out"), dryRun: true));

        var game = Assert.Single(result.Changes, c => c.RelativePath == "Game1.cs");
        Assert.Equal(ChangeKind.Modified, game.Kind);
        Assert.DoesNotContain("#if", game.NewText, StringComparison.Ordinal);
        Assert.Contains("graphics.IsFullScreen = true;", game.NewText, StringComparison.Ordinal);
        Assert.DoesNotContain("IsFullScreen = false", game.NewText, StringComparison.Ordinal);
        Assert.Contains("XNA053", result.Manifest.Changes.Single(c => c.Path == "Game1.cs").Rules);
    }
}
