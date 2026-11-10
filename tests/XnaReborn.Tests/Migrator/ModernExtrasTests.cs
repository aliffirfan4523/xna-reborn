using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Modern;
using XnaReborn.Migrator.Output;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator;

/// <summary>The opt-in window extras: options, the generated helper, the one-line hook and the manifest block. All off changes nothing.</summary>
[Trait("Category", "Migrator")]
public sealed class ModernExtrasTests
{
    private const string _program = """
        using System;

        namespace Demo
        {
            static class Program
            {
                static void Main(string[] args)
                {
                    using (Game1 game = new Game1())
                    {
                        game.Run();
                    }
                }
            }
        }
        """;

    private static readonly string _output = Path.Combine(Path.GetTempPath(), "xr-modern-never-written");

    private static readonly ModernExtrasChoice _all = new(true, true, true);

    private static MigrationOptions Options(ModernExtrasChoice? choice = null) =>
        MigrationTestSupport.Options(_output, dryRun: true) with
        {
            AllowWindowResize = choice?.AllowWindowResize ?? false,
            FullscreenToggle = choice?.FullscreenToggle ?? false,
            HighDpi = choice?.HighDpi ?? false,
        };

    [Fact]
    public void Options_default_to_all_extras_off()
    {
        var options = MigrationTestSupport.Options(_output);

        Assert.False(options.AllowWindowResize);
        Assert.False(options.FullscreenToggle);
        Assert.False(options.HighDpi);
        Assert.False(ModernExtrasChoice.From(options).Any);
    }

    [Theory]
    [InlineData(true, false, false, "AllowWindowResize")]
    [InlineData(false, true, false, "FullscreenToggle")]
    [InlineData(false, false, true, "HighDpi")]
    public void Choice_reads_each_option_on_its_own(bool resize, bool fullscreen, bool dpi, string expected)
    {
        var choice = ModernExtrasChoice.From(Options(new ModernExtrasChoice(resize, fullscreen, dpi)));

        Assert.True(choice.Any);
        Assert.Equal([expected], choice.FeatureNames());
        Assert.Equal(resize || fullscreen, choice.NeedsScaling);
    }

    [Fact]
    public void Generator_refuses_when_no_extra_is_on()
    {
        Assert.Throws<ArgumentException>(() => ModernExtrasGenerator.Generate(string.Empty, new ModernExtrasChoice(false, false, false)));
    }

    [Fact]
    public void Generator_puts_the_helper_in_the_project_folder_with_lf_endings()
    {
        var root = ModernExtrasGenerator.Generate(string.Empty, _all);
        var nested = ModernExtrasGenerator.Generate("Game/Sub", _all);

        Assert.Equal("XnaReborn.Modern.cs", root.RelativePath);
        Assert.Equal("Game/Sub/XnaReborn.Modern.cs", nested.RelativePath);
        Assert.Equal(ChangeKind.Created, root.Kind);
        Assert.DoesNotContain('\r', root.NewText!);
        Assert.EndsWith("\n", root.NewText!, StringComparison.Ordinal);
    }

    [Fact]
    public Task Helper_with_all_extras_matches_snapshot() => Verifier.Verify(Helper(_all), "txt");

    [Fact]
    public Task Helper_with_resize_only_matches_snapshot() => Verifier.Verify(Helper(new ModernExtrasChoice(true, false, false)), "txt");

    [Fact]
    public Task Helper_with_high_dpi_only_matches_snapshot() => Verifier.Verify(Helper(new ModernExtrasChoice(false, false, true)), "txt");

    [Fact]
    public void Resize_is_in_the_helper_only_when_asked_for()
    {
        Assert.Contains("AllowUserResizing = true", Helper(new ModernExtrasChoice(true, false, false)), StringComparison.Ordinal);
        Assert.DoesNotContain("AllowUserResizing", Helper(new ModernExtrasChoice(false, true, false)), StringComparison.Ordinal);
        Assert.DoesNotContain("AllowUserResizing", Helper(new ModernExtrasChoice(false, false, true)), StringComparison.Ordinal);
    }

    [Fact]
    public void Fullscreen_toggle_is_in_the_helper_only_when_asked_for()
    {
        Assert.Contains("ScaledPresenter(game, true)", Helper(new ModernExtrasChoice(false, true, false)), StringComparison.Ordinal);
        Assert.Contains("ScaledPresenter(game, false)", Helper(new ModernExtrasChoice(true, false, false)), StringComparison.Ordinal);
        Assert.DoesNotContain("ScaledPresenter", Helper(new ModernExtrasChoice(false, false, true)), StringComparison.Ordinal);
    }

    [Fact]
    public void High_dpi_is_in_the_helper_only_when_asked_for()
    {
        Assert.Contains("[ModuleInitializer]", Helper(new ModernExtrasChoice(false, false, true)), StringComparison.Ordinal);
        Assert.DoesNotContain("ModuleInitializer", Helper(new ModernExtrasChoice(true, true, false)), StringComparison.Ordinal);
        Assert.DoesNotContain("user32", Helper(new ModernExtrasChoice(true, true, false)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    public void Generated_helper_has_no_syntax_errors(bool resize, bool fullscreen, bool dpi)
    {
        // shortcut: syntax only. The real compile against MonoGame runs in the integration publish test.
        var tree = CSharpSyntaxTree.ParseText(Helper(new ModernExtrasChoice(resize, fullscreen, dpi)));

        Assert.DoesNotContain(tree.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Hook_adds_two_lines_above_Run_and_changes_nothing_else()
    {
        var result = ModernExtrasHook.Apply(_program, []);

        Assert.True(result.Applied);
        Assert.Null(result.Note);
        var lines = result.Text.Split('\n');
        var run = Array.FindIndex(lines, l => l.Trim() == "game.Run();");
        Assert.Equal("                XnaReborn.Modern.ModernExtras.Attach(game);", lines[run - 1]);
        Assert.StartsWith("                // XNAREBORN: MODERN: ", lines[run - 2], StringComparison.Ordinal);
        Assert.Equal(_program.Split('\n').Length + 2, lines.Length);
        Assert.Equal(_program, string.Join('\n', lines.Where((_, i) => i != run - 1 && i != run - 2)));
    }

    [Fact]
    public void Hook_keeps_crlf_line_endings()
    {
        var crlf = _program.Replace("\n", "\r\n", StringComparison.Ordinal);

        var result = ModernExtrasHook.Apply(crlf, []);

        Assert.True(result.Applied);
        Assert.Equal(result.Text.Count(c => c == '\n'), result.Text.Count(c => c == '\r'));
    }

    [Fact]
    public void Hook_is_added_once()
    {
        var once = ModernExtrasHook.Apply(_program, []);
        var twice = ModernExtrasHook.Apply(once.Text, []);

        Assert.False(twice.Applied);
        Assert.Null(twice.Note);
        Assert.Equal(once.Text, twice.Text);
    }

    [Fact]
    public void Hook_works_with_top_level_statements()
    {
        const string text = "var game = new Game1();\ngame.Run();\n";

        var result = ModernExtrasHook.Apply(text, []);

        Assert.True(result.Applied);
        Assert.Contains("ModernExtras.Attach(game);\ngame.Run();", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Hook_leaves_a_file_without_Main_alone()
    {
        const string text = "class Game1 { void Go(Game1 g) { g.Run(); } }\n";

        var result = ModernExtrasHook.Apply(text, []);

        Assert.False(result.Applied);
        Assert.Null(result.Note);
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void Hook_leaves_a_Main_without_Run_alone()
    {
        var text = _program.Replace("game.Run();", "game.Tick();", StringComparison.Ordinal);

        var result = ModernExtrasHook.Apply(text, []);

        Assert.False(result.Applied);
        Assert.Null(result.Note);
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void Hook_does_not_guess_at_new_Game_Run_and_says_so()
    {
        var text = _program.Replace("using (Game1 game = new Game1())\n            {\n                game.Run();\n            }", "new Game1().Run();", StringComparison.Ordinal);

        var result = ModernExtrasHook.Apply(text, []);

        Assert.False(result.Applied);
        Assert.Contains("by hand", result.Note, StringComparison.Ordinal);
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void Hook_does_not_guess_at_an_embedded_statement_and_says_so()
    {
        var text = _program.Replace("using (Game1 game = new Game1())\n            {\n                game.Run();\n            }", "using (Game1 game = new Game1()) game.Run();", StringComparison.Ordinal);

        var result = ModernExtrasHook.Apply(text, []);

        Assert.False(result.Applied);
        Assert.NotNull(result.Note);
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void Hook_ignores_code_in_an_inactive_if_block()
    {
        var text = "#if XBOX\n" + _program + "\n#endif\n";

        var result = ModernExtrasHook.Apply(text, ["WINDOWS"]);

        Assert.False(result.Applied);
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public async Task Migration_with_all_extras_off_changes_nothing()
    {
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna40-basic"), Options());

        Assert.DoesNotContain(result.Changes, c => c.RelativePath.EndsWith("XnaReborn.Modern.cs", StringComparison.Ordinal));
        Assert.Equal(ChangeKind.Copied, result.Changes.Single(c => c.RelativePath == "Program.cs").Kind);
        Assert.Null(result.Manifest.Modern);
        Assert.DoesNotContain("modern", ManifestJsonWriter.Write(result.Manifest), StringComparison.Ordinal);
        Assert.DoesNotContain("## Modern extras", ManifestMarkdownWriter.Write(result.Manifest, []), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migration_with_extras_on_adds_the_helper_the_hook_and_the_manifest_block()
    {
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna40-basic"), Options(_all));

        var helper = result.Changes.Single(c => c.RelativePath == "XnaReborn.Modern.cs");
        Assert.Equal(ChangeKind.Created, helper.Kind);
        var program = result.Changes.Single(c => c.RelativePath == "Program.cs");
        Assert.Equal(ChangeKind.Modified, program.Kind);
        Assert.Contains("XnaReborn.Modern.ModernExtras.Attach(game);", program.NewText, StringComparison.Ordinal);
        Assert.Equal(["MODERN"], result.Manifest.Changes.Single(c => c.Path == "Program.cs").Rules.Where(r => r == "MODERN"));

        var modern = result.Manifest.Modern!;
        Assert.Equal(["AllowWindowResize", "FullscreenToggle", "HighDpi"], modern.Features);
        Assert.Equal(["XnaReborn.Modern.cs"], modern.Files);
        Assert.Equal(["Program.cs"], modern.HookedFiles);
        Assert.NotEmpty(modern.Limits);

        using var json = JsonDocument.Parse(ManifestJsonWriter.Write(result.Manifest));
        Assert.Equal(3, json.RootElement.GetProperty("modern").GetProperty("features").GetArrayLength());
        var markdown = ManifestMarkdownWriter.Write(result.Manifest, []);
        Assert.Contains("## Modern extras", markdown, StringComparison.Ordinal);
        Assert.Contains("Mouse and touch positions are not remapped", markdown, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task Migration_with_one_extra_on_lists_only_that_extra(bool resize, bool fullscreen, bool dpi)
    {
        var choice = new ModernExtrasChoice(resize, fullscreen, dpi);

        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna40-basic"), Options(choice));

        Assert.Equal(choice.FeatureNames(), result.Manifest.Modern!.Features);
        Assert.Equal(Helper(choice), result.Changes.Single(c => c.RelativePath == "XnaReborn.Modern.cs").NewText);
    }

    [Fact]
    public async Task Migration_does_not_change_gameplay_files()
    {
        var off = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna40-basic"), Options());
        var on = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna40-basic"), Options(_all));

        Assert.Equal(ChangeKind.Copied, on.Changes.Single(c => c.RelativePath == "Game1.cs").Kind);
        Assert.Equal(
            off.Changes.Where(c => c.RelativePath is not ("Program.cs" or "XnaReborn.Modern.cs")).Select(c => (c.RelativePath, c.Kind, c.NewText)),
            on.Changes.Where(c => c.RelativePath is not ("Program.cs" or "XnaReborn.Modern.cs")).Select(c => (c.RelativePath, c.Kind, c.NewText)));
    }

    [Fact]
    public async Task Migration_notes_that_the_extras_do_nothing_when_no_entry_point_takes_the_hook()
    {
        using var temp = new TempFolder();
        MigrationTestSupport.CopyFolder(FixturePaths.Of("xna40-basic"), temp.Path);
        var program = Path.Combine(temp.Path, "Program.cs");
        File.WriteAllText(program, File.ReadAllText(program).Replace("game.Run();", "game.Tick();", StringComparison.Ordinal));

        var result = await MigrationTestSupport.MigrateAsync(temp.Path, Options(_all));

        Assert.DoesNotContain(result.Changes, c => c.RelativePath == "XnaReborn.Modern.cs");
        Assert.Empty(result.Manifest.Modern!.HookedFiles);
        Assert.Contains("do nothing", ManifestMarkdownWriter.Write(result.Manifest, []), StringComparison.Ordinal);
    }

    [Fact]
    public Task Modern_manifest_markdown_matches_snapshot()
    {
        var modern = new ManifestModern(
            ["AllowWindowResize", "HighDpi"],
            ["Game/XnaReborn.Modern.cs"],
            ["Game/Program.cs"],
            ModernExtrasGenerator.Limits(new ModernExtrasChoice(true, false, true)));
        var manifest = ManifestBuilder.Build("c", true, [], new Dictionary<string, IReadOnlyList<string>>(), null, "0.1.0", modern: modern);

        return Verifier.Verify(ManifestMarkdownWriter.Write(manifest, []), "md");
    }

    private static string Helper(ModernExtrasChoice choice) => ModernExtrasGenerator.Generate(string.Empty, choice).NewText!;

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xr-modern-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
