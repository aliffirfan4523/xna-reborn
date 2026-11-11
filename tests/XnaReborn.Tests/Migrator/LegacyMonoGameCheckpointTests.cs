using XnaReborn.Migrator;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator;

/// <summary>
/// Monogame-upgrade task 18: the checkpoint tests. Every old MonoGame fixture is migrated, the source folder must be byte for byte
/// unchanged, and the manifest of each run is kept as a snapshot so a change in it shows up in review.
/// </summary>
[Trait("Category", "Migrator")]
public sealed class LegacyMonoGameCheckpointTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-mgcp-" + Guid.NewGuid().ToString("N"));

    public LegacyMonoGameCheckpointTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    /// <summary>The fixtures the migrator turns into a copy. D, F and H have nothing to migrate and are covered below.</summary>
    public static IEnumerable<object[]> MigratedFixtures() =>
        new[] { "mg34-windowsgl", "mg37-desktopgl-installdir", "mg37-source-apis", "mg37-windowsdx-pkgconfig", "mg38-old-pin" }
            .Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(MigratedFixtures))]
    public async Task Migration_leaves_the_source_unchanged_and_the_manifest_matches_snapshot(string fixture)
    {
        var source = FixturePaths.Of(fixture);
        var before = MigrationTestSupport.Tree(source);
        var output = Path.Combine(_temp, fixture);

        var report = await LegacyMonoGameMigrationTests.AnalyzeAsync(source);
        var result = await new ProjectMigrator().MigrateAsync(source, report, new MigrationOptions(output, MonoGameTarget.DesktopGL, false, false, new HashSet<string>(StringComparer.Ordinal)), false, null, CancellationToken.None);

        Assert.True(result.Completed);
        Assert.Equal(before, MigrationTestSupport.Tree(source));
        var markdown = await File.ReadAllTextAsync(Path.Combine(output, ProjectMigrator.ManifestMarkdownName));
        Assert.DoesNotContain(_temp, markdown, StringComparison.OrdinalIgnoreCase);
        await Verifier.Verify(markdown, "md").UseParameters(fixture);
    }

    [Fact]
    public async Task Multiplatform_solution_leaves_the_source_unchanged_and_the_manifest_matches_snapshot()
    {
        var source = FixturePaths.Of("mg37-multiplatform-sln");
        var before = MigrationTestSupport.Tree(source);
        var output = Path.Combine(_temp, "multi");

        var report = await LegacyMonoGameMigrationTests.AnalyzeAsync(Path.Combine(source, "Mg37Multi.sln"));
        var result = await new ProjectMigrator().MigrateAsync(Path.Combine(source, "Mg37Multi.sln"), report, new MigrationOptions(output, MonoGameTarget.DesktopGL, false, false, new HashSet<string>(StringComparer.Ordinal)), false, null, CancellationToken.None);

        Assert.True(result.Completed);
        Assert.Equal(before, MigrationTestSupport.Tree(source));
        await Verifier.Verify(await File.ReadAllTextAsync(Path.Combine(output, ProjectMigrator.ManifestMarkdownName)), "md");
    }

    [Theory]
    [InlineData("mg35-win8-wp8-ouya")]
    [InlineData("mg3x-protobuild")]
    [InlineData("mg38-current")]
    public async Task A_fixture_with_nothing_to_migrate_stops_and_writes_nothing(string fixture)
    {
        var source = FixturePaths.Of(fixture);
        var before = MigrationTestSupport.Tree(source);
        var output = Path.Combine(_temp, fixture);

        var report = await LegacyMonoGameMigrationTests.AnalyzeAsync(source);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ProjectMigrator().MigrateAsync(source, report, new MigrationOptions(output, MonoGameTarget.DesktopGL, false, false, new HashSet<string>(StringComparer.Ordinal)), false, null, CancellationToken.None));

        Assert.Equal(before, MigrationTestSupport.Tree(source));
        Assert.False(Directory.Exists(output));
    }
}
