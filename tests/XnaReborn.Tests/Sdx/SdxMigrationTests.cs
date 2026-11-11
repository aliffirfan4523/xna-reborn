using VerifyXunit;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;
using XnaReborn.Migrator.Projects;
using XnaReborn.Migrator.Source;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Migrator;
using XnaReborn.Tests.Safety;

namespace XnaReborn.Tests.Sdx;

/// <summary>Tasks 8 and 10: project generation, the migrator on Toolkit projects, and the manifest.</summary>
[Trait("Category", "Sdx")]
public sealed class SdxMigrationTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-sdx-" + Guid.NewGuid().ToString("N"));

    public SdxMigrationTests()
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

    private static async Task<AnalysisReport> AnalyzeAsync(string path)
    {
        var root = Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "..", "rules"));
        var catalogs = await RuleCatalogLoader.LoadAllAsync([Path.Combine(root, "xna-to-monogame.json"), Path.Combine(root, "sharpdx-toolkit-to-monogame.json")], new ReadOnlyFileSystem(), CancellationToken.None);
        return await new ProjectAnalyzer().AnalyzeAsync(path, catalogs, null, CancellationToken.None);
    }

    private static MigrationOptions Options(string output, bool dryRun = false, MonoGameTarget target = MonoGameTarget.WindowsDX, params string[] excluded) =>
        new(output, target, false, dryRun, new HashSet<string>(excluded, StringComparer.Ordinal));

    private async Task<(MigrationResult Result, string Output)> MigrateAsync(string fixture, MonoGameTarget target = MonoGameTarget.WindowsDX, string? source = null)
    {
        var path = source ?? Fixture(fixture);
        var output = Path.Combine(_temp, "out-" + Guid.NewGuid().ToString("N"));
        var report = await AnalyzeAsync(path);
        var result = await new ProjectMigrator().MigrateAsync(path, report, Options(output, false, target), false, null, CancellationToken.None);
        return (result, output);
    }

    private static FileChange Change(MigrationResult result, string path) =>
        Assert.Single(result.Changes, c => c.RelativePath == path);

    private static async Task<ProjectReadResult> ReadAsync(string fixture, string project) =>
        await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(Fixture(fixture), project), Fixture(fixture), CancellationToken.None);

    // ---------- 8.1 project generation ----------
    [Theory]
    [InlineData("sdx-basic", "SdxBasic.csproj", "Content/Content.mgcb")]
    [InlineData("sdx-effects", "SdxEffects.csproj", "Content/Content.mgcb")]
    public async Task Generated_project_matches_snapshot_and_has_nothing_of_sharpdx(string fixture, string project, string mgcb)
    {
        var read = await ReadAsync(fixture, project);

        var change = new SdkCsprojGenerator().Generate(read, MonoGameTarget.WindowsDX, mgcb);

        Assert.Equal(ChangeKind.Created, change.Kind);
        foreach (var text in new[] { "SharpDX", "Toolkit", "packages.config", "NuGetPackageImportStamp", "EnsureNuGetPackageBuildImports", "HintPath", "v4.5" })
        {
            Assert.DoesNotContain(text, change.NewText, StringComparison.Ordinal);
        }

        Assert.Contains("<PackageReference Include=\"MonoGame.Framework.WindowsDX\"", change.NewText, StringComparison.Ordinal);
        Assert.Contains("<MonoGameContentReference Include=\"Content\\Content.mgcb\" />", change.NewText, StringComparison.Ordinal);
        await Verifier.Verify(change.NewText, "xml").UseParameters(fixture);
    }

    [Fact]
    public async Task A_toolkit_project_without_converted_content_has_no_content_reference()
    {
        var read = await ReadAsync("sdx-lowlevel", "SdxLowLevel.csproj");

        var change = new SdkCsprojGenerator().Generate(read, MonoGameTarget.WindowsDX);

        Assert.DoesNotContain("MonoGameContentReference", change.NewText, StringComparison.Ordinal);
        Assert.DoesNotContain("MonoGame.Content.Builder.Task", change.NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_xna_project_keeps_a_sharpdx_reference_it_has_with_a_hint_path()
    {
        var folder = Path.Combine(_temp, "xnasharpdx");
        MigrationTestSupport.CopyFolder(Fixture("xna40-basic"), folder);
        var csproj = Path.Combine(folder, "Xna40Basic.csproj");
        File.WriteAllText(csproj, File.ReadAllText(csproj).Replace("<Reference Include=\"System\" />", "<Reference Include=\"System\" /><Reference Include=\"SharpDX.XInput\"><HintPath>..\\lib\\SharpDX.XInput.dll</HintPath></Reference>", StringComparison.Ordinal));
        var read = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(csproj, folder, CancellationToken.None);

        var change = new SdkCsprojGenerator().Generate(read, MonoGameTarget.DesktopGL);

        Assert.Contains("<Reference Include=\"SharpDX.XInput\">", change.NewText, StringComparison.Ordinal);
    }

    // ---------- 8.2 defaults, entry point, mixed solution ----------
    [Fact]
    public async Task Windowsdx_is_the_default_target_for_a_toolkit_source_and_desktopgl_for_xna()
    {
        var toolkit = await AnalyzeAsync(Fixture("sdx-basic"));
        var xna = await AnalyzeAsync(Fixture("xna40-basic"));
        var mixed = await AnalyzeAsync(Fixture("sdx-mixed"));

        Assert.Equal(MonoGameTarget.WindowsDX, MigrationDefaults.TargetFor(toolkit));
        Assert.Equal(MonoGameTarget.WindowsDX, MigrationDefaults.TargetFor(mixed));
        Assert.Equal(MonoGameTarget.DesktopGL, MigrationDefaults.TargetFor(xna));
        Assert.Equal(MonoGameTarget.DesktopGL, MigrationDefaults.TargetFor(null));
    }

    [Theory]
    [InlineData("#if NETFX_CORE\n        [MTAThread]\n#else\n        [STAThread]\n#endif\n")]
    [InlineData("#if NETFX_CORE || WINDOWS_APP\n        [MTAThread]\n#else\n        [STAThread]\n#endif\n")]
    public void The_toolkit_template_entry_point_with_netfx_core_is_left_alone_without_a_note(string attributes)
    {
        var text = "using System;\nclass Program\n{\n" + attributes + "    static void Main()\n    {\n        using (var program = new Game1())\n            program.Run();\n    }\n}\n";

        var result = new EntryPointSourceRewriter().Rewrite(new SourceRewriteContext("Program.cs", text, new[] { "DEBUG", "TRACE" }));

        Assert.Equal(text, result.Text);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void An_entry_point_in_a_netfx_core_block_with_a_platform_block_around_it_is_still_unwrapped()
    {
        var text = "using System;\nclass Program\n{\n#if WINDOWS || XBOX\n#if NETFX_CORE\n    [MTAThread]\n#else\n    [STAThread]\n#endif\n    static void Main() { }\n#endif\n}\n";

        var result = new EntryPointSourceRewriter().Rewrite(new SourceRewriteContext("Program.cs", text, new[] { "WINDOWS" }));

        Assert.DoesNotContain("#if WINDOWS || XBOX", result.Text, StringComparison.Ordinal);
        Assert.Contains("#if NETFX_CORE", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_migrated_sdx_basic_project_has_the_expected_files_and_nothing_from_sharpdx()
    {
        var (result, output) = await MigrateAsync("sdx-basic");

        Assert.True(result.Completed);
        var tree = MigrationTestSupport.Tree(output).Keys.Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            new[]
            {
                "Content/Arial16.spritefont",
                "Content/Balls.dds",
                "Content/Content.mgcb",
                ".config/dotnet-tools.json",
                "Directory.Build.props",
                "Directory.Build.targets",
                "Directory.Packages.props",
                "Game1.cs",
                "Program.cs",
                "Properties/AssemblyInfo.cs",
                "SdxBasic.csproj",
                "xnareborn-manifest.json",
                "xnareborn-manifest.md",
            }.Order(StringComparer.Ordinal),
            tree);

        var game = File.ReadAllText(Path.Combine(output, "Game1.cs"));
        Assert.DoesNotContain("SharpDX", game, StringComparison.Ordinal);
        Assert.Contains("using Microsoft.Xna.Framework;", game, StringComparison.Ordinal);
        Assert.Contains("Keyboard.GetState()", game, StringComparison.Ordinal);
        var csproj = File.ReadAllText(Path.Combine(output, "SdxBasic.csproj"));
        Assert.Contains("net8.0-windows", csproj, StringComparison.Ordinal);
        Assert.Contains("MonoGame.Framework.WindowsDX", csproj, StringComparison.Ordinal);
        Assert.Contains("MonoGameContentReference", csproj, StringComparison.Ordinal);
        var mgcb = File.ReadAllText(Path.Combine(output, "Content", "Content.mgcb"));
        Assert.Contains("/build:Arial16.spritefont", mgcb, StringComparison.Ordinal);
        Assert.Contains("/importer:TextureImporter", mgcb, StringComparison.Ordinal);
        Assert.Contains("/platform:Windows", mgcb, StringComparison.Ordinal);
        Assert.DoesNotContain("packages.config", string.Join('\n', tree), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_migrated_source_of_sdx_basic_compiles_against_monogame_when_it_is_cached()
    {
        if (Device.MonoGameFactAttribute.Path() is null)
        {
            return;
        }

        var (_, output) = await MigrateAsync("sdx-basic");

        var errors = SdxPipeline.CompileErrors(
            ("Game1.cs", File.ReadAllText(Path.Combine(output, "Game1.cs"))),
            ("Program.cs", File.ReadAllText(Path.Combine(output, "Program.cs"))));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_dry_run_of_a_toolkit_project_writes_nothing()
    {
        var output = Path.Combine(_temp, "dry");
        var report = await AnalyzeAsync(Fixture("sdx-basic"));

        var result = await new ProjectMigrator().MigrateAsync(Fixture("sdx-basic"), report, Options(output, dryRun: true), false, null, CancellationToken.None);

        Assert.False(result.Completed);
        Assert.False(Directory.Exists(output));
        Assert.Equal(ChangeKind.Created, Change(result, "SdxBasic.csproj").Kind);
        Assert.Equal(ChangeKind.Modified, Change(result, "Game1.cs").Kind);
        Assert.Equal(ChangeKind.Created, Change(result, "Content/Arial16.spritefont").Kind);
        Assert.Equal(ChangeKind.Copied, Change(result, "Content/Balls.dds").Kind);
    }

    [Fact]
    public async Task The_source_project_is_not_changed_by_a_migration()
    {
        var before = MigrationTestSupport.Tree(Fixture("sdx-basic"));

        await MigrateAsync("sdx-basic");

        Assert.Equal(before, MigrationTestSupport.Tree(Fixture("sdx-basic")));
    }

    [Fact]
    public async Task A_mixed_solution_gives_toolkit_handling_to_the_toolkit_project_only()
    {
        var (result, output) = await MigrateAsync("sdx-mixed", MonoGameTarget.DesktopGL);

        Assert.True(result.Completed);
        var toolkit = File.ReadAllText(Path.Combine(output, "Toolkit", "ToolkitGame.csproj"));
        var xna = File.ReadAllText(Path.Combine(output, "Xna", "XnaGame.csproj"));
        Assert.Contains("MonoGameContentReference", toolkit, StringComparison.Ordinal);
        Assert.DoesNotContain("SharpDX", toolkit, StringComparison.Ordinal);
        Assert.Contains("MonoGameContentReference", xna, StringComparison.Ordinal);
        Assert.DoesNotContain("Toolkit", xna, StringComparison.Ordinal);
        Assert.Contains("Keys.Escape", File.ReadAllText(Path.Combine(output, "Xna", "Game1.cs")), StringComparison.Ordinal);
        var game = File.ReadAllText(Path.Combine(output, "Toolkit", "Game1.cs"));
        Assert.Contains("Matrix.CreateLookAt(", game, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(output, "Toolkit", "Content", "Content.mgcb")));
        Assert.True(File.Exists(Path.Combine(output, "sdx-mixed.sln")));
    }

    // ---------- 8.3 store apps ----------
    [Fact]
    public async Task A_windows_store_project_is_refused_and_no_copy_is_made_for_it()
    {
        var folder = Path.Combine(_temp, "store-and-desktop");
        MigrationTestSupport.CopyFolder(Fixture("sdx-mixed"), folder);
        var toolkitProject = Path.Combine(folder, "Toolkit", "ToolkitGame.csproj");
        File.WriteAllText(toolkitProject, File.ReadAllText(toolkitProject).Replace("<ProjectGuid>", "<ProjectTypeGuids>{BC8A1FFA-BEE3-4634-8014-F334798102B3};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids><ProjectGuid>", StringComparison.Ordinal));

        var (result, output) = await MigrateAsync("sdx-mixed", source: folder);

        var skipped = Change(result, "Toolkit/ToolkitGame.csproj");
        Assert.Equal(ChangeKind.Skipped, skipped.Kind);
        Assert.Contains("Windows Store or Windows Phone", skipped.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Changes, c => c.RelativePath.StartsWith("Toolkit/", StringComparison.Ordinal) && c.Kind != ChangeKind.Skipped);
        Assert.False(Directory.Exists(Path.Combine(output, "Toolkit")));
        Assert.True(File.Exists(Path.Combine(output, "Xna", "XnaGame.csproj")));
        Assert.Contains(result.Manifest.Changes, c => c.Path == "Toolkit/ToolkitGame.csproj" && c.Kind == ChangeKind.Skipped && c.Rules.Contains("SDX004"));
    }

    [Fact]
    public async Task A_source_whose_only_project_is_a_store_app_is_refused_with_a_clear_message()
    {
        var path = Fixture("sdx-store-app");
        var report = await AnalyzeAsync(path);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ProjectMigrator().MigrateAsync(path, report, Options(Path.Combine(_temp, "store")), false, null, CancellationToken.None));

        Assert.Contains("Windows Store or Windows Phone", ex.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_temp, "store")));
    }

    // ---------- 10 planner and manifest ----------
    [Fact]
    public async Task The_plan_for_the_mixed_solution_has_unique_ids_across_both_catalogs()
    {
        var report = await AnalyzeAsync(Fixture("sdx-mixed"));

        var plan = new MigrationPlanner().CreatePlan(report, new HashSet<string>(StringComparer.Ordinal));

        var ids = plan.Items.Select(i => i.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(plan.Items, i => i.Id == "SDX020:Toolkit/ToolkitGame.csproj");
        Assert.Contains(plan.Items, i => i.Id == "XNA001:Xna/XnaGame.csproj");
        Assert.All(plan.Items.Where(i => i.RuleId.StartsWith("SDX", StringComparison.Ordinal)), i => Assert.All(i.AffectedFiles, f => Assert.StartsWith("Toolkit/", f, StringComparison.Ordinal)));
        Assert.All(plan.Items.Where(i => i.RuleId.StartsWith("XNA", StringComparison.Ordinal)), i => Assert.All(i.AffectedFiles, f => Assert.StartsWith("Xna/", f, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Every_autofixable_toolkit_rule_that_fires_resolves_its_task_from_the_report()
    {
        var report = await AnalyzeAsync(Fixture("sdx-basic"));

        var plan = new MigrationPlanner().CreatePlan(report, new HashSet<string>(StringComparer.Ordinal));

        Assert.All(plan.Items, item => Assert.Contains(report.TriggeredRules, r => r.Id == item.RuleId && !string.IsNullOrEmpty(r.MigratorTask)));
        Assert.Contains(plan.Items, i => i.RuleId == "SDX001");
    }

    [Fact]
    public async Task The_manifest_names_the_source_framework_and_the_rules_that_fired()
    {
        var (result, output) = await MigrateAsync("sdx-basic");

        Assert.Equal(["SharpDxToolkit"], result.Manifest.SourceFrameworks);
        Assert.Contains("SDX001", result.Manifest.RulesFired!);
        Assert.Contains("SDX030", result.Manifest.RulesFired!);
        var json = File.ReadAllText(Path.Combine(output, "xnareborn-manifest.json"));
        var markdown = File.ReadAllText(Path.Combine(output, "xnareborn-manifest.md"));
        Assert.Contains("\"sourceFrameworks\": [", json, StringComparison.Ordinal);
        Assert.Contains("Source framework: SharpDX Toolkit.", markdown, StringComparison.Ordinal);
        Assert.Contains("Rules that fired: SDX001, SDX002", markdown, StringComparison.Ordinal);
        var game = Assert.Single(result.Manifest.Changes, c => c.Path == "Game1.cs");
        Assert.Contains("SDX030", game.Rules);
    }

    [Fact]
    public async Task A_mixed_manifest_names_both_frameworks()
    {
        var (result, _) = await MigrateAsync("sdx-mixed", MonoGameTarget.DesktopGL);

        Assert.Equal(["SharpDxToolkit", "Xna"], result.Manifest.SourceFrameworks);
    }

    [Fact]
    public async Task An_xna_manifest_has_no_framework_fields()
    {
        var path = Fixture("xna40-basic");
        var report = await MigrationTestSupport.AnalyzeAsync(path);
        var output = Path.Combine(_temp, "xna-out");

        var result = await new ProjectMigrator().MigrateAsync(path, report, MigrationTestSupport.Options(output), false, null, CancellationToken.None);

        Assert.Null(result.Manifest.SourceFrameworks);
        Assert.Null(result.Manifest.RulesFired);
        var json = ManifestJsonWriter.Write(result.Manifest);
        Assert.DoesNotContain("sourceFrameworks", json, StringComparison.Ordinal);
        Assert.DoesNotContain("rulesFired", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Source framework", ManifestMarkdownWriter.Write(result.Manifest, []), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_manifest_snapshot_for_sdx_basic_matches()
    {
        var (result, _) = await MigrateAsync("sdx-basic");

        await Verifier.Verify(ManifestMarkdownWriter.Write(result.Manifest, []).Replace(ProjectMigrator.MigratorVersion(), "VERSION", StringComparison.Ordinal), "md");
    }
}
