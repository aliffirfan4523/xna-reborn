using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator;

/// <summary>Monogame-upgrade tasks 11 and 14: the old MonoGame flow of the project migrator, end to end, with both real catalogs.</summary>
[Trait("Category", "Migrator")]
public sealed class LegacyMonoGameMigrationTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-mgup-" + Guid.NewGuid().ToString("N"));

    public LegacyMonoGameMigrationTests()
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

    private string Out(string name = "out") => Path.Combine(_temp, name);

    private static string Rules(string file) => Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "..", "rules", file));

    internal static async Task<AnalysisReport> AnalyzeAsync(string path)
    {
        var catalogs = await RuleCatalogLoader.LoadAllAsync([Rules("xna-to-monogame.json"), Rules("monogame-upgrade.json")], new ReadOnlyFileSystem(), CancellationToken.None);
        return await new ProjectAnalyzer().AnalyzeAsync(path, catalogs, null, CancellationToken.None);
    }

    private static async Task<MigrationResult> MigrateAsync(string source, MigrationOptions options, bool confirmed = false, IProgress<MigrationProgress>? progress = null, CancellationToken ct = default)
    {
        var report = await AnalyzeAsync(source);
        return await new ProjectMigrator().MigrateAsync(source, report, options, confirmed, progress, ct);
    }

    private static MigrationOptions Options(string output, bool dryRun = false, MonoGameTarget? legacy = null, params string[] excluded) =>
        new(output, MonoGameTarget.DesktopGL, false, dryRun, new HashSet<string>(excluded, StringComparer.Ordinal), LegacyTarget: legacy);

    private static FileChange Single(MigrationResult result, string path) =>
        Assert.Single(result.Changes, c => c.RelativePath == path);

    [Fact]
    public async Task Windows_installer_project_becomes_a_WindowsDX_sdk_project()
    {
        var result = await MigrateAsync(FixturePaths.Of("mg37-windowsdx-pkgconfig"), Options(Out(), dryRun: true));

        var csproj = Single(result, "Mg37WindowsDx.csproj");
        Assert.Equal(ChangeKind.Created, csproj.Kind);
        Assert.Contains("MonoGame.Framework.WindowsDX", csproj.NewText);
        Assert.Contains("net8.0-windows", csproj.NewText);
        Assert.Equal(ChangeKind.Skipped, Single(result, "packages.config").Kind);
        Assert.Equal(ChangeKind.ManualWork, Single(result, "Content/Prebuilt/logo.xnb").Kind);
        Assert.Equal(ChangeKind.Copied, Single(result, "Game1.cs").Kind);
        Assert.Contains(result.Changes, c => c.RelativePath == ".config/dotnet-tools.json");
        Assert.Contains(result.Changes, c => c.RelativePath == "Directory.Build.props");
        Assert.Contains("MG001", result.Manifest.Changes.Single(c => c.Path == "Mg37WindowsDx.csproj").Rules);
        Assert.Equal(result.Changes.Select(c => c.RelativePath).Order(StringComparer.Ordinal), result.Changes.Select(c => c.RelativePath));
        Assert.False(result.Completed);
        Assert.False(Directory.Exists(Out()));
    }

    [Fact]
    public async Task Legacy_target_option_overrides_the_platform_choice()
    {
        var result = await MigrateAsync(FixturePaths.Of("mg37-windowsdx-pkgconfig"), Options(Out(), dryRun: true, legacy: MonoGameTarget.DesktopGL));

        var csproj = Single(result, "Mg37WindowsDx.csproj");
        Assert.Contains("MonoGame.Framework.DesktopGL", csproj.NewText);
        Assert.DoesNotContain("net8.0-windows", csproj.NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Desktop_installer_project_is_written_to_a_new_folder_and_the_source_is_unchanged()
    {
        var source = FixturePaths.Of("mg37-desktopgl-installdir");
        var before = MigrationTestSupport.Tree(source);

        var result = await MigrateAsync(source, Options(Out()));

        Assert.True(result.Completed);
        Assert.Equal(before, MigrationTestSupport.Tree(source));
        var tree = MigrationTestSupport.Tree(Out());
        Assert.Contains("Mg37DesktopGl.csproj", tree.Keys);
        Assert.Contains("Game1.cs", tree.Keys);
        Assert.Contains("Content/Content.mgcb", tree.Keys);
        Assert.Contains(ProjectMigrator.ManifestJsonName, tree.Keys);
        Assert.Contains(ProjectMigrator.ManifestMarkdownName, tree.Keys);
        Assert.DoesNotContain(tree.Keys, k => k.EndsWith("Mg37DesktopGl.sln", StringComparison.Ordinal));
        Assert.Contains("MonoGame.Framework.DesktopGL", File.ReadAllText(Path.Combine(Out(), "Mg37DesktopGl.csproj")));
    }

    [Fact]
    public async Task Manual_findings_become_notes_in_the_markdown_manifest()
    {
        var source = FixturePaths.Of("mg34-windowsgl");

        var result = await MigrateAsync(source, Options(Out()));

        var markdown = File.ReadAllText(Path.Combine(Out(), ProjectMigrator.ManifestMarkdownName));
        Assert.True(result.Completed);
        Assert.Contains("MG021", markdown, StringComparison.Ordinal);
        Assert.Contains("MG022", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Excluded_plan_item_is_not_in_the_plan()
    {
        var report = await AnalyzeAsync(FixturePaths.Of("mg34-windowsgl"));
        var item = new MigrationOptions(Out(), MonoGameTarget.DesktopGL, false, true, new HashSet<string>(StringComparer.Ordinal) { "MG003:Mg34WindowsGl.csproj" });

        var result = await new ProjectMigrator().MigrateAsync(FixturePaths.Of("mg34-windowsgl"), report, item, false, null, CancellationToken.None);

        Assert.DoesNotContain(result.Plan.Items, i => i.Id == "MG003:Mg34WindowsGl.csproj");
        Assert.Contains(result.Plan.ExcludedItems, i => i.Id == "MG003:Mg34WindowsGl.csproj");
        Assert.Contains(result.Plan.Items, i => i.RuleId == "MG011");
    }

    [Fact]
    public async Task Old_pin_project_is_bumped_and_its_folder_is_copied()
    {
        var source = FixturePaths.Of("mg38-old-pin");
        var before = MigrationTestSupport.Tree(source);

        var result = await MigrateAsync(source, Options(Out()));

        Assert.True(result.Completed);
        Assert.Equal(before, MigrationTestSupport.Tree(source));
        var csproj = File.ReadAllText(Path.Combine(Out(), "Mg38OldPin.csproj"));
        Assert.Contains("net8.0", csproj);
        Assert.DoesNotContain("netcoreapp3.1", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("3.8.0.1641", csproj, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Out(), "Game1.cs")));
        Assert.True(File.Exists(Path.Combine(Out(), "Content", "Content.mgcb")));
        Assert.True(File.Exists(Path.Combine(Out(), ".config", "dotnet-tools.json")));
        Assert.Equal(ChangeKind.Modified, Single(result, "Mg38OldPin.csproj").Kind);
    }

    [Fact]
    public async Task Multiplatform_solution_skips_platform_projects_links_shared_code_and_lists_one_project()
    {
        var source = FixturePaths.Of("mg37-multiplatform-sln");
        var before = MigrationTestSupport.Tree(source);

        var result = await MigrateAsync(Path.Combine(source, "Mg37Multi.sln"), Options(Out()));

        Assert.True(result.Completed);
        Assert.Equal(before, MigrationTestSupport.Tree(source));
        var desktop = Single(result, "Desktop/Mg37Desktop.csproj");
        Assert.Equal(ChangeKind.Created, desktop.Kind);
        Assert.Contains("<Compile Include=\"..\\Shared\\SharedGame.cs\" Link=\"SharedGame.cs\" />", desktop.NewText);
        Assert.Equal(ChangeKind.Copied, Single(result, "Shared/SharedGame.cs").Kind);
        foreach (var platformProject in new[] { "Android/Mg37Android.csproj", "Ios/Mg37Ios.csproj", "Uwp/Mg37Uwp.csproj" })
        {
            var change = Single(result, platformProject);
            Assert.Equal(ChangeKind.ManualWork, change.Kind);
            Assert.False(string.IsNullOrWhiteSpace(change.Reason));
        }

        var solution = Single(result, "Mg37Multi.sln").NewText!;
        Assert.Contains("Mg37Desktop.csproj", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("Mg37Android", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("Mg37Uwp", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("Mg37Ios", solution, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Plan.Items, i => i.Id.EndsWith(":Uwp/Mg37Uwp.csproj", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(Out(), "Uwp", "Content", "Content.mgcb")));
    }

    [Fact]
    public async Task Folder_of_current_projects_has_nothing_to_migrate()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => MigrateAsync(FixturePaths.Of("mg38-current"), Options(Out())));

        Assert.Contains("current", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Out()));
    }

    [Fact]
    public async Task Existing_non_empty_output_folder_is_refused_without_confirmation()
    {
        Directory.CreateDirectory(Out());
        await File.WriteAllTextAsync(Path.Combine(Out(), "keep.txt"), "mine");

        await Assert.ThrowsAsync<OutputGuardException>(() => MigrateAsync(FixturePaths.Of("mg34-windowsgl"), Options(Out())));

        Assert.Equal("mine", await File.ReadAllTextAsync(Path.Combine(Out(), "keep.txt")));
    }

    [Fact]
    public async Task Output_folder_inside_the_source_is_refused()
    {
        var source = Path.Combine(_temp, "src");
        MigrationTestSupport.CopyFolder(FixturePaths.Of("mg34-windowsgl"), source);

        await Assert.ThrowsAsync<OutputGuardException>(() => MigrateAsync(source, Options(Path.Combine(source, "out"))));
    }

    [Fact]
    public async Task Cancelled_run_leaves_no_output()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MigrateAsync(FixturePaths.Of("mg34-windowsgl"), Options(Out()), ct: cts.Token));

        Assert.False(Directory.Exists(Out()));
    }

    [Fact]
    public async Task Two_runs_write_the_same_bytes()
    {
        var source = FixturePaths.Of("mg37-desktopgl-installdir");

        await MigrateAsync(source, Options(Out("a")));
        await MigrateAsync(source, Options(Out("b")));

        Assert.Equal(MigrationTestSupport.Tree(Out("a")), MigrationTestSupport.Tree(Out("b")));
    }

    [Fact]
    public async Task Xna_project_still_takes_the_xna_flow()
    {
        var report = await MigrationTestSupport.AnalyzeAsync(FixturePaths.Of("xna40-basic"));

        var result = await new ProjectMigrator().MigrateAsync(FixturePaths.Of("xna40-basic"), report, MigrationTestSupport.Options(Out(), dryRun: true), false, null, CancellationToken.None);

        Assert.Contains(result.Plan.Items, i => i.RuleId.StartsWith("XNA", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Plan.Items, i => i.RuleId.StartsWith("MG", StringComparison.Ordinal));
    }
}
