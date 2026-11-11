using System.Text.Json;
using XnaReborn.Flash.Converter;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Flash.Tests;

/// <summary>Tasks 16.3 and R13: the manifest lists each marker with its file and line, and the behaviors that are only approximated.</summary>
public sealed class FlashManifestTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xnareborn-flashmanifest-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    [Fact]
    public async Task Marker_sites_match_the_marker_counts_and_point_at_real_lines()
    {
        var result = await FlashMigrator.MigrateAsync(FlashFixtures.Of("flx-gaps"), Path.Combine(_temp, "gaps"));
        var manifest = result.Manifest;

        Assert.NotNull(manifest.Markers);
        Assert.NotEmpty(manifest.Markers);
        Assert.Equal(manifest.Markers.OrderBy(p => p.Key, StringComparer.Ordinal), manifest.MarkerSites!.GroupBy(s => s.RuleId).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => KeyValuePair.Create(g.Key, g.Count())));
        Assert.Equal(manifest.MarkerSites, manifest.MarkerSites!.OrderBy(s => s.RuleId, StringComparer.Ordinal).ThenBy(s => s.File, StringComparer.Ordinal).ThenBy(s => s.Line));
        foreach (var site in manifest.MarkerSites!)
        {
            var original = File.ReadAllLines(Path.Combine(FlashFixtures.Of("flx-gaps"), site.File.Replace('/', Path.DirectorySeparatorChar)));
            Assert.InRange(site.Line, 1, original.Length);
            var converted = File.ReadAllText(Path.Combine(result.OutputFolder, site.ConvertedFile.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains("XNAREBORN: " + site.RuleId, converted, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Approximations_are_in_the_manifest_json_and_the_notes()
    {
        var result = await FlashMigrator.MigrateAsync(FlashFixtures.Of("flx-gaps"), Path.Combine(_temp, "gaps"));

        Assert.Equal(FlashMigrator.KnownDifferences, result.Manifest.Approximations);
        var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputFolder, "xnareborn-manifest.json"))).RootElement;
        Assert.Equal(FlashMigrator.KnownDifferences.Count, json.GetProperty("approximations").GetArrayLength());
        Assert.True(json.GetProperty("markerSites").GetArrayLength() > 0);
        var notes = File.ReadAllText(Path.Combine(result.OutputFolder, "MIGRATION-NOTES.md"));
        Assert.All(FlashMigrator.KnownDifferences, d => Assert.Contains(d, notes, StringComparison.Ordinal));
        var first = result.Manifest.MarkerSites![0];
        Assert.Contains($"  - {first.File}, line ", notes, StringComparison.Ordinal);
    }

    [Fact]
    public void A_manifest_without_the_new_fields_leaves_them_out_of_the_json()
    {
        var manifest = new Manifest(1, "x", "c", true, [], null);

        var json = ManifestJsonWriter.Write(manifest);

        Assert.DoesNotContain("markerSites", json, StringComparison.Ordinal);
        Assert.DoesNotContain("approximations", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_project_with_no_markers_has_empty_site_list_and_none_in_the_notes()
    {
        var result = await FlashMigrator.MigrateAsync(FlashFixtures.Of("flx-hello"), Path.Combine(_temp, "hello"));

        Assert.Empty(result.Manifest.MarkerSites!);
        Assert.Contains("None.", File.ReadAllText(Path.Combine(result.OutputFolder, "MIGRATION-NOTES.md")), StringComparison.Ordinal);
    }
}
