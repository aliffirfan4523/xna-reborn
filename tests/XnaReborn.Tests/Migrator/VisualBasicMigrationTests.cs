using System.Xml.Linq;
using XnaReborn.Core.Models;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Projects;
using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;
using XnaReborn.Tests.Fixtures;
using Xunit.Abstractions;

namespace XnaReborn.Tests.Migrator;

/// <summary>Older-and-vb tasks 18 to 20: language aware rewriter contracts, Visual Basic project generation and the Visual Basic rewriters.</summary>
[Trait("Category", "Migrator")]
public sealed class VisualBasicMigrationTests(ITestOutputHelper testOutput) : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-vb-" + Guid.NewGuid().ToString("N"));

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

    private static readonly string[] _graphics = ["Microsoft.Xna.Framework.Graphics"];

    private static SourceRewriteContext Vb(string text, IReadOnlyList<string>? imports = null) =>
        new("Game1.vb", text, ["WINDOWS"], XnaPlatform.Windows, SourceLanguage.VisualBasic, imports);

    private static XnaReborn.Migrator.MigrationResult Done(XnaReborn.Migrator.MigrationResult result)
    {
        Assert.True(result.Completed);
        return result;
    }

    private static FileChange Single(XnaReborn.Migrator.MigrationResult result, string path) => result.Changes.Single(c => c.RelativePath == path);

    private static void UseRealImages(string folder)
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        foreach (var file in Directory.EnumerateFiles(folder, "*.png", SearchOption.AllDirectories))
        {
            File.WriteAllBytes(file, png);
        }
    }

    // ---------- registry and engine (task 18) ----------
    private sealed class FakeRewriter(string id, SourceLanguage language, string suffix) : ISourceRewriter
    {
        public string RuleId => id;

        public SourceLanguage Language => language;

        public SourceRewriteResult Rewrite(SourceRewriteContext context) => new(context.Text + suffix, []);
    }

    [Fact]
    public void Two_rewriters_for_one_rule_with_different_languages_register_and_the_same_pair_throws()
    {
        var registry = new RewriterRegistry();
        registry.Register(new FakeRewriter("R1", SourceLanguage.CSharp, "c"));
        registry.Register(new FakeRewriter("R1", SourceLanguage.VisualBasic, "v"));

        Assert.Throws<ArgumentException>(() => registry.Register(new FakeRewriter("R1", SourceLanguage.VisualBasic, "x")));
        Assert.Equal(["R1"], registry.RuleIds);
        Assert.True(registry.TryGet("R1", out var csharp));
        Assert.Equal(SourceLanguage.CSharp, csharp.Language);
        Assert.True(registry.TryGet("R1", SourceLanguage.VisualBasic, out var vb));
        Assert.Equal(SourceLanguage.VisualBasic, vb.Language);
        Assert.False(registry.TryGet("R2", SourceLanguage.VisualBasic, out _));
    }

    [Fact]
    public void Engine_runs_only_the_rewriters_of_the_files_language()
    {
        var registry = new RewriterRegistry();
        registry.Register(new FakeRewriter("R1", SourceLanguage.CSharp, "c"));
        registry.Register(new FakeRewriter("R1", SourceLanguage.VisualBasic, "v"));
        var engine = new SourceRewriteEngine(registry);
        var enabled = new HashSet<string> { "R1" };

        Assert.Equal("textc", engine.Rewrite("a.cs", "text", [], enabled).Text);
        Assert.Equal("textv", engine.Rewrite("a.vb", "text", [], enabled, XnaPlatform.Unknown, SourceLanguage.VisualBasic).Text);
    }

    [Fact]
    public void Vb_rewriters_exist_for_the_vb_fixable_rules_and_the_marker_rules_only()
    {
        var registry = DefaultRewriters.Create();

        var vbIds = registry.All.Where(r => r.Language == SourceLanguage.VisualBasic).Select(r => r.RuleId).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["ENTRYPOINT", "XNA022", "XNA024", "XNA039", "XNA040"], vbIds);
        Assert.Equal(VisualBasicMarkerRewriter.RuleIds.Order(StringComparer.Ordinal), vbIds.Where(id => id is "XNA024" or "XNA039" or "XNA040"));
    }

    [Fact]
    public void Marker_prefixes_cover_both_languages()
    {
        Assert.True(MarkerPrefixes.ContainsMarker("    // XNAREBORN: XNA1: x"));
        Assert.True(MarkerPrefixes.ContainsMarker("    ' XNAREBORN: XNA1: x"));
        Assert.False(MarkerPrefixes.ContainsMarker("    ' just a comment"));
    }

    // ---------- marker inserter (task 20.1) ----------
    [Fact]
    public void Vb_marker_goes_above_the_statement_with_its_indent_and_line_ending()
    {
        var text = "Class C\r\n    Sub M()\r\n        Dim x = Foo(1,\r\n            Bar)\r\n    End Sub\r\nEnd Class\r\n";
        var position = text.IndexOf("Bar", StringComparison.Ordinal);

        var after = new VisualBasicMarkerInserter().InsertMarker(text, position, "XNA024", "Fix this\r\nby hand.");

        Assert.Equal("Class C\r\n    Sub M()\r\n        ' XNAREBORN: XNA024: Fix this by hand.\r\n        Dim x = Foo(1,\r\n            Bar)\r\n    End Sub\r\nEnd Class\r\n", after);
    }

    [Fact]
    public void Vb_marker_is_not_added_twice_or_inside_a_comment_or_string()
    {
        var inserter = new VisualBasicMarkerInserter();
        var text = "Class C\n    Sub M()\n        Dim s = \"RenderState\" ' RenderState\n        Dim t = x.RenderState\n    End Sub\nEnd Class\n";

        Assert.Equal(text, inserter.InsertMarker(text, text.IndexOf("\"RenderState", StringComparison.Ordinal) + 3, "XNA024", "m"));
        Assert.Equal(text, inserter.InsertMarker(text, text.IndexOf("' RenderState", StringComparison.Ordinal) + 4, "XNA024", "m"));
        var once = inserter.InsertMarker(text, text.IndexOf("x.RenderState", StringComparison.Ordinal), "XNA024", "m");
        Assert.Contains("        ' XNAREBORN: XNA024: m\n        Dim t = x.RenderState", once, StringComparison.Ordinal);
        Assert.Equal(once, inserter.InsertMarker(once, once.IndexOf("x.RenderState", StringComparison.Ordinal), "XNA024", "m"));
    }

    // ---------- entry point (task 20.2) ----------
    [Fact]
    public void Entry_point_conditional_around_shared_sub_main_is_removed()
    {
        const string text = "#If WINDOWS OrElse XBOX Then\nFriend Class Program\n    Shared Sub Main(ByVal args As String())\n    End Sub\nEnd Class\n#End If\n";

        var result = new VisualBasicEntryPointSourceRewriter().Rewrite(Vb(text));

        Assert.Equal("Friend Class Program\n    Shared Sub Main(ByVal args As String())\n    End Sub\nEnd Class\n", result.Text);
        Assert.DoesNotContain(result.Notes, n => n.NeedsManualWork);
    }

    [Fact]
    public void Entry_point_conditional_in_a_module_is_removed_and_other_shapes_are_left_alone()
    {
        const string module = "#If WINDOWS Then\nModule M\n    Sub Main()\n    End Sub\nEnd Module\n#End If\n";
        const string withElse = "#If WINDOWS Then\nModule M\n    Sub Main()\n    End Sub\nEnd Module\n#Else\n#End If\n";
        const string custom = "#If MYFLAG Then\nModule M\n    Sub Main()\n    End Sub\nEnd Module\n#End If\n";
        const string plain = "Module M\n    Sub Main()\n    End Sub\nEnd Module\n";
        var rewriter = new VisualBasicEntryPointSourceRewriter();

        Assert.Equal("Module M\n    Sub Main()\n    End Sub\nEnd Module\n", rewriter.Rewrite(Vb(module)).Text);
        Assert.Equal(withElse, rewriter.Rewrite(Vb(withElse)).Text);
        Assert.Contains(rewriter.Rewrite(Vb(withElse)).Notes, n => n.NeedsManualWork);
        Assert.Equal(custom, rewriter.Rewrite(Vb(custom)).Text);
        Assert.Equal(plain, rewriter.Rewrite(Vb(plain)).Text);
    }

    // ---------- SpriteBatch.Begin (task 20.3) ----------
    [Fact]
    public void Blend_mode_map_maps_three_members_and_nothing_else()
    {
        Assert.True(BlendModeMap.TryGet("None", out var none));
        Assert.Equal("Opaque", none);
        Assert.True(BlendModeMap.TryGet("Additive", out var additive));
        Assert.Equal("Additive", additive);
        Assert.False(BlendModeMap.TryGet("Subtractive", out _));
    }

    [Theory]
    [InlineData("spriteBatch.Begin(SpriteBlendMode.AlphaBlend)", "spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend)")]
    [InlineData("spriteBatch.Begin(spriteblendmode.additive)", "spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive)")]
    [InlineData("spriteBatch.Begin(blendMode:=SpriteBlendMode.None)", "spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque)")]
    [InlineData("spriteBatch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Immediate, SaveStateMode.None)", "spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend)")]
    public void Sprite_batch_begin_is_rewritten(string before, string after)
    {
        var text = "Imports Microsoft.Xna.Framework.Graphics\nClass C\n    Sub M()\n        " + before + "\n    End Sub\nEnd Class\n";

        var result = new VisualBasicSpriteBatchBeginRewriter().Rewrite(Vb(text));

        Assert.Equal(text.Replace(before, after, StringComparison.Ordinal), result.Text);
        Assert.DoesNotContain(result.Notes, n => n.NeedsManualWork);
    }

    [Fact]
    public void Sprite_batch_begin_uses_the_project_level_import_and_marks_what_it_cannot_change()
    {
        const string text = "Class C\n    Private mode As SpriteBlendMode\n    Sub M()\n        spriteBatch.Begin(SpriteBlendMode.Additive)\n        spriteBatch.Begin(SpriteBlendMode.Subtractive)\n    End Sub\nEnd Class\n";
        var rewriter = new VisualBasicSpriteBatchBeginRewriter();

        var result = rewriter.Rewrite(Vb(text, _graphics));

        Assert.Contains("spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive)", result.Text, StringComparison.Ordinal);
        Assert.Contains("        ' XNAREBORN: XNA022:", result.Text, StringComparison.Ordinal);
        Assert.Contains("spriteBatch.Begin(SpriteBlendMode.Subtractive)", result.Text, StringComparison.Ordinal);
        Assert.Contains(result.Notes, n => n.NeedsManualWork);
        Assert.Equal(text, rewriter.Rewrite(Vb(text)).Text);
    }

    [Fact]
    public void A_user_type_named_sprite_blend_mode_without_the_import_is_left_alone()
    {
        const string text = "Class SpriteBlendMode\nEnd Class\nClass C\n    Dim m As SpriteBlendMode\nEnd Class\n";

        Assert.Equal(text, new VisualBasicSpriteBatchBeginRewriter().Rewrite(Vb(text)).Text);
    }

    // ---------- marker rewriters (task 20.4) ----------
    [Fact]
    public void Marker_rewriters_mark_render_state_and_effect_calls_without_changing_code()
    {
        const string text = "Imports Microsoft.Xna.Framework.Graphics\nClass C\n    Sub M()\n        With device.RenderState\n            .CullMode = 1\n        End With\n        effect.Begin\n        pass.Begin()\n        other.Begin()\n    End Sub\nEnd Class\n";
        var all = VisualBasicMarkerRewriter.CreateAll();

        var result = text;
        foreach (var rewriter in all)
        {
            result = rewriter.Rewrite(Vb(result)).Text;
        }

        Assert.Contains("        ' XNAREBORN: XNA024:", result, StringComparison.Ordinal);
        Assert.Contains("        ' XNAREBORN: XNA040:", result, StringComparison.Ordinal);
        Assert.Contains("        ' XNAREBORN: XNA039:", result, StringComparison.Ordinal);
        Assert.Equal(text, string.Join("\n", result.Split('\n').Where(l => !l.Contains("XNAREBORN", StringComparison.Ordinal))));
        Assert.Equal(3, result.Split('\n').Count(l => l.Contains("XNAREBORN", StringComparison.Ordinal)));
        Assert.Equal(result, all.Aggregate(result, (current, rewriter) => rewriter.Rewrite(Vb(current)).Text));
    }

    // ---------- project and solution (task 19) ----------
    [Fact]
    public async Task Vb_40_fixture_gets_an_sdk_vbproj_with_my_type_empty_options_imports_and_no_csharp_properties()
    {
        var original = FixturePaths.Of("vb-xna40-basic");
        var before = MigrationTestSupport.Tree(original);
        var output = Path.Combine(_temp, "vb40");

        var result = Done(await MigrationTestSupport.MigrateAsync(original, MigrationTestSupport.Options(output)));

        var project = XDocument.Parse(Single(result, "VbXna40Basic.vbproj").NewText!);
        Assert.Equal("Microsoft.NET.Sdk", project.Root!.Attribute("Sdk")!.Value);
        string Property(string name) => project.Descendants(name).Single().Value;
        Assert.Equal("Empty", Property("MyType"));
        Assert.Equal("On", Property("OptionExplicit"));
        Assert.Equal("Off", Property("OptionStrict"));
        Assert.Equal("Binary", Property("OptionCompare"));
        Assert.Equal("On", Property("OptionInfer"));
        Assert.Equal("net8.0", Property("TargetFramework"));
        Assert.Equal("false", Property("GenerateAssemblyInfo"));
        Assert.Empty(project.Descendants("Nullable"));
        Assert.Empty(project.Descendants("ImplicitUsings"));
        Assert.Empty(project.Descendants("NoStdLib"));
        Assert.Equal(
            ["Microsoft.Xna.Framework", "Microsoft.Xna.Framework.Graphics", "Microsoft.Xna.Framework.Input", "System", "System.Collections.Generic"],
            project.Descendants("Import").Select(i => i.Attribute("Include")!.Value));
        Assert.Contains("MonoGameContentReference", project.ToString(), StringComparison.Ordinal);
        Assert.Contains(result.Changes, c => c.RelativePath == "My Project/AssemblyInfo.vb");
        Assert.Equal(before, MigrationTestSupport.Tree(original));
    }

    [Fact]
    public async Task Vb_entry_point_conditional_is_removed_in_the_migrated_program()
    {
        var output = Path.Combine(_temp, "vb40-program");

        var result = Done(await MigrationTestSupport.MigrateAsync(FixturePaths.Of("vb-xna40-basic"), MigrationTestSupport.Options(output)));

        var program = Single(result, "Program.vb").NewText!;
        Assert.DoesNotContain("#If", program, StringComparison.Ordinal);
        Assert.Contains("Shared Sub Main", program, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handmade_vb_project_omits_my_designer_files_rewrites_begin_and_marks_the_rest()
    {
        var output = Path.Combine(_temp, "vb31");

        var result = Done(await MigrationTestSupport.MigrateAsync(FixturePaths.Of("vb-xna31-handmade"), MigrationTestSupport.Options(output)));

        var project = XDocument.Parse(Single(result, "VbXna31Handmade.vbproj").NewText!);
        Assert.Equal("Empty", project.Descendants("MyType").Single().Value);
        Assert.Empty(project.Descendants("StartupObject"));
        var designer = Single(result, "My Project/Application.Designer.vb");
        Assert.Equal(ChangeKind.Skipped, designer.Kind);
        Assert.False(File.Exists(Path.Combine(output, "My Project", "Application.Designer.vb")));
        var game = Single(result, "Game1.vb").NewText!;
        Assert.Contains("spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend)", game, StringComparison.Ordinal);
        Assert.Contains("' XNAREBORN: XNA024:", game, StringComparison.Ordinal);
        Assert.Contains("' XNAREBORN: XNA040:", game, StringComparison.Ordinal);
        Assert.Contains("My.Application.Info.Title", game, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mixed_solution_lists_the_vb_project_with_the_vb_project_type()
    {
        var output = Path.Combine(_temp, "mixed");

        var result = Done(await MigrationTestSupport.MigrateAsync(FixturePaths.Of("mixed-cs-vb-sln"), MigrationTestSupport.Options(output)));

        var solution = result.Changes.Single(c => c.RelativePath.EndsWith(".sln", StringComparison.Ordinal)).NewText!;
        Assert.Contains("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Game\"", solution, StringComparison.Ordinal);
        Assert.Contains("{F184B08F-C81C-45F6-A57F-5ABD9991F28F}\") = \"Lib\"", solution, StringComparison.Ordinal);
        Assert.Contains(result.Changes, c => c.RelativePath == "Lib/Lib.vbproj");
        Assert.Contains(result.Changes, c => c.RelativePath == "Lib/Helper.vb");
    }

    [IntegrationFact]
    public async Task Migrated_vb_40_fixture_builds()
    {
        var output = Path.Combine(_temp, "vb40-build");
        Done(await MigrationTestSupport.MigrateAsync(FixturePaths.Of("vb-xna40-basic"), MigrationTestSupport.Options(output)));
        UseRealImages(output);

        var (exitCode, text) = await MigrationIntegrationTests.RunBuildAsync(output);
        testOutput.WriteLine(text);

        Assert.True(exitCode == 0, $"dotnet build failed with exit code {exitCode}.{Environment.NewLine}{text}");
    }
}
