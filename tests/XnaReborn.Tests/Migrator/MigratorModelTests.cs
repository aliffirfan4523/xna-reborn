using System.Text.Json;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;

namespace XnaReborn.Tests.Migrator;

/// <summary>The migrator's data records compare by value and write enums as text.</summary>
public class MigratorModelTests
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Change_kind_is_written_as_text()
    {
        var json = JsonSerializer.Serialize(new ManifestChange("Content/Content.mgcb", ChangeKind.Created, ["XNA005"], null), _options);

        Assert.Contains("\"kind\":\"Created\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Manifest_round_trips_through_json()
    {
        var manifest = new Manifest(
            1,
            "0.1.0",
            "xna-to-monogame",
            true,
            [
                new ManifestChange("Game1.cs", ChangeKind.Modified, ["XNA022"], null),
                new ManifestChange("Audio/Sounds.xap", ChangeKind.ManualWork, [], "XACT project"),
            ],
            new ManifestBuild(true, false, ["CS0246: type not found"]));

        var restored = JsonSerializer.Deserialize<Manifest>(JsonSerializer.Serialize(manifest, _options), _options)!;

        Assert.Equal(manifest.Version, restored.Version);
        Assert.Equal(manifest.Changes.Select(c => (c.Path, c.Kind, c.Reason)), restored.Changes.Select(c => (c.Path, c.Kind, c.Reason)));
        Assert.Equal(["XNA022"], restored.Changes[0].Rules);
        Assert.Equal(manifest.Build!.Errors, restored.Build!.Errors);
    }

    [Fact]
    public void Plan_items_and_file_changes_compare_by_value()
    {
        string[] files = ["Game1.cs"];

        Assert.Equal(new PlanItem("1", "XNA022", "Replace SpriteBlendMode", files), new PlanItem("1", "XNA022", "Replace SpriteBlendMode", files));
        Assert.Equal(new FileChange("a.cs", ChangeKind.Copied, null, null, null), new FileChange("a.cs", ChangeKind.Copied, null, null, null));
        Assert.NotEqual(new FileChange("a.cs", ChangeKind.Copied, null, null, null), new FileChange("a.cs", ChangeKind.Modified, null, null, null));
    }

    [Fact]
    public void Options_default_to_a_real_run_with_no_exclusions()
    {
        var options = new MigrationOptions(@"C:\out", MonoGameTarget.DesktopGL, false, false, new HashSet<string>());

        Assert.False(options.DryRun);
        Assert.False(options.EnableShims);
        Assert.Empty(options.ExcludedPlanItemIds);
        Assert.False(options.SubstituteFonts);
    }
}
