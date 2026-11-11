using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Content;
using XnaReborn.Migrator.Output;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Safety;

namespace XnaReborn.Tests.Migrator.Content;

/// <summary>The effect step inside content conversion, run on the xna40-effects fixture.</summary>
[Trait("Category", "Migrator")]
public sealed class EffectMigrationTests
{
    private static async Task<ContentConversionResult> ConvertAsync(bool rewriteEffects)
    {
        var root = FixturePaths.Of("xna40-effects");
        var fileSystem = new ReadOnlyFileSystem();
        var read = await new ProjectReader(fileSystem).ReadAsync(Path.Combine(root, "Xna40Effects.csproj"), root, CancellationToken.None);
        var options = new ContentOptions(MonoGameTarget.DesktopGL, SubstituteFonts: false, RewriteEffects: rewriteEffects);
        return await new ContentConverter(fileSystem).ConvertAsync(read, root, string.Empty, options, CancellationToken.None);
    }

    private static FileChange Change(ContentConversionResult result, string path) =>
        Assert.Single(result.Changes, c => c.RelativePath == path);

    [Fact]
    public async Task Rewritable_effects_become_modified_copies_with_macros()
    {
        var result = await ConvertAsync(rewriteEffects: true);

        var sm2 = Change(result, "Content/Sm2.fx");
        Assert.Equal(ChangeKind.Modified, sm2.Kind);
        Assert.Contains("compile VS_SHADERMODEL MainVS()", sm2.NewText, StringComparison.Ordinal);
        Assert.Contains("compile vs_2_0 MainVS()", sm2.OriginalText, StringComparison.Ordinal);
        Assert.Equal(ChangeKind.Modified, Change(result, "Content/MipBias.fx").Kind);
    }

    [Fact]
    public async Task Included_file_is_copied_next_to_the_effect()
    {
        var result = await ConvertAsync(rewriteEffects: true);

        var include = Change(result, "Content/Common.fxh");
        Assert.Equal(ChangeKind.Copied, include.Kind);
        Assert.Equal(Path.Combine(FixturePaths.Of("xna40-effects"), "Content", "Common.fxh"), include.SourcePath);
    }

    [Fact]
    public async Task Notes_list_markers_by_file_and_rule()
    {
        var result = await ConvertAsync(rewriteEffects: true);

        Assert.Contains(result.ManualNotes, n => n.StartsWith("Content/AlphaTest.fx: [XNA065]", StringComparison.Ordinal));
        Assert.Contains(result.ManualNotes, n => n.StartsWith("Content/Vpos.fx: [XNA067]", StringComparison.Ordinal));
        Assert.Contains(result.ManualNotes, n => n.StartsWith("Content/Sm2.fx: [XNA064]", StringComparison.Ordinal));
        Assert.Equal(result.ManualNotes, result.Report.Notes);
    }

    [Fact]
    public async Task Effect_that_needs_no_rewrite_is_still_a_plain_copy()
    {
        var result = await ConvertAsync(rewriteEffects: true);

        Assert.Equal(ChangeKind.Copied, Change(result, "Content/Portable.fx").Kind);
    }

    [Fact]
    public async Task Turning_rewriting_off_copies_every_effect_unchanged()
    {
        var result = await ConvertAsync(rewriteEffects: false);

        Assert.DoesNotContain(result.Changes, c => c.Kind == ChangeKind.Modified);
        Assert.DoesNotContain(result.Changes, c => c.RelativePath == "Content/Common.fxh");
        Assert.DoesNotContain(result.ManualNotes, n => n.Contains("[NEW-B", StringComparison.Ordinal));
        Assert.Equal(ChangeKind.Copied, Change(result, "Content/Sm2.fx").Kind);
    }

    [Fact]
    public async Task Conversion_leaves_the_original_folder_unchanged()
    {
        var before = FolderState.Capture(FixturePaths.Of("xna40-effects"));

        await ConvertAsync(rewriteEffects: true);

        Assert.Equal(before, FolderState.Capture(FixturePaths.Of("xna40-effects")));
    }

    [Fact]
    public async Task Content_mgcb_still_builds_every_effect_with_the_effect_processor()
    {
        var result = await ConvertAsync(rewriteEffects: true);

        var mgcb = Change(result, "Content/Content.mgcb").NewText!;
        Assert.Equal(11, System.Text.RegularExpressions.Regex.Matches(mgcb, "/processor:EffectProcessor").Count);
    }
}
