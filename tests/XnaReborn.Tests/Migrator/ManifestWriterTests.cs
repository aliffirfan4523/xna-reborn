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

    private static ManifestContent SomeContent() => new(
        new Dictionary<string, int> { ["Converted"] = 5, ["ManualWork"] = 2, ["Missing"] = 1, ["Conflict"] = 0, ["Prebuilt"] = 1 },
        new Dictionary<string, int>
        {
            ["Texture"] = 4,
            ["Font"] = 1,
            ["SoundEffect"] = 1,
            ["Song"] = 0,
            ["Model"] = 0,
            ["Effect"] = 1,
            ["Xact"] = 1,
            ["Xml"] = 0,
            ["Prebuilt"] = 1,
            ["Other"] = 0,
        });

    private static Manifest WithContent() => ManifestBuilder.Build(
        "xna-to-monogame",
        true,
        [new FileChange("Content/Content.mgcb", ChangeKind.Created, null, "x", null)],
        new Dictionary<string, IReadOnlyList<string>> { ["Content/Content.mgcb"] = ["XNA005"] },
        null,
        "0.1.0",
        SomeContent());

    [Fact]
    public void Build_passes_the_content_totals_through_and_defaults_to_none()
    {
        Assert.Null(Rich().Content);
        Assert.Null(ManifestBuilder.Build("c", true, [], _noRules, null, "0.1.0").Content);
        Assert.Equal(5, WithContent().Content!.CountsByStatus["Converted"]);
    }

    [Fact]
    public void Markdown_has_a_content_section_only_when_content_totals_exist()
    {
        var withContent = ManifestMarkdownWriter.Write(WithContent(), []);
        var without = ManifestMarkdownWriter.Write(Rich(), []);

        Assert.Contains("## Content\n\n| Status | Assets |\n|---|---|\n| Converted | 5 |\n| ManualWork | 2 |", withContent, StringComparison.Ordinal);
        Assert.Contains("\n\n| Type | Assets |\n|---|---|\n| Texture | 4 |\n| Font | 1 |", withContent, StringComparison.Ordinal);
        Assert.DoesNotContain("## Content", without, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_writes_content_totals_in_key_order_or_null()
    {
        using var withContent = JsonDocument.Parse(ManifestJsonWriter.Write(WithContent()));
        var content = withContent.RootElement.GetProperty("content");

        Assert.Equal(["Converted", "ManualWork", "Missing", "Conflict", "Prebuilt"], content.GetProperty("countsByStatus").EnumerateObject().Select(p => p.Name));
        Assert.Equal(4, content.GetProperty("countsByType").GetProperty("Texture").GetInt32());
        using var without = JsonDocument.Parse(ManifestJsonWriter.Write(Rich()));
        Assert.Equal(JsonValueKind.Null, without.RootElement.GetProperty("content").ValueKind);
    }

    [Fact]
    public Task Content_json_snapshot() => Verifier.Verify(ManifestJsonWriter.Write(WithContent()), "json");

    [Fact]
    public Task Content_markdown_snapshot() => Verifier.Verify(ManifestMarkdownWriter.Write(WithContent(), []), "md");
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
