using System.Security.Cryptography;
using System.Text.Json;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Source;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Safety;

namespace XnaReborn.Tests.Migrator;

/// <summary>Shared helpers for the end-to-end migrator tests.</summary>
internal static class MigrationTestSupport
{
    private static string CatalogPath => Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json"));

    internal static async Task<AnalysisReport> AnalyzeAsync(string path)
    {
        var catalog = await RuleCatalogLoader.LoadAsync(CatalogPath, new ReadOnlyFileSystem(), CancellationToken.None);
        return await new ProjectAnalyzer().AnalyzeAsync(path, catalog, null, CancellationToken.None);
    }

    internal static MigrationOptions Options(string output, bool dryRun = false, params string[] excluded) =>
        new(output, MonoGameTarget.DesktopGL, false, dryRun, new HashSet<string>(excluded, StringComparer.Ordinal));

    internal static async Task<MigrationResult> MigrateAsync(
        string source,
        MigrationOptions options,
        bool confirmedNonEmpty = false,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var report = await AnalyzeAsync(source);
        return await new ProjectMigrator().MigrateAsync(source, report, options, confirmedNonEmpty, progress, cancellationToken);
    }

    /// <summary>Maps each file under <paramref name="folder"/> to the SHA-256 of its bytes.</summary>
    internal static SortedDictionary<string, string> Tree(string folder) =>
        new(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .ToDictionary(
                f => Path.GetRelativePath(folder, f).Replace('\\', '/'),
                f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))),
                StringComparer.Ordinal), StringComparer.Ordinal);

    internal static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var directory in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)));
        }
    }
}

/// <summary>Task 7 to 9: the project migrator end to end, on the fixtures, with the real rule catalog.</summary>
[Trait("Category", "Migrator")]
public sealed class ProjectMigratorTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-migrator-" + Guid.NewGuid().ToString("N"));

    public ProjectMigratorTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private static string Fixture(string name) => FixturePaths.Of(name);

    private string Out(string name = "out") => Path.Combine(_temp, name);

    private static FileChange Single(MigrationResult result, string path) =>
        Assert.Single(result.Changes, c => c.RelativePath == path);

    [Fact]
    public void Default_rewriters_register_the_entry_point_and_the_four_rules()
    {
        Assert.Equal(["ENTRYPOINT", "XNA022", "XNA024", "XNA039", "XNA040"], DefaultRewriters.Create().RuleIds);
    }

    [Fact]
    public async Task Dry_run_on_the_rewritable_fixture_plans_every_change_and_writes_nothing()
    {
        var result = await MigrationTestSupport.MigrateAsync(Fixture("xna31-rewritable"), MigrationTestSupport.Options(Out(), dryRun: true));

        Assert.False(result.Completed);
        Assert.False(result.Manifest.Completed);
        Assert.Equal(ChangeKind.Created, Single(result, "Xna31Rewritable.csproj").Kind);

        var program = Single(result, "Program.cs");
        Assert.Equal(ChangeKind.Modified, program.Kind);
        Assert.DoesNotContain("#if WINDOWS || XBOX", program.NewText);

        var game = Single(result, "Game1.cs");
        Assert.Equal(ChangeKind.Modified, game.Kind);
        Assert.DoesNotContain("SpriteBlendMode", game.NewText);
        Assert.DoesNotContain(".RenderState.", game.NewText);
        Assert.DoesNotContain("pass.Begin()", game.NewText);
        Assert.DoesNotContain("effect.Begin()", game.NewText);

        Assert.Equal(ChangeKind.Copied, Single(result, "Properties/AssemblyInfo.cs").Kind);
        Assert.Equal(result.Changes.Select(c => c.RelativePath).Order(StringComparer.Ordinal), result.Changes.Select(c => c.RelativePath));
        Assert.False(Directory.Exists(Out()));
        Assert.Empty(Directory.GetFileSystemEntries(_temp));
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task Real_run_writes_a_complete_tree_and_leaves_the_source_untouched()
    {
        var source = Fixture("xna31-rewritable");
        var before = FolderState.Capture(source);

        var result = await MigrationTestSupport.MigrateAsync(source, MigrationTestSupport.Options(Out("first")));

        Assert.True(result.Completed);
        Assert.True(result.Manifest.Completed);
        var first = Out("first");
        foreach (var name in new[] { "Xna31Rewritable.csproj", "Program.cs", "Game1.cs", "Properties/AssemblyInfo.cs", "xnareborn-manifest.json", "xnareborn-manifest.md" })
        {
            Assert.True(File.Exists(Path.Combine(first, name)), name);
        }

        Assert.False(File.Exists(Path.Combine(first, "INCOMPLETE.txt")));
        Assert.Empty(Directory.GetDirectories(_temp, "*.xnareborn-incomplete"));

        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(first, "xnareborn-manifest.json")));
        Assert.True(json.RootElement.GetProperty("completed").GetBoolean());
        Assert.Contains("# XNA Reborn migration manifest", File.ReadAllText(Path.Combine(first, "xnareborn-manifest.md")), StringComparison.Ordinal);
        Assert.Equal(before, FolderState.Capture(source));

        await MigrationTestSupport.MigrateAsync(source, MigrationTestSupport.Options(Out("second")));
        Assert.Equal(MigrationTestSupport.Tree(first), MigrationTestSupport.Tree(Out("second")));
    }

    [Fact]
    public async Task Manifest_lists_the_rules_that_changed_each_file()
    {
        var result = await MigrationTestSupport.MigrateAsync(Fixture("xna31-rewritable"), MigrationTestSupport.Options(Out(), dryRun: true));

        Assert.Contains("ENTRYPOINT", Rules(result, "Program.cs"));
        var game = Rules(result, "Game1.cs");
        Assert.Contains("XNA022", game);
        Assert.Contains("XNA024", game);
        Assert.Contains("XNA039", game);
        var project = Rules(result, "Xna31Rewritable.csproj");
        Assert.Contains("XNA001", project);
        Assert.DoesNotContain("ENTRYPOINT", project);
        Assert.Empty(Rules(result, "Properties/AssemblyInfo.cs"));
    }

    private static IReadOnlyList<string> Rules(MigrationResult result, string path) =>
        result.Manifest.Changes.Single(c => c.Path == path).Rules;

    [Fact]
    public async Task Excluding_a_plan_item_keeps_that_rule_from_running()
    {
        var result = await MigrationTestSupport.MigrateAsync(
            Fixture("xna31-rewritable"),
            MigrationTestSupport.Options(Out(), dryRun: true, "XNA022:Xna31Rewritable.csproj"));

        var game = Single(result, "Game1.cs");
        Assert.Contains("SpriteBlendMode", game.NewText ?? string.Empty);
        Assert.DoesNotContain(".RenderState.", game.NewText);
        Assert.Single(result.Plan.ExcludedItems);
    }

    [Fact]
    public async Task Multi_project_solution_gets_two_projects_a_solution_sources_and_manual_content()
    {
        var output = Out();
        var result = await MigrationTestSupport.MigrateAsync(Fixture("multi-project-sln"), MigrationTestSupport.Options(output));

        var created = result.Changes.Where(c => c.Kind == ChangeKind.Created).Select(c => c.RelativePath).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["Game/Game.csproj", "Library/Library.csproj", "multi-project-sln.sln"], created);
        Assert.Contains(result.Changes, c => c.RelativePath == "Library/Helpers.cs");
        Assert.Contains(result.Changes, c => c.RelativePath == "Game/Game1.cs");

        var icon = Single(result, "GameContent/Textures/icon.png");
        Assert.Equal(ChangeKind.ManualWork, icon.Kind);
        Assert.Equal(Path.Combine(Fixture("multi-project-sln"), "GameContent", "Textures", "icon.png"), icon.SourcePath);
        Assert.DoesNotContain("Content.mgcb", Single(result, "Game/Game.csproj").NewText);

        Assert.True(File.Exists(Path.Combine(output, "GameContent", "Textures", "icon.png")));
        var markdown = File.ReadAllText(Path.Combine(output, "xnareborn-manifest.md"));
        Assert.Contains("GameContent/GameContent.contentproj: content conversion is not wired in yet", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Custom_content_fixture_copies_assets_as_manual_work_without_a_content_reference()
    {
        var result = await MigrationTestSupport.MigrateAsync(Fixture("xna40-custom-content"), MigrationTestSupport.Options(Out(), dryRun: true));

        var content = result.Changes.Where(c => c.RelativePath.StartsWith("Content/", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(content);
        Assert.All(content, c =>
        {
            Assert.Equal(ChangeKind.ManualWork, c.Kind);
            Assert.NotNull(c.SourcePath);
            Assert.Contains("not wired in yet", c.Reason, StringComparison.Ordinal);
        });
        Assert.Contains(content, c => c.RelativePath == "Content/Shaders/Glow.fx");

        var csproj = Single(result, "CustomContent.csproj").NewText!;
        Assert.DoesNotContain("Content.mgcb", csproj);
        Assert.DoesNotContain("MonoGameContentReference", csproj);
        Assert.DoesNotContain("MonoGame.Content.Builder", csproj);
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task Output_inside_the_source_is_refused_and_nothing_is_written()
    {
        var source = Path.Combine(_temp, "src");
        MigrationTestSupport.CopyFolder(Fixture("xna31-rewritable"), source);
        var before = FolderState.Capture(source);
        var inside = Path.Combine(source, "out");

        var real = await Assert.ThrowsAsync<OutputGuardException>(() => MigrationTestSupport.MigrateAsync(source, MigrationTestSupport.Options(inside)));
        var dry = await Assert.ThrowsAsync<OutputGuardException>(() => MigrationTestSupport.MigrateAsync(source, MigrationTestSupport.Options(inside, dryRun: true)));

        Assert.Equal(GuardProblem.OutputInsideSource, real.Result.Problem);
        Assert.Equal(GuardProblem.OutputInsideSource, dry.Result.Problem);
        Assert.False(Directory.Exists(inside));
        Assert.Equal(before, FolderState.Capture(source));
        Assert.DoesNotContain(Directory.GetDirectories(source), d => d.Contains("xnareborn", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task Non_empty_output_needs_confirmation()
    {
        var output = Out();
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "keep.txt"), "old");

        var ex = await Assert.ThrowsAsync<OutputGuardException>(
            () => MigrationTestSupport.MigrateAsync(Fixture("xna31-rewritable"), MigrationTestSupport.Options(output)));

        Assert.Equal(GuardProblem.OutputNotEmpty, ex.Result.Problem);
        Assert.Equal("old", File.ReadAllText(Path.Combine(output, "keep.txt")));
        Assert.Empty(Directory.GetDirectories(_temp, "*.xnareborn-incomplete"));

        var confirmed = await MigrationTestSupport.MigrateAsync(Fixture("xna31-rewritable"), MigrationTestSupport.Options(output), confirmedNonEmpty: true);

        Assert.True(confirmed.Completed);
        Assert.False(File.Exists(Path.Combine(output, "keep.txt")));
        Assert.Single(Directory.GetDirectories(_temp, "out.backup-*"));
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task Cancelling_while_rewriting_throws_and_never_creates_the_output()
    {
        using var cts = new CancellationTokenSource();
        var progress = new InlineProgress(p =>
        {
            if (p.Stage == "Rewriting source" && p.Completed == 1)
            {
                cts.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MigrationTestSupport.MigrateAsync(
            Fixture("xna31-rewritable"), MigrationTestSupport.Options(Out()), progress: progress, cancellationToken: cts.Token));

        Assert.False(Directory.Exists(Out()));
        Assert.Empty(Directory.GetFileSystemEntries(_temp));
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task Cancelling_while_writing_leaves_an_incomplete_staging_folder_and_no_output()
    {
        using var cts = new CancellationTokenSource();
        var progress = new InlineProgress(p =>
        {
            if (p.Stage == "Writing files")
            {
                cts.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MigrationTestSupport.MigrateAsync(
            Fixture("xna31-rewritable"), MigrationTestSupport.Options(Out()), progress: progress, cancellationToken: cts.Token));

        Assert.False(Directory.Exists(Out()));
        var staging = Assert.Single(Directory.GetDirectories(_temp));
        Assert.EndsWith(".xnareborn-incomplete", staging, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(staging, "INCOMPLETE.txt")));
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(staging, "xnareborn-manifest.json")));
        Assert.False(json.RootElement.GetProperty("completed").GetBoolean());
    }

    [Fact]
    public async Task A_source_without_xna_projects_is_refused_with_the_analyzer_reason()
    {
        var source = Fixture("not-xna");
        var report = await MigrationTestSupport.AnalyzeAsync(source);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new ProjectMigrator().MigrateAsync(
            source, report, MigrationTestSupport.Options(Out()), false, null, CancellationToken.None));

        Assert.False(string.IsNullOrWhiteSpace(report.NoProjectReason));
        Assert.Equal(report.NoProjectReason, ex.Message);
        Assert.False(Directory.Exists(Out()));
    }

    [Fact]
    public async Task Csproj_input_migrates_only_that_project_without_a_solution()
    {
        var result = await MigrationTestSupport.MigrateAsync(
            Path.Combine(Fixture("xna31-rewritable"), "Xna31Rewritable.csproj"),
            MigrationTestSupport.Options(Out(), dryRun: true));

        Assert.DoesNotContain(result.Changes, c => c.RelativePath.EndsWith(".sln", StringComparison.Ordinal));
        Assert.Equal(ChangeKind.Created, Single(result, "Xna31Rewritable.csproj").Kind);
    }

    [Fact]
    public async Task Sln_input_always_gets_a_solution_named_after_the_input()
    {
        var result = await MigrationTestSupport.MigrateAsync(
            Path.Combine(Fixture("multi-project-sln"), "MultiProject.sln"),
            MigrationTestSupport.Options(Out(), dryRun: true));

        Assert.Equal(ChangeKind.Created, Single(result, "MultiProject.sln").Kind);
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task Byte_order_mark_is_kept_and_a_missing_compile_file_is_skipped()
    {
        var source = Path.Combine(_temp, "src");
        MigrationTestSupport.CopyFolder(Fixture("xna31-rewritable"), source);
        var game = Path.Combine(source, "Game1.cs");
        File.WriteAllBytes(game, [0xEF, 0xBB, 0xBF, .. File.ReadAllBytes(game)]);
        var csproj = Path.Combine(source, "Xna31Rewritable.csproj");
        File.WriteAllText(csproj, File.ReadAllText(csproj).Replace(
            "<Compile Include=\"Game1.cs\" />",
            "<Compile Include=\"Game1.cs\" />\n    <Compile Include=\"Gone.cs\" />",
            StringComparison.Ordinal));

        var result = await MigrationTestSupport.MigrateAsync(source, MigrationTestSupport.Options(Out()));

        var gone = Single(result, "Gone.cs");
        Assert.Equal(ChangeKind.Skipped, gone.Kind);
        Assert.Contains("Gone.cs", gone.Reason, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Out(), "Gone.cs")));
        var bytes = File.ReadAllBytes(Path.Combine(Out(), "Game1.cs"));
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.NotEqual(0xBF, bytes[3]);
        Assert.DoesNotContain("SpriteBlendMode", File.ReadAllText(Path.Combine(Out(), "Game1.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Progress_reports_each_stage()
    {
        var stages = new List<string>();
        var progress = new InlineProgress(p => stages.Add(p.Stage));

        await MigrationTestSupport.MigrateAsync(Fixture("xna31-rewritable"), MigrationTestSupport.Options(Out(), dryRun: true), progress: progress);

        var distinct = stages.Distinct().ToList();
        Assert.Equal(["Discovering projects", "Planning", "Generating projects", "Rewriting source", "Done"], distinct);
    }

    private sealed class InlineProgress(Action<MigrationProgress> onReport) : IProgress<MigrationProgress>
    {
        public void Report(MigrationProgress value) => onReport(value);
    }
}
