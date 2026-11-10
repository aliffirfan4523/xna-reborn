using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models;
using XnaReborn.Migrator.Content;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Tests.Migrator.Content;

/// <summary>Planning of content file copies.</summary>
[Trait("Category", "Migrator")]
public class AssetCopierTests
{
    private static readonly string _project = Path.Combine(Path.GetTempPath(), "copier-fake", "Game", "Content", "Content.contentproj");
    private static readonly string _dir = Path.GetDirectoryName(_project)!;

    private static ConvertedAsset Asset(string source, AssetStatus status, string? note = null) =>
        new(new ContentItem(source, null, null, null, new Dictionary<string, string>()), "name", "I", "P", status, note);

    private static string Abs(string relative) =>
        Path.GetFullPath(Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static IReadOnlyList<FileChange> Plan(params ConvertedAsset[] assets) =>
        new AssetCopier(new ReadOnlyFileSystem()).Plan(_project, assets, "Game/Content");

    [Theory]
    [InlineData(AssetStatus.Converted)]
    [InlineData(AssetStatus.Prebuilt)]
    public void Converted_and_prebuilt_are_copied(AssetStatus status)
    {
        var change = Assert.Single(Plan(Asset("Textures/hero.png", status, "n")));

        Assert.Equal("Game/Content/Textures/hero.png", change.RelativePath);
        Assert.Equal(ChangeKind.Copied, change.Kind);
        Assert.Equal(Abs("Textures/hero.png"), change.SourcePath);
        Assert.Null(change.NewText);
        Assert.Null(change.OriginalText);
    }

    [Theory]
    [InlineData(AssetStatus.ManualWork)]
    [InlineData(AssetStatus.Conflict)]
    public void Manual_work_and_conflict_carry_the_note(AssetStatus status)
    {
        var change = Assert.Single(Plan(Asset("a.xap", status, "do it by hand")));

        Assert.Equal(ChangeKind.ManualWork, change.Kind);
        Assert.Equal("do it by hand", change.Reason);
        Assert.Equal(Abs("a.xap"), change.SourcePath);
    }

    [Fact]
    public void Missing_is_skipped_with_a_reason_naming_the_file()
    {
        var change = Assert.Single(Plan(Asset("Textures/gone.png", AssetStatus.Missing, "Source file not found: Textures/gone.png")));

        Assert.Equal(ChangeKind.Skipped, change.Kind);
        Assert.Contains("Textures/gone.png", change.Reason);
        Assert.Null(change.SourcePath);
    }

    [Fact]
    public void Missing_without_a_note_still_names_the_file()
    {
        var change = Assert.Single(Plan(Asset("gone.png", AssetStatus.Missing)));

        Assert.Contains("gone.png", change.Reason);
    }

    [Fact]
    public void Sources_outside_the_content_folder_go_to_external()
    {
        var change = Assert.Single(Plan(Asset("../Shared/a.png", AssetStatus.Converted)));

        Assert.Equal("Game/Content/_external/a.png", change.RelativePath);
        Assert.Equal(Abs("../Shared/a.png"), change.SourcePath);
        Assert.Contains("outside the content project folder", change.Reason);
    }

    [Fact]
    public void External_note_is_combined_with_the_asset_note()
    {
        var change = Assert.Single(Plan(Asset("..\\Shared\\a.fx", AssetStatus.Converted, "Shader review needed.")));

        Assert.Contains("outside the content project folder", change.Reason);
        Assert.EndsWith("Shader review needed.", change.Reason);
    }

    [Fact]
    public void Dot_dot_that_stays_inside_is_not_external()
    {
        var change = Assert.Single(Plan(Asset("Textures/../Fonts/a.spritefont", AssetStatus.Converted)));

        Assert.StartsWith("Game/Content/", change.RelativePath);
        Assert.DoesNotContain("_external", change.RelativePath);
    }

    [Fact]
    public void Backslash_sources_and_folder_are_normalized()
    {
        var changes = new AssetCopier(new ReadOnlyFileSystem())
            .Plan(_project, [Asset("Textures\\hero.png", AssetStatus.Converted)], "Game\\Content\\");

        Assert.Equal("Game/Content/Textures/hero.png", Assert.Single(changes).RelativePath);
    }

    [Fact]
    public void Changes_are_sorted_by_path_and_deterministic()
    {
        var assets = new[]
        {
            Asset("b.png", AssetStatus.Converted),
            Asset("../x/c.png", AssetStatus.Converted),
            Asset("A.png", AssetStatus.Missing),
            Asset("a.png", AssetStatus.ManualWork),
        };

        var first = Plan(assets);
        var second = Plan(assets.Reverse().ToArray());

        Assert.Equal(["Game/Content/A.png", "Game/Content/_external/c.png", "Game/Content/a.png", "Game/Content/b.png"], first.Select(c => c.RelativePath).ToArray());
        Assert.Equal(first, second);
    }

    [Fact]
    public void Empty_input_gives_no_changes()
    {
        Assert.Empty(Plan());
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var copier = new AssetCopier(new ReadOnlyFileSystem());

        Assert.Throws<ArgumentNullException>(() => new AssetCopier(null!));
        Assert.Throws<ArgumentNullException>(() => copier.Plan(null!, [], "x"));
        Assert.Throws<ArgumentNullException>(() => copier.Plan(_project, null!, "x"));
        Assert.Throws<ArgumentNullException>(() => copier.Plan(_project, [], null!));
    }
}
