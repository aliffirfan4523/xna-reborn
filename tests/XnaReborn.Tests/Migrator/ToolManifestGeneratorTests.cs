using System.Text.Json;
using VerifyXunit;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Projects;

namespace XnaReborn.Tests.Migrator;

/// <summary>The tool manifest pins dotnet-mgcb.</summary>
[Trait("Category", "Migrator")]
public class ToolManifestGeneratorTests
{
    private readonly ToolManifestGenerator _generator = new();

    [Fact]
    public void Change_is_a_created_manifest_with_a_reason()
    {
        var change = _generator.Generate("3.8.6.1");

        Assert.Equal(".config/dotnet-tools.json", change.RelativePath);
        Assert.Equal(ChangeKind.Created, change.Kind);
        Assert.Null(change.OriginalText);
        Assert.Contains("manifest", change.Reason);
    }

    [Fact]
    public void Content_is_valid_json_that_pins_the_version()
    {
        var text = _generator.Generate("3.8.6.1").NewText!;

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.True(root.GetProperty("isRoot").GetBoolean());
        var tool = root.GetProperty("tools").GetProperty("dotnet-mgcb");
        Assert.Equal("3.8.6.1", tool.GetProperty("version").GetString());
        Assert.Equal("mgcb", tool.GetProperty("commands")[0].GetString());
        Assert.False(tool.GetProperty("rollForward").GetBoolean());
    }

    [Fact]
    public void Content_also_pins_the_effect_compiler_to_the_same_version()
    {
        using var doc = JsonDocument.Parse(_generator.Generate("3.8.6.1").NewText!);

        var tool = doc.RootElement.GetProperty("tools").GetProperty("dotnet-mgfxc");
        Assert.Equal("3.8.6.1", tool.GetProperty("version").GetString());
        Assert.Equal("mgfxc", tool.GetProperty("commands")[0].GetString());
        Assert.False(tool.GetProperty("rollForward").GetBoolean());
    }

    [Fact]
    public void Text_uses_lf_and_one_trailing_newline()
    {
        var text = _generator.Generate("3.8.6.1").NewText!;

        Assert.DoesNotContain("\r", text);
        Assert.EndsWith("}\n", text);
        Assert.False(text.EndsWith("\n\n", StringComparison.Ordinal));
        Assert.False(text.StartsWith('\uFEFF'));
    }

    [Fact]
    public void Two_calls_give_equal_results() =>
        Assert.Equal(_generator.Generate("3.8.6.1"), _generator.Generate("3.8.6.1"));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Empty_version_throws(string version) =>
        Assert.Throws<ArgumentException>(() => _generator.Generate(version));

    [Fact]
    public async Task Manifest_matches_snapshot() =>
        await Verifier.Verify(_generator.Generate("3.8.6.1").NewText!, "json");
}
