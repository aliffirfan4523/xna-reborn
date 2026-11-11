using System.Security.Cryptography;
using Microsoft.CodeAnalysis.CSharp;
using XnaReborn.Flash.Converter;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Flash.Tests;

public sealed class FlashMigratorTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xnareborn-flashmig-" + Guid.NewGuid().ToString("N"));

    public FlashMigratorTests() => Directory.CreateDirectory(_temp);

    public void Dispose()
    {
        // XNAREBORN_KEEP_OUTPUT=<folder> copies the last output there for review; nothing else keeps files.
        Directory.Delete(_temp, recursive: true);
    }

    private async Task<FlashMigrationResult> Migrate(string fixture, string? name = null)
    {
        var output = Path.Combine(_temp, name ?? fixture);
        var result = await FlashMigrator.MigrateAsync(FlashFixtures.Of(fixture), output);
        var keep = Environment.GetEnvironmentVariable("XNAREBORN_KEEP_OUTPUT");
        if (!string.IsNullOrEmpty(keep))
        {
            var target = Path.Combine(keep, fixture);
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }

            CopyDirectory(output, target);
        }

        return result;
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from))
        {
            File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        }

        foreach (var d in Directory.GetDirectories(from))
        {
            CopyDirectory(d, Path.Combine(to, Path.GetFileName(d)));
        }
    }

    private static IEnumerable<string> GameFiles(string output) =>
        Directory.EnumerateFiles(Path.Combine(output, "Game"), "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal);

    [Theory]
    [InlineData("flx-hello")]
    [InlineData("flx-platformer")]
    [InlineData("flx-gaps")]
    [InlineData("flx-embeds")]
    public async Task Converted_files_are_valid_csharp_syntax(string fixture)
    {
        var result = await Migrate(fixture);
        var files = GameFiles(result.OutputFolder).ToList();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.CSharp12));
            Assert.True(tree.GetDiagnostics().All(d => d.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Error), file + ": " + string.Join("; ", tree.GetDiagnostics().Take(3)));
        }
    }

    [Fact]
    public async Task Platformer_output_has_the_expected_layout_and_content()
    {
        var result = await Migrate("flx-platformer");
        var o = result.OutputFolder;
        Assert.True(File.Exists(Path.Combine(o, "PlatformerGame.csproj")));
        Assert.True(File.Exists(Path.Combine(o, "Program.cs")));
        Assert.True(File.Exists(Path.Combine(o, "xnareborn-manifest.json")));
        Assert.True(File.Exists(Path.Combine(o, "XnaReborn.Flixel", "XnaReborn.Flixel.csproj")));
        Assert.True(File.Exists(Path.Combine(o, "XnaReborn.Flixel", "FlxSprite.cs")));
        Assert.True(File.Exists(Path.Combine(o, "XnaReborn.Flixel", "As3", "AsCore.cs")));
        Assert.True(File.Exists(Path.Combine(o, "Content", "data", "sprites.png")));
        Assert.True(File.Exists(Path.Combine(o, "Content", "data", "jump.wav")));
        var mgcb = File.ReadAllText(Path.Combine(o, "Content", "Content.mgcb"));
        Assert.Contains("/build:data/sprites.png", mgcb);
        Assert.Contains("WavImporter", mgcb);
        Assert.False(File.Exists(Path.Combine(o, "INCOMPLETE.txt")));
        Assert.Equal("Flash", System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(o, "xnareborn-manifest.json"))).RootElement.GetProperty("sourceKind").GetString());
    }

    [Fact]
    public async Task Platformer_code_shows_the_language_conversions()
    {
        var result = await Migrate("flx-platformer");
        string Read(string n) => File.ReadAllText(Path.Combine(result.OutputFolder, "Game", n));
        var menu = Read("MenuState.cs");
        Assert.Contains("((double)FlxG.height / 2) - 10", menu);
        Assert.Contains("FlxG.switchState(new PlayState());", menu);
        Assert.Contains("public override void create()", menu);
        var game = Read("PlatformerGame.cs");
        Assert.Contains(": base(320, 240, AsClass.Of<MenuState>(), 2)", game);
        var player = Read("Player.cs");
        Assert.Contains("public double runSpeed = double.NaN;", player);
        Assert.Contains("AsClass ImgPlayer = AsClass.FromAsset(\"data/sprites\", AssetKind.Texture);", player);
        Assert.Contains("_bullets.recycle(AsClass.Of<Bullet>()) as Bullet", player);
        Assert.Contains("addAnimation(\"walk\", new AsArray(1, 2, 3, 2), 10, true);", player);
        var score = Read("Score.cs");
        Assert.Contains("public virtual int value", score);
        Assert.Contains("AsVector<AsFunction>", score);
        Assert.Contains("f.Call(_value)", score);
        Assert.Contains("return (double)a / b;", score);
        var play = Read("PlayState.cs");
        Assert.Contains("AsFunction.From(new Action<FlxObject, FlxObject>(hitEnemy))", play);
        Assert.Contains("_score.value += 10;", play);
    }

    [Fact]
    public async Task Gap_constructs_leave_markers_with_the_original_text()
    {
        var result = await Migrate("flx-gaps");
        var ids = result.Markers.Select(m => m.RuleId).ToHashSet();
        Assert.Superset(new HashSet<string> { "FL020", "FL021", "FL022", "FL025", "FL030", "FL031", "FL040", "FL043", "FL046", "FL052" }, ids);
        var with = File.ReadAllText(Path.Combine(result.OutputFolder, "Game", "src", "GapWith.cs"));
        Assert.Contains("// XNAREBORN: FL020", with);
        Assert.Contains("with (o)", with);
        var counts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputFolder, "xnareborn-manifest.json"))).RootElement.GetProperty("markers");
        Assert.True(counts.GetProperty("FL020").GetInt32() >= 1);
    }

    [Fact]
    public async Task Migration_changes_no_source_file()
    {
        string[] Before() => [.. Directory.EnumerateFiles(FlashFixtures.Root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(f => f + "=" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))))];
        var before = Before();
        foreach (var name in new[] { "flx-hello", "flx-platformer", "flx-gaps", "flx-embeds", "flx-missing-embed" })
        {
            await Migrate(name);
        }

        Assert.Equal(before, Before());
    }

    [Fact]
    public async Task A_non_empty_output_folder_is_refused_and_left_alone()
    {
        var output = Path.Combine(_temp, "occupied");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "keep.txt"), "keep");
        await Assert.ThrowsAsync<OutputGuardException>(() => FlashMigrator.MigrateAsync(FlashFixtures.Of("flx-hello"), output));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(output, "keep.txt")));
    }

    [Fact]
    public async Task Output_inside_the_source_is_refused()
    {
        await Assert.ThrowsAsync<OutputGuardException>(() => FlashMigrator.MigrateAsync(FlashFixtures.Of("flx-hello"), Path.Combine(FlashFixtures.Of("flx-hello"), "out")));
        Assert.False(Directory.Exists(Path.Combine(FlashFixtures.Of("flx-hello"), "out")));
    }

    [Fact]
    public async Task Cancelled_migration_leaves_no_finished_output()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var output = Path.Combine(_temp, "cancelled");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FlashMigrator.MigrateAsync(FlashFixtures.Of("flx-hello"), output, false, null, cts.Token));
        Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData("flx-swf-only")]
    [InlineData("flx-fla-only")]
    [InlineData("flx-two-entries")]
    [InlineData("flx-not-flash")]
    public async Task Projects_that_cannot_be_converted_are_refused_before_anything_is_written(string fixture)
    {
        var output = Path.Combine(_temp, fixture);
        await Assert.ThrowsAsync<InvalidOperationException>(() => FlashMigrator.MigrateAsync(FlashFixtures.Of(fixture), output));
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task Missing_embeds_are_noted_and_do_not_stop_the_migration()
    {
        var result = await Migrate("flx-missing-embed");
        Assert.Contains(result.Notes, n => n.Contains("missing", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(result.OutputFolder, "Content", "Content.mgcb")));
    }
}
