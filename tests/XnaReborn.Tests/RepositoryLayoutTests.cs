using System.Text.Json;

namespace XnaReborn.Tests;

/// <summary>Smoke tests for the repository skeleton: catalog and schema files exist and parse.</summary>
public class RepositoryLayoutTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "XnaReborn.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("XnaReborn.sln not found above the test output folder.");
    }

    [Fact]
    [Trait("Category", "Rules")]
    public void Catalog_is_valid_json_with_version_1_and_rules()
    {
        var path = Path.Combine(RepoRoot(), "rules", "xna-to-monogame.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("xna-to-monogame", root.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("rules").ValueKind);
        Assert.True(root.GetProperty("rules").GetArrayLength() > 0);
    }

    [Fact]
    [Trait("Category", "Rules")]
    public void Schema_is_valid_json_and_declares_the_rule_catalog_shape()
    {
        var path = Path.Combine(RepoRoot(), "src", "XnaReborn.Analyzer", "Rules", "RuleSchema.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var defs = doc.RootElement.GetProperty("$defs");

        Assert.True(defs.TryGetProperty("rule", out _));
        Assert.True(defs.TryGetProperty("match", out _));
        Assert.Equal(11, defs.GetProperty("match").GetProperty("oneOf").GetArrayLength());
    }
}
