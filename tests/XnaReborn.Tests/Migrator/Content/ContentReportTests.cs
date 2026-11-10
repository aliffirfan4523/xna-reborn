using XnaReborn.Core.Models;
using XnaReborn.Migrator.Content;

namespace XnaReborn.Tests.Migrator.Content;

/// <summary>Counting and ordering in <see cref="ContentReport"/>.</summary>
[Trait("Category", "Migrator")]
public class ContentReportTests
{
    private static ConvertedAsset Asset(string name, string source, string processor, AssetStatus status = AssetStatus.Converted) =>
        new(new ContentItem(source, null, null, null, new Dictionary<string, string>()), name, "I", processor, status, null);

    [Fact]
    public void Empty_report_has_every_key_with_zero_in_order()
    {
        var report = ContentReport.Create([], []);

        Assert.Equal(Enum.GetValues<AssetStatus>(), report.CountsByStatus.Keys.ToArray());
        Assert.All(report.CountsByStatus.Values, v => Assert.Equal(0, v));
        Assert.Equal(["Texture", "Font", "SoundEffect", "Song", "Model", "Effect", "Xact", "Xml", "Prebuilt", "Other"], report.CountsByType.Keys.ToArray());
        Assert.All(report.CountsByType.Values, v => Assert.Equal(0, v));
        Assert.Empty(report.Assets);
        Assert.Empty(report.Notes);
    }

    [Fact]
    public void Types_follow_the_processor_extension_and_status()
    {
        var report = ContentReport.Create(
        [
            Asset("a", "a.png", "TextureProcessor"),
            Asset("b", "b.png", "TextureProcessor"),
            Asset("c", "c.spritefont", "FontDescriptionProcessor"),
            Asset("d", "d.wav", "SoundEffectProcessor"),
            Asset("e", "e.mp3", "SongProcessor"),
            Asset("f", "f.fbx", "ModelProcessor"),
            Asset("g", "g.fx", "EffectProcessor"),
            Asset("h", "h.xap", "XactProcessor", AssetStatus.ManualWork),
            Asset("i", "i.xwb", string.Empty, AssetStatus.ManualWork),
            Asset("j", "j.xml", "PassThroughProcessor"),
            Asset("k", "k.dat", "PassThroughProcessor"),
            Asset("l", "l.xnb", string.Empty, AssetStatus.Prebuilt),
            Asset("m", "m.bin", "OutlineProcessor", AssetStatus.ManualWork),
        ], []);

        Assert.Equal(2, report.CountsByType["Texture"]);
        Assert.Equal(1, report.CountsByType["Font"]);
        Assert.Equal(1, report.CountsByType["SoundEffect"]);
        Assert.Equal(1, report.CountsByType["Song"]);
        Assert.Equal(1, report.CountsByType["Model"]);
        Assert.Equal(1, report.CountsByType["Effect"]);
        Assert.Equal(2, report.CountsByType["Xact"]);
        Assert.Equal(1, report.CountsByType["Xml"]);
        Assert.Equal(1, report.CountsByType["Prebuilt"]);
        Assert.Equal(2, report.CountsByType["Other"]);
        Assert.Equal(13, report.CountsByType.Values.Sum());
    }

    [Fact]
    public void Status_counts_include_every_status()
    {
        var report = ContentReport.Create(
        [
            Asset("a", "a.png", "TextureProcessor"),
            Asset("b", "b.png", "TextureProcessor", AssetStatus.Missing),
            Asset("c", "c.png", "TextureProcessor", AssetStatus.Missing),
            Asset("d", "d.png", "TextureProcessor", AssetStatus.Conflict),
        ], []);

        Assert.Equal(1, report.CountsByStatus[AssetStatus.Converted]);
        Assert.Equal(2, report.CountsByStatus[AssetStatus.Missing]);
        Assert.Equal(1, report.CountsByStatus[AssetStatus.Conflict]);
        Assert.Equal(0, report.CountsByStatus[AssetStatus.ManualWork]);
        Assert.Equal(0, report.CountsByStatus[AssetStatus.Prebuilt]);
    }

    [Fact]
    public void Assets_are_sorted_by_name_and_notes_are_sorted_and_distinct()
    {
        var input = new[] { Asset("b/x", "b.png", "TextureProcessor"), Asset("A", "a.png", "TextureProcessor"), Asset("a", "a2.png", "TextureProcessor") };

        var report = ContentReport.Create(input, ["zeta", "alpha", "zeta"]);

        Assert.Equal(["A", "a", "b/x"], report.Assets.Select(a => a.AssetName).ToArray());
        Assert.Equal(["alpha", "zeta"], report.Notes);
    }

    [Fact]
    public void Result_is_deterministic_for_any_input_order()
    {
        var input = new[] { Asset("b", "b.png", "TextureProcessor"), Asset("a", "a.png", "TextureProcessor") };

        var first = ContentReport.Create(input, ["n"]);
        var second = ContentReport.Create(input.Reverse(), ["n"]);

        Assert.Equal(first.Assets, second.Assets);
        Assert.Equal(first.CountsByType, second.CountsByType);
        Assert.Equal(first.CountsByStatus, second.CountsByStatus);
    }

    [Fact]
    public void Null_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => ContentReport.Create(null!, []));
        Assert.Throws<ArgumentNullException>(() => ContentReport.Create([], null!));
    }
}
