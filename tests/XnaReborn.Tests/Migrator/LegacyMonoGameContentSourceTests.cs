using XnaReborn.Core.Mgcb;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Content;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator;

/// <summary>Monogame-upgrade tasks 12 and 13: the mgcb upgrade and the source rewriters, as units.</summary>
[Trait("Category", "Migrator")]
public class LegacyMonoGameContentSourceTests
{
    private const string _installReference = "/reference:..\\..\\..\\..\\Program Files (x86)\\MonoGame\\v3.0\\Tools\\MonoGame.Content.Pipeline.dll";

    private static string Mgcb(string fixture, string relative = "Content/Content.mgcb") =>
        File.ReadAllText(Path.Combine(FixturePaths.Of(fixture), relative.Replace('/', Path.DirectorySeparatorChar))).Replace("\r\n", "\n", StringComparison.Ordinal);

    // ---- MgcbUpgrader ----

    [Fact]
    public void Install_reference_is_removed_and_a_custom_reference_stays()
    {
        var before = Mgcb("mg37-desktopgl-installdir");

        var upgrade = MgcbUpgrader.Upgrade(before, "DesktopGL");

        Assert.True(upgrade.Changed);
        Assert.Single(upgrade.RemovedReferences);
        Assert.Equal(["Pipeline\\GlowPipeline.dll"], upgrade.KeptReferences);
        Assert.DoesNotContain("Program Files", upgrade.Text, StringComparison.Ordinal);
        Assert.Contains("/reference:Pipeline\\GlowPipeline.dll", upgrade.Text);
        Assert.Equal(before.Replace(_installReference + "\n", string.Empty, StringComparison.Ordinal), upgrade.Text);
    }

    [Fact]
    public void Platform_is_set_to_the_target_and_nothing_else_changes()
    {
        var before = Mgcb("mg37-windowsdx-pkgconfig");

        var upgrade = MgcbUpgrader.Upgrade(before, "DesktopGL");

        Assert.Equal("Windows", upgrade.OldPlatform);
        Assert.Equal(before.Replace("/platform:Windows", "/platform:DesktopGL", StringComparison.Ordinal), upgrade.Text);
    }

    [Fact]
    public void A_file_that_is_already_right_is_returned_unchanged()
    {
        var before = Mgcb("mg38-current");

        var upgrade = MgcbUpgrader.Upgrade(before, "DesktopGL");

        Assert.False(upgrade.Changed);
        Assert.Equal(before, upgrade.Text);
        Assert.Empty(upgrade.RemovedReferences);
    }

    [Fact]
    public void Entry_order_and_asset_names_are_unchanged()
    {
        var before = Mgcb("mg37-desktopgl-installdir");

        var after = MgcbUpgrader.Upgrade(before, "Windows").Text;

        Assert.Equal(
            MgcbParser.Parse(before).Entries.Select(e => (e.Source, e.Importer, e.Processor, e.Parameters.Count)),
            MgcbParser.Parse(after).Entries.Select(e => (e.Source, e.Importer, e.Processor, e.Parameters.Count)));
        Assert.Equal("Windows", MgcbParser.Parse(after).Setting("platform")!.Value);
    }

    [Fact]
    public void Upgrade_is_deterministic_and_idempotent()
    {
        var before = Mgcb("mg37-desktopgl-installdir");

        var first = MgcbUpgrader.Upgrade(before, "DesktopGL");
        var second = MgcbUpgrader.Upgrade(before, "DesktopGL");
        var again = MgcbUpgrader.Upgrade(first.Text, "DesktopGL");

        Assert.Equal(first.Text, second.Text);
        Assert.False(again.Changed);
    }

    [Fact]
    public void Crlf_line_endings_stay()
    {
        var before = Mgcb("mg37-desktopgl-installdir").Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);

        var after = MgcbUpgrader.Upgrade(before, "Windows").Text;

        Assert.DoesNotContain("\n", after.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("/platform:Windows\r\n", after, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void A_missing_platform_line_is_added_after_the_last_option(string newLine)
    {
        var before = string.Join(newLine, "/outputDir:bin", "/intermediateDir:obj", "", "#begin a.png", "/build:a.png", "");

        var after = MgcbUpgrader.Upgrade(before, "DesktopGL").Text;

        Assert.Equal(string.Join(newLine, "/outputDir:bin", "/intermediateDir:obj", "/platform:DesktopGL", "", "#begin a.png", "/build:a.png", ""), after);
    }

    [Fact]
    public void Install_references_stay_when_the_step_is_switched_off()
    {
        var before = Mgcb("mg37-desktopgl-installdir");

        var upgrade = MgcbUpgrader.Upgrade(before, "DesktopGL", removeInstallReferences: false);

        Assert.Contains("Program Files", upgrade.Text, StringComparison.Ordinal);
        Assert.Empty(upgrade.RemovedReferences);
    }

    [Theory]
    [InlineData("..\\..\\Program Files (x86)\\MonoGame\\v3.0\\Tools\\x.dll", true)]
    [InlineData("$(MonoGameInstallDirectory)/MonoGame/v3.0/Tools/x.dll", true)]
    [InlineData("Pipeline\\Mine.dll", false)]
    public void Install_folder_references_are_told_apart(string path, bool install) =>
        Assert.Equal(install, MgcbUpgrader.IsInstallFolderReference(path));

    // ---- ColorTransparentBlackRewriter ----

    private static SourceRewriteResult Color(string text) =>
        new ColorTransparentBlackRewriter().Rewrite(new SourceRewriteContext("Colors.cs", text, []));

    [Fact]
    public void Transparent_black_becomes_transparent_in_a_file_that_imports_xna()
    {
        var before = File.ReadAllText(Path.Combine(FixturePaths.Of("mg37-source-apis"), "Colors.cs"));

        var result = Color(before);

        Assert.Equal(before.Replace("TransparentBlack", "Transparent", StringComparison.Ordinal), result.Text);
        Assert.Contains(result.Notes, n => !n.NeedsManualWork);
    }

    [Fact]
    public void A_file_with_the_supported_member_is_not_touched()
    {
        var before = File.ReadAllText(Path.Combine(FixturePaths.Of("mg37-source-apis"), "SafeColors.cs"));

        var result = Color(before);

        Assert.Same(before, result.Text);
        Assert.Empty(result.Notes);
    }

    [Theory]
    [InlineData("using Alias = Microsoft.Xna.Framework.Color;\nclass A { object x = Alias.TransparentBlack; }\n", "Alias.Transparent;")]
    [InlineData("class A { object x = Microsoft.Xna.Framework.Color.TransparentBlack; }\n", "Color.Transparent;")]
    [InlineData("using Microsoft.Xna.Framework;\nclass A { object x = Color.TransparentBlack; object y = Color.TransparentBlack; }\n", "Color.Transparent; object y = Color.Transparent;")]
    public void Alias_and_full_name_are_recognized(string before, string expected)
    {
        var result = Color(before);

        Assert.Contains(expected, result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("TransparentBlack", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_color_the_file_does_not_show_to_be_xna_gets_a_marker_and_no_change()
    {
        const string before = "using System.Drawing;\nclass A\n{\n    object x = Color.TransparentBlack;\n}\n";

        var result = Color(before);

        Assert.Contains("TransparentBlack", result.Text, StringComparison.Ordinal);
        Assert.Contains("// XNAREBORN: MG032:", result.Text, StringComparison.Ordinal);
        Assert.Contains(result.Notes, n => n.NeedsManualWork);
        Assert.Equal(before, result.Text.Replace(result.Text.Split('\n').Single(l => l.Contains("XNAREBORN", StringComparison.Ordinal)) + "\n", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void Another_member_named_transparent_black_is_not_touched()
    {
        const string before = "using Microsoft.Xna.Framework;\nclass A { object x = Palette.TransparentBlack; }\n";

        var result = Color(before);

        Assert.Same(before, result.Text);
    }

    // ---- NamespaceMarkerRewriter ----

    [Theory]
    [InlineData("MG028", "NetCode.cs")]
    [InlineData("MG029", "Services.cs")]
    [InlineData("MG030", "GlCode.cs")]
    [InlineData("MG031", "UwpCode.cs")]
    public void Manual_source_findings_get_a_marker_above_the_using(string ruleId, string file)
    {
        var before = File.ReadAllText(Path.Combine(FixturePaths.Of("mg37-source-apis"), file));
        var rewriter = DefaultRewriters.Create().TryGet(ruleId, out var found) ? found : throw new InvalidOperationException(ruleId);

        var result = rewriter.Rewrite(new SourceRewriteContext(file, before, []));

        Assert.StartsWith("// XNAREBORN: " + ruleId + ":", result.Text, StringComparison.Ordinal);
        Assert.EndsWith(before, result.Text, StringComparison.Ordinal);
        Assert.Contains(result.Notes, n => n.NeedsManualWork);
    }

    [Theory]
    [InlineData("MG028", "Colors.cs")]
    [InlineData("MG030", "NetCode.cs")]
    [InlineData("MG031", "GlCode.cs")]
    public void A_file_without_the_namespace_gets_no_marker(string ruleId, string file)
    {
        var before = File.ReadAllText(Path.Combine(FixturePaths.Of("mg37-source-apis"), file));
        DefaultRewriters.Create().TryGet(ruleId, out var rewriter);

        var result = rewriter!.Rewrite(new SourceRewriteContext(file, before, []));

        Assert.Same(before, result.Text);
    }

    // ---- EntryPointRewriter on a MonoGame template ----

    [Fact]
    public void Main_inside_if_windows_or_linux_is_unwrapped()
    {
        const string before = "#if WINDOWS || LINUX\nusing System;\nnamespace G\n{\n    public static class Program\n    {\n        [STAThread]\n        static void Main()\n        {\n            using (var g = new Game1()) g.Run();\n        }\n    }\n}\n#endif\n";

        var result = new EntryPointSourceRewriter().Rewrite(new SourceRewriteContext("Program.cs", before, ["WINDOWS"]));

        Assert.DoesNotContain("#if", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("#endif", result.Text, StringComparison.Ordinal);
        Assert.Contains("static void Main()", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_inside_a_conditional_with_another_symbol_is_left_with_a_manual_note()
    {
        const string before = "#if WINDOWS || MONOMAC\nclass P { static void Main() { } }\n#endif\n";

        var result = new EntryPointSourceRewriter().Rewrite(new SourceRewriteContext("Program.cs", before, ["WINDOWS"]));

        Assert.Equal(before, result.Text);
        Assert.Contains(result.Notes, n => n.NeedsManualWork);
    }
}
