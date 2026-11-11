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
    public void Default_rewriters_register_the_entry_point_the_source_rules_the_device_rules_the_monogame_rules_and_the_toolkit_rules()
    {
        Assert.Equal(["DEVICEINPUT", "ENTRYPOINT", "MG028", "MG029", "MG030", "MG031", "MG032", "SDX005", "SDX006", "SDX007", "SDX008", "SDX020", "SDX021", "SDX022", "SDX023", "SDX024", "SDX025", "SDX026", "SDX027", "SDX028", "SDX030", "SDX031", "SDX032", "SDX033", "SDX034", "SDX035", "SDX040", "SDX041", "SDX042", "SDX043", "SDX044", "SDX045", "SDX046", "SDX047", "SDX048", "SDX049", "SDX050", "SDX051", "SDX060", "SDX061", "SDX062", "SDX063", "XNA022", "XNA024", "XNA039", "XNA040", "XNA053", "XNA056", "XNA059", "XNA062", "XNA063", "XNA102", "XNA103"], DefaultRewriters.Create().RuleIds);
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

        // XNA053 folds the #if WINDOWS || XBOX wrapper first, so the entry point rewriter has nothing left to do.
        Assert.Contains("XNA053", Rules(result, "Program.cs"));
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
    public async Task Multi_project_solution_gets_two_projects_a_solution_sources_and_converted_content()
    {
        var output = Out();
        var result = await MigrationTestSupport.MigrateAsync(Fixture("multi-project-sln"), MigrationTestSupport.Options(output));

        var created = result.Changes.Where(c => c.Kind == ChangeKind.Created).Select(c => c.RelativePath).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            [".config/dotnet-tools.json", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "Game/Content/Content.mgcb", "Game/Game.csproj", "Library/Library.csproj", "multi-project-sln.sln"],
            created);
        Assert.Contains(result.Changes, c => c.RelativePath == "Library/Helpers.cs");
        Assert.Contains(result.Changes, c => c.RelativePath == "Game/Game1.cs");

        var icon = Single(result, "Game/Content/Textures/icon.png");
        Assert.Equal(ChangeKind.Copied, icon.Kind);
        Assert.Equal(Path.Combine(Fixture("multi-project-sln"), "GameContent", "Textures", "icon.png"), icon.SourcePath);
        Assert.DoesNotContain(result.Changes, c => c.RelativePath.StartsWith("GameContent/", StringComparison.Ordinal));
        var csproj = Single(result, "Game/Game.csproj").NewText!;
        Assert.Contains("<MonoGameContentReference Include=\"Content\\Content.mgcb\" />", csproj, StringComparison.Ordinal);
        Assert.Contains("MonoGame.Content.Builder.Task", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("MonoGame.Content.Builder", Single(result, "Library/Library.csproj").NewText, StringComparison.Ordinal);

        Assert.True(File.Exists(Path.Combine(output, "Game", "Content", "Textures", "icon.png")));
        Assert.True(File.Exists(Path.Combine(output, "Game", "Content", "Content.mgcb")));
        Assert.True(File.Exists(Path.Combine(output, ".config", "dotnet-tools.json")));
        var markdown = File.ReadAllText(Path.Combine(output, "xnareborn-manifest.md"));
        Assert.Contains("## Content", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("not wired in yet", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Custom_content_fixture_converts_what_it_can_and_lists_the_rest_as_manual_work()
    {
        var result = await MigrationTestSupport.MigrateAsync(Fixture("xna40-custom-content"), MigrationTestSupport.Options(Out(), dryRun: true));

        var content = result.Changes.Where(c => c.RelativePath.StartsWith("Content/", StringComparison.Ordinal)).ToList();
        Assert.Equal(
            ["Content/Audio/Sounds.xap", "Content/Content.mgcb", "Content/Fonts/Hud.spritefont", "Content/Shaders/Glow.fx", "Content/Textures/hero.png", "Content/Textures/ship.png"],
            content.Select(c => c.RelativePath));
        Assert.Equal(ChangeKind.ManualWork, Single(result, "Content/Audio/Sounds.xap").Kind);
        Assert.Equal(ChangeKind.ManualWork, Single(result, "Content/Textures/ship.png").Kind);
        Assert.Equal(ChangeKind.ManualWork, Single(result, "Content/Fonts/Hud.spritefont").Kind);
        Assert.Equal(ChangeKind.Copied, Single(result, "Content/Textures/hero.png").Kind);
        Assert.Equal(ChangeKind.Copied, Single(result, "Content/Shaders/Glow.fx").Kind);
        Assert.DoesNotContain(content, c => c.Reason?.Contains("not wired in yet", StringComparison.Ordinal) == true);

        var csproj = Single(result, "CustomContent.csproj").NewText!;
        Assert.Contains("<MonoGameContentReference Include=\"Content\\Content.mgcb\" />", csproj, StringComparison.Ordinal);
        Assert.Contains("MonoGame.Content.Builder.Task", csproj, StringComparison.Ordinal);
        Assert.Equal(2, result.Manifest.Content!.CountsByStatus["Converted"]);
        Assert.Equal(3, result.Manifest.Content.CountsByStatus["ManualWork"]);
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

    [Fact]
    public async Task Basic_fixture_content_is_converted_with_the_builder_package_tool_manifest_and_counts()
    {
        var output = Out();
        var result = await MigrationTestSupport.MigrateAsync(Fixture("xna40-basic"), MigrationTestSupport.Options(output));

        Assert.True(result.Completed);
        var csproj = Single(result, "Xna40Basic.csproj").NewText!;
        Assert.Contains("<PackageReference Include=\"MonoGame.Content.Builder.Task\" Version=\"3.8.6.1\" />", csproj, StringComparison.Ordinal);
        Assert.Contains("<MonoGameContentReference Include=\"Content\\Content.mgcb\" />", csproj, StringComparison.Ordinal);

        var mgcb = Single(result, "Content/Content.mgcb");
        Assert.Equal(ChangeKind.Created, mgcb.Kind);
        Assert.Equal(_expectedBasicMgcb, mgcb.NewText);
        Assert.Equal(_expectedBasicMgcb, File.ReadAllText(Path.Combine(output, "Content", "Content.mgcb")));
        Assert.Equal(ChangeKind.Copied, Single(result, "Content/Textures/hero.png").Kind);
        Assert.Equal(ChangeKind.Copied, Single(result, "Content/Fonts/Font.spritefont").Kind);
        Assert.DoesNotContain(result.Changes, c => c.RelativePath.EndsWith(".contentproj", StringComparison.Ordinal));

        var tools = Single(result, ".config/dotnet-tools.json");
        Assert.Equal(ChangeKind.Created, tools.Kind);
        Assert.Contains("\"dotnet-mgcb\"", tools.NewText, StringComparison.Ordinal);
        Assert.Contains("\"3.8.6.1\"", tools.NewText, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(output, ".config", "dotnet-tools.json")));
        Assert.True(File.Exists(Path.Combine(output, "Content", "Textures", "hero.png")));

        Assert.Equal(2, result.Manifest.Content!.CountsByStatus["Converted"]);
        Assert.Equal(0, result.Manifest.Content.CountsByStatus["ManualWork"]);
        Assert.Equal(["Converted", "ManualWork", "Missing", "Conflict", "Prebuilt"], result.Manifest.Content.CountsByStatus.Keys);
        Assert.Equal(
            ["Texture", "Font", "SoundEffect", "Song", "Model", "Effect", "Xact", "Xml", "Prebuilt", "Other"],
            result.Manifest.Content.CountsByType.Keys);
        Assert.Equal(1, result.Manifest.Content.CountsByType["Texture"]);
        Assert.Equal(1, result.Manifest.Content.CountsByType["Font"]);

        Assert.Contains("XNA005", Rules(result, "Content/Content.mgcb"));
        Assert.Contains("XNA005", Rules(result, "Xna40Basic.csproj"));
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "xnareborn-manifest.json")));
        Assert.Equal(2, json.RootElement.GetProperty("content").GetProperty("countsByStatus").GetProperty("Converted").GetInt32());
        Assert.Contains("## Content", File.ReadAllText(Path.Combine(output, "xnareborn-manifest.md")), StringComparison.Ordinal);
    }

    private const string _expectedBasicMgcb = """
        #----------------------------- Global Properties ----------------------------#

        /outputDir:bin/$(Platform)
        /intermediateDir:obj/$(Platform)
        /platform:DesktopGL
        /config:
        /profile:HiDef
        /compress:False

        #-------------------------------- References --------------------------------#


        #---------------------------------- Content ---------------------------------#

        #begin Fonts/Font.spritefont
        /importer:FontDescriptionImporter
        /processor:FontDescriptionProcessor
        /build:Fonts/Font.spritefont

        #begin Textures/hero.png
        /importer:TextureImporter
        /processor:TextureProcessor
        /processorParam:PremultiplyAlpha=True
        /build:Textures/hero.png

        """;

    [Fact]
    [Trait("Category", "Safety")]
    public async Task Content_conversion_leaves_the_source_untouched_and_a_dry_run_writes_nothing()
    {
        var source = Path.Combine(_temp, "src");
        MigrationTestSupport.CopyFolder(Fixture("xna40-custom-content"), source);
        var before = FolderState.Capture(source);

        var dry = await MigrationTestSupport.MigrateAsync(source, MigrationTestSupport.Options(Out("dry"), dryRun: true));
        var real = await MigrationTestSupport.MigrateAsync(source, MigrationTestSupport.Options(Out("real")));

        Assert.False(dry.Completed);
        Assert.NotNull(dry.Manifest.Content);
        Assert.False(Directory.Exists(Out("dry")));
        Assert.True(real.Completed);
        Assert.Equal(before, FolderState.Capture(source));
        Assert.Equal(dry.Changes, real.Changes);
    }

    [Fact]
    public async Task Excluding_the_content_rule_keeps_the_placeholder_copy_without_a_content_reference()
    {
        var result = await MigrationTestSupport.MigrateAsync(
            Fixture("xna40-basic"),
            MigrationTestSupport.Options(Out(), dryRun: true, "XNA005:Xna40Basic.csproj"));

        var content = result.Changes.Where(c => c.RelativePath.StartsWith("Content/", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(content);
        Assert.All(content, c =>
        {
            Assert.Equal(ChangeKind.ManualWork, c.Kind);
            Assert.NotNull(c.SourcePath);
        });
        Assert.Contains(content, c => c.RelativePath == "Content/Content.contentproj");
        Assert.DoesNotContain(result.Changes, c => c.RelativePath is "Content/Content.mgcb" or ".config/dotnet-tools.json");

        var csproj = Single(result, "Xna40Basic.csproj").NewText!;
        Assert.DoesNotContain("MonoGameContentReference", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("MonoGame.Content.Builder", csproj, StringComparison.Ordinal);
        Assert.Null(result.Manifest.Content);
        Assert.DoesNotContain(result.Plan.Items, i => i.RuleId == "XNA005");
    }

    [Fact]
    public async Task Font_substitution_option_gives_the_xna_only_font_a_modified_copy()
    {
        var plain = await MigrationTestSupport.MigrateAsync(Fixture("xna40-custom-content"), MigrationTestSupport.Options(Out("plain"), dryRun: true));
        var options = MigrationTestSupport.Options(Out("sub"), dryRun: true) with { SubstituteFonts = true };
        var substituted = await MigrationTestSupport.MigrateAsync(Fixture("xna40-custom-content"), options);

        Assert.Equal(ChangeKind.ManualWork, Single(plain, "Content/Fonts/Hud.spritefont").Kind);
        var hud = Single(substituted, "Content/Fonts/Hud.spritefont");
        Assert.Equal(ChangeKind.Modified, hud.Kind);
        Assert.Contains("<FontName>Georgia</FontName>", hud.NewText, StringComparison.Ordinal);
        Assert.Equal(3, substituted.Manifest.Content!.CountsByStatus["Converted"]);
        Assert.Equal(2, substituted.Manifest.Content.CountsByStatus["ManualWork"]);
    }

    [Fact]
    public async Task A_content_root_other_than_Content_moves_the_mgcb_and_its_reference()
    {
        var source = Path.Combine(_temp, "src");
        MigrationTestSupport.CopyFolder(Fixture("xna40-basic"), source);
        var contentProject = Path.Combine(source, "Content", "Content.contentproj");
        File.WriteAllText(contentProject, File.ReadAllText(contentProject).Replace(
            "<ContentRootDirectory>Content</ContentRootDirectory>",
            "<ContentRootDirectory>Data/Art</ContentRootDirectory>",
            StringComparison.Ordinal));

        var result = await MigrationTestSupport.MigrateAsync(source, MigrationTestSupport.Options(Out(), dryRun: true));

        Assert.Equal(ChangeKind.Created, Single(result, "Data/Art/Content.mgcb").Kind);
        Assert.Equal(ChangeKind.Copied, Single(result, "Data/Art/Textures/hero.png").Kind);
        Assert.Contains(
            "<MonoGameContentReference Include=\"Data\\Art\\Content.mgcb\" />",
            Single(result, "Xna40Basic.csproj").NewText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Progress_reports_a_converting_content_stage_before_the_project_files()
    {
        var stages = new List<string>();
        var progress = new InlineProgress(p => stages.Add(p.Stage));

        await MigrationTestSupport.MigrateAsync(Fixture("xna40-basic"), MigrationTestSupport.Options(Out(), dryRun: true), progress: progress);

        Assert.Equal(["Discovering projects", "Planning", "Converting content", "Generating projects", "Rewriting source", "Done"], stages.Distinct());
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task Cancelling_while_converting_content_throws_and_never_creates_the_output()
    {
        using var cts = new CancellationTokenSource();
        var progress = new InlineProgress(p =>
        {
            if (p.Stage == "Converting content" && p.Completed == 0)
            {
                cts.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MigrationTestSupport.MigrateAsync(
            Fixture("xna40-basic"), MigrationTestSupport.Options(Out()), progress: progress, cancellationToken: cts.Token));

        Assert.False(Directory.Exists(Out()));
        Assert.Empty(Directory.GetFileSystemEntries(_temp));
    }
    private sealed class InlineProgress(Action<MigrationProgress> onReport) : IProgress<MigrationProgress>
    {
        public void Report(MigrationProgress value) => onReport(value);
    }
}
