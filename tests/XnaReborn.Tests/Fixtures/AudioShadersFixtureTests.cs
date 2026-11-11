using System.Xml.Linq;
using XnaReborn.Core.Content;
using XnaReborn.Tests.Safety;

namespace XnaReborn.Tests.Fixtures;

/// <summary>Checks the fixtures and data files added for the audio-and-shaders spec (task 1).</summary>
public sealed class AudioShadersFixtureTests
{
    private static readonly string[] _newFixtures = ["xna40-xact", "xna40-xact-compiled", "xna40-effects"];

    [Fact]
    public void Xap_fixture_has_balanced_braces_and_no_wave_files()
    {
        var root = FixturePaths.Of("xna40-xact");
        var text = File.ReadAllText(Path.Combine(root, "Content", "Sounds.xap"));

        Assert.Equal(text.Count(c => c == '{'), text.Count(c => c == '}'));
        Assert.Empty(Directory.EnumerateFiles(root, "*.wav", SearchOption.AllDirectories));
    }

    [Fact]
    public void Xap_fixture_names_the_waves_the_design_lists()
    {
        var text = File.ReadAllText(Path.Combine(FixturePaths.Of("xna40-xact"), "Content", "Sounds.xap"));

        Assert.Contains("Streaming = 1;", text, StringComparison.Ordinal);
        Assert.Contains("Streaming = 0;", text, StringComparison.Ordinal);
        Assert.Contains("Audio\\ghost.wav", text, StringComparison.Ordinal);
        Assert.Contains("Loop Count = 255;", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Compiled_fixture_has_banks_and_no_xap()
    {
        var root = FixturePaths.Of("xna40-xact-compiled");
        var names = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();

        Assert.DoesNotContain(names, n => n!.EndsWith(".xap", StringComparison.Ordinal));
        Assert.Contains("Sounds.xgs", names);
        Assert.Contains("Wave Bank.xwb", names);
        Assert.Contains("Sound Bank.xsb", names);
        Assert.Contains("AudioEngine(", File.ReadAllText(Path.Combine(root, "Game1.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Effect_files_are_each_under_40_lines()
    {
        var files = Directory.EnumerateFiles(FixturePaths.Of("xna40-effects"), "*.fx*", SearchOption.AllDirectories).ToList();

        Assert.Equal(12, files.Count);
        foreach (var file in files)
        {
            Assert.True(File.ReadAllLines(file).Length < 40, file);
        }
    }

    [Fact]
    public void Effect_content_project_lists_every_fx_file_but_not_the_include()
    {
        var root = FixturePaths.Of("xna40-effects");
        var listed = XDocument.Load(Path.Combine(root, "Content", "Content.contentproj")).Descendants()
            .Where(e => e.Name.LocalName == "Compile").Select(e => (string)e.Attribute("Include")!).Order().ToList();
        var onDisk = Directory.EnumerateFiles(Path.Combine(root, "Content"), "*.fx").Select(f => Path.GetFileName(f)!).Order().ToList();

        Assert.Equal(onDisk, listed);
    }

    [Theory]
    [InlineData("xna40-xact")]
    [InlineData("xna40-xact-compiled")]
    [InlineData("xna40-effects")]
    public void Fixture_state_helper_covers_the_new_folders(string name)
    {
        var root = FixturePaths.Of(name);

        var first = FolderState.Capture(root);

        Assert.Equal(first, FolderState.Capture(root));
        Assert.Contains(first, line => line.StartsWith("F|README.md|", StringComparison.Ordinal));
        Assert.Contains(first, line => line.StartsWith("F|Game1.cs|", StringComparison.Ordinal));
    }

    [Fact]
    public void New_fixture_names_are_listed()
    {
        foreach (var name in _newFixtures)
        {
            Assert.Contains(name, FixturePaths.AllNames);
        }
    }

    [Fact]
    public void Xact_features_data_loads_with_status_and_source_on_every_entry()
    {
        var features = XactFeatures.Default;

        Assert.NotEmpty(features.Features);
        foreach (var feature in features.Features)
        {
            Assert.Contains(feature.Status, new[] { "confirmed", "expected, unverified" });
            Assert.False(string.IsNullOrWhiteSpace(feature.Source));
        }

        Assert.Equal(["0", "1", "2", "3", "4"], features.ForGroup("variationType").Select(f => f.Key));
        Assert.All(features.ForGroup("variationType"), f => Assert.True(f.IsConfirmed));
        Assert.Contains("DSP", features.UnsupportedTerms);
        Assert.Equal("255", features.ForGroup("loopCount").First().Key);
    }

    [Theory]
    [InlineData("""{"version":2,"features":[]}""")]
    [InlineData("""{"version":1}""")]
    [InlineData("""{"version":1,"features":[{"group":"unit"}]}""")]
    [InlineData("not json")]
    public void Xact_features_parse_rejects_bad_input(string json) =>
        Assert.Throws<FormatException>(() => XactFeatures.Parse(json));
}
