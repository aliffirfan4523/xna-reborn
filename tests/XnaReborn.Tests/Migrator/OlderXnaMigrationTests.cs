using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;
using XnaReborn.Tests.Fixtures;
using Xunit.Abstractions;

namespace XnaReborn.Tests.Migrator;

/// <summary>Older-and-vb tasks 8 and 9: in-project content, the legacy texture processor alias, and the LoadGraphicsContent rewriter.</summary>
[Trait("Category", "Migrator")]
public sealed class OlderXnaMigrationTests(ITestOutputHelper testOutput) : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-older-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            try
            {
                Directory.Delete(_temp, recursive: true);
            }
            catch (IOException)
            {
                // A build server may still hold a file. The folder is in the temp directory.
            }
        }
    }

    private static FileChange Single(XnaReborn.Migrator.MigrationResult result, string path) => result.Changes.Single(c => c.RelativePath == path);

    // The fixtures hold ASCII placeholders instead of images. The content builder needs real ones, so the migrated copy gets a 1x1 PNG.
    private static void UseRealImages(string folder)
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        foreach (var file in Directory.EnumerateFiles(folder, "*.png", SearchOption.AllDirectories))
        {
            File.WriteAllBytes(file, png);
        }
    }

    private static SourceRewriteContext Context(string text) => new("Game1.cs", text, ["WINDOWS"]);

    private static string Load(string text, bool unload = false) =>
        (unload ? LoadGraphicsContentRewriter.ForUnload() : LoadGraphicsContentRewriter.ForLoad()).Rewrite(Context(text)).Text;

    // ---------- the rewriter ----------
    private const string _before = "using Microsoft.Xna.Framework;\nclass G : Game\n{\n    int x;   // untouched\n\n    protected override void LoadGraphicsContent(bool loadAllContent)\n    {\n        if (loadAllContent)\n        {\n            x = 1;\n        }\n    }\n\n    protected override void UnloadGraphicsContent(bool unloadAllContent)\n    {\n        if (unloadAllContent) { x = 0; }\n    }\n}\n";

    [Fact]
    public void Load_override_is_renamed_loses_its_parameter_and_gets_a_constant()
    {
        var after = Load(_before);

        Assert.Equal(
            "using Microsoft.Xna.Framework;\nclass G : Game\n{\n    int x;   // untouched\n\n    protected override void LoadContent()\n    {\n        const bool loadAllContent = true;\n        if (loadAllContent)\n        {\n            x = 1;\n        }\n    }\n\n    protected override void UnloadGraphicsContent(bool unloadAllContent)\n    {\n        if (unloadAllContent) { x = 0; }\n    }\n}\n",
            after);
    }

    [Fact]
    public void Unload_override_is_rewritten_by_its_own_rewriter()
    {
        var after = Load(_before, unload: true);

        Assert.Contains("protected override void UnloadContent()\n    {\n        const bool unloadAllContent = true;\n        if (unloadAllContent) { x = 0; }", after, StringComparison.Ordinal);
        Assert.Contains("LoadGraphicsContent(bool loadAllContent)", after, StringComparison.Ordinal);
    }

    [Fact]
    public void Crlf_line_endings_and_tabs_are_kept()
    {
        var text = "class G\r\n{\r\n\tprotected override void LoadGraphicsContent(bool all)\r\n\t{\r\n\t\tF(all);\r\n\t}\r\n}\r\n";

        var after = Load(text);

        Assert.Equal("class G\r\n{\r\n\tprotected override void LoadContent()\r\n\t{\r\n\t\tconst bool all = true;\r\n\t\tF(all);\r\n\t}\r\n}\r\n", after);
    }

    [Theory]
    [InlineData("class G { protected override void LoadGraphicsContent(bool all) { } void M() { base.LoadGraphicsContent(true); } }")]
    [InlineData("class G { protected override void LoadGraphicsContent(int all) { } }")]
    [InlineData("class G { protected override void LoadGraphicsContent(bool a, bool b) { } }")]
    [InlineData("class G { protected override void LoadGraphicsContent(bool all) { } protected override void LoadContent() { } }")]
    public void Unusual_shapes_are_marked_and_not_changed(string text)
    {
        var result = LoadGraphicsContentRewriter.ForLoad().Rewrite(Context(text));

        Assert.Contains("// XNAREBORN: XNA102:", result.Text, StringComparison.Ordinal);
        Assert.Contains("LoadGraphicsContent(", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("const bool", result.Text, StringComparison.Ordinal);
        Assert.Contains(result.Notes, n => n.NeedsManualWork);
    }

    [Fact]
    public void A_call_by_name_marks_the_override_and_leaves_the_file_otherwise_alone()
    {
        const string text = "class G\n{\n    void A() { LoadGraphicsContent(false); }\n    protected override void LoadGraphicsContent(bool all) { }\n}\n";

        var result = LoadGraphicsContentRewriter.ForLoad().Rewrite(Context(text));

        Assert.Equal(text.Replace("    protected override", "    // XNAREBORN: XNA102: LoadGraphicsContent does not exist in MonoGame. Move this code into LoadContent() by hand.\n    protected override", StringComparison.Ordinal), result.Text);
    }

    [Fact]
    public void A_file_without_the_override_and_a_non_override_are_unchanged()
    {
        const string plain = "class G { protected override void LoadContent() { } }";
        const string notOverride = "class G { void LoadGraphicsContent(bool all) { } }";

        Assert.Equal(plain, Load(plain));
        Assert.Equal(notOverride, Load(notOverride));
    }

    [Fact]
    public void Running_the_rewriter_twice_changes_nothing_the_second_time()
    {
        var once = Load(_before);

        Assert.Equal(once, Load(once));
    }

    // ---------- the migration ----------
    [Fact]
    public async Task Express_fixture_gets_a_content_file_next_to_the_project_and_the_current_processor()
    {
        var original = FixturePaths.Of("xna10-express-basic");
        var before = MigrationTestSupport.Tree(original);
        var output = Path.Combine(_temp, "express");

        var result = await MigrationTestSupport.MigrateAsync(original, MigrationTestSupport.Options(output));

        Assert.True(result.Completed);
        var csproj = Single(result, "XnaExpress10Basic.csproj").NewText!;
        Assert.Contains("<MonoGameContentReference Include=\"Content.mgcb\" />", csproj, StringComparison.Ordinal);
        Assert.Contains("MonoGame.Content.Builder.Task", csproj, StringComparison.Ordinal);
        var mgcb = Single(result, "Content.mgcb").NewText!;
        Assert.Contains("/build:Content/ship.png", mgcb, StringComparison.Ordinal);
        Assert.Contains("/build:Content/backdrop.png", mgcb, StringComparison.Ordinal);
        Assert.Contains("/processor:TextureProcessor", mgcb, StringComparison.Ordinal);
        Assert.DoesNotContain("SpriteTextureProcessor", mgcb, StringComparison.Ordinal);
        Assert.Equal(ChangeKind.Copied, Single(result, "Content/ship.png").Kind);
        Assert.Equal(2, result.Manifest.Content!.CountsByStatus["Converted"]);

        var game = Single(result, "Game1.cs").NewText!;
        Assert.Contains("protected override void LoadContent()", game, StringComparison.Ordinal);
        Assert.Contains("protected override void UnloadContent()", game, StringComparison.Ordinal);
        Assert.Contains("const bool loadAllContent = true;", game, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadGraphicsContent", game, StringComparison.Ordinal);
        Assert.Equal(before, MigrationTestSupport.Tree(original));
    }

    [Fact]
    public async Task Version_2_0_solution_keeps_its_nested_content_project_conversion()
    {
        var output = Path.Combine(_temp, "v20");

        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna20-vs2005"), MigrationTestSupport.Options(output));

        Assert.True(result.Completed);
        var mgcb = Single(result, "Game/Content/Content.mgcb").NewText!;
        Assert.Contains("/build:Textures/hero.png", mgcb, StringComparison.Ordinal);
        Assert.Equal(ChangeKind.Copied, Single(result, "Game/Content/Textures/hero.png").Kind);
        Assert.Contains("<MonoGameContentReference Include=\"Content\\Content.mgcb\" />", Single(result, "Game/Game.csproj").NewText!, StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task Migrated_express_fixture_builds()
    {
        var output = Path.Combine(_temp, "express-build");
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna10-express-basic"), MigrationTestSupport.Options(output));
        Assert.True(result.Completed);
        UseRealImages(output);

        var (exitCode, text) = await MigrationIntegrationTests.RunBuildAsync(output);
        testOutput.WriteLine(text);

        Assert.True(exitCode == 0, $"dotnet build failed with exit code {exitCode}.{Environment.NewLine}{text}");
    }

    [IntegrationFact]
    public async Task Migrated_version_2_0_solution_builds_the_windows_project()
    {
        var output = Path.Combine(_temp, "v20-build");
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna20-vs2005"), MigrationTestSupport.Options(output));
        Assert.True(result.Completed);
        UseRealImages(output);

        var (exitCode, text) = await MigrationIntegrationTests.RunBuildAsync(Path.Combine(output, "Game"));
        testOutput.WriteLine(text);

        Assert.True(exitCode == 0, $"dotnet build failed with exit code {exitCode}.{Environment.NewLine}{text}");
    }
}
