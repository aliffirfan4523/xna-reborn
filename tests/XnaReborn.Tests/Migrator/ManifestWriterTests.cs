using System.Text.Json;
using VerifyXunit;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Tests.Migrator;

/// <summary>The manifest builder and its JSON and Markdown writers.</summary>
[Trait("Category", "Migrator")]
public class ManifestWriterTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _noRules =
        new Dictionary<string, IReadOnlyList<string>>();

    private static Manifest Rich(bool completed = true) => ManifestBuilder.Build(
        "xna-to-monogame",
        completed,
        [
            new FileChange("Game1.cs", ChangeKind.Modified, "a", "b", null),
            new FileChange("Content/a|b.png", ChangeKind.Copied, null, null, null, "C:/src/a.png"),
            new FileChange("App.csproj", ChangeKind.Created, null, "<Project/>", "SDK project"),
            new FileChange("Old.sln", ChangeKind.Skipped, null, null, "Replaced by new solution"),
            new FileChange("Sound.xap", ChangeKind.ManualWork, null, null, "XACT not supported"),
        ],
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["Game1.cs"] = ["XNA024", "XNA022", "XNA022"],
            ["App.csproj"] = ["XNA001"],
        },
        new ManifestBuild(true, false, ["Game1.cs(3): error CS0103", "Game1.cs(1): error CS0246"]),
        "0.1.0");

    [Fact]
    public void Build_sorts_changes_by_path_and_rules_distinct()
    {
        var manifest = Rich();

        Assert.Equal(1, manifest.Version);
        Assert.Equal(
            ["App.csproj", "Content/a|b.png", "Game1.cs", "Old.sln", "Sound.xap"],
            manifest.Changes.Select(c => c.Path));
        Assert.Equal(["XNA022", "XNA024"], manifest.Changes.Single(c => c.Path == "Game1.cs").Rules);
        Assert.Empty(manifest.Changes.Single(c => c.Path == "Old.sln").Rules);
        Assert.Equal("SDK project", manifest.Changes[0].Reason);
    }

    [Fact]
    public void Markdown_counts_by_kind()
    {
        var md = ManifestMarkdownWriter.Write(Rich(), []);

        Assert.Contains("| Created | 1 |", md);
        Assert.Contains("| Modified | 1 |", md);
        Assert.Contains("| Copied | 1 |", md);
        Assert.Contains("| Skipped | 1 |", md);
        Assert.Contains("| ManualWork | 1 |", md);
    }

    [Fact]
    public void Empty_manifest_has_no_manual_work_and_no_build_section()
    {
        var manifest = ManifestBuilder.Build("c", true, [], _noRules, null, "0.1.0");
        var md = ManifestMarkdownWriter.Write(manifest, []);

        Assert.Empty(manifest.Changes);
        Assert.Contains("## Manual work\n\nNone.", md);
        Assert.Contains("## Changed files\n\nNone.", md);
        Assert.DoesNotContain("## Build", md);
    }

    [Fact]
    public void Incomplete_status_is_stated()
    {
        var md = ManifestMarkdownWriter.Write(Rich(completed: false), []);

        Assert.Contains("Status: Incomplete: the migration did not finish", md);
        Assert.DoesNotContain("Status: Completed", md);
    }

    [Fact]
    public void Manual_notes_follow_manual_files()
    {
        var md = ManifestMarkdownWriter.Write(Rich(), ["Port the audio by hand"]);

        Assert.Contains("- `Sound.xap`: XACT not supported\n- Port the audio by hand", md);
    }

    [Fact]
    public void Table_cells_escape_pipes()
    {
        var manifest = ManifestBuilder.Build(
            "c",
            true,
            [new FileChange("a|b.cs", ChangeKind.Modified, "x", "y", null)],
            _noRules,
            null,
            "0.1.0");

        Assert.Contains("| a\\|b.cs | Modified | - |", ManifestMarkdownWriter.Write(manifest, []));
    }

    [Fact]
    public void Json_round_trips_and_starts_with_version()
    {
        var json = ManifestJsonWriter.Write(Rich());

        Assert.StartsWith("{\n  \"version\": 1,", json, StringComparison.Ordinal);
        Assert.EndsWith("}\n", json, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', json);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("xna-to-monogame", root.GetProperty("catalogName").GetString());
        Assert.True(root.GetProperty("completed").GetBoolean());
        Assert.Equal(5, root.GetProperty("changes").GetArrayLength());
        Assert.Equal("Created", root.GetProperty("changes")[0].GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("changes")[2].GetProperty("reason").ValueKind);
    }

    [Fact]
    public void Json_keeps_null_build()
    {
        var json = ManifestJsonWriter.Write(ManifestBuilder.Build("c", true, [], _noRules, null, "0.1.0"));

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("build").ValueKind);
    }

    [Fact]
    public Task Rich_json_snapshot() => Verifier.Verify(ManifestJsonWriter.Write(Rich()), "json");

    [Fact]
    public Task Empty_json_snapshot() =>
        Verifier.Verify(ManifestJsonWriter.Write(ManifestBuilder.Build("c", true, [], _noRules, null, "0.1.0")), "json");

    [Fact]
    public Task Rich_markdown_snapshot() =>
        Verifier.Verify(ManifestMarkdownWriter.Write(Rich(), ["Port the audio by hand"]), "md");

    [Fact]
    public Task Empty_markdown_snapshot() =>
        Verifier.Verify(ManifestMarkdownWriter.Write(ManifestBuilder.Build("c", true, [], _noRules, null, "0.1.0"), []), "md");
}
