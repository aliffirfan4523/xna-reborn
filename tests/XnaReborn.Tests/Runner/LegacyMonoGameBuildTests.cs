using System.Text.Json.Nodes;
using XnaReborn.Migrator;
using XnaReborn.Runner;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Run;
using XnaReborn.Runner.Safety;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Migrator;
using Xunit.Abstractions;

namespace XnaReborn.Tests.Runner;

/// <summary>
/// Monogame-upgrade task 15: migrates old MonoGame fixtures and builds the copy with a real <c>dotnet build</c>.
/// Needs the network for NuGet, so the builds run only when <c>XNAREBORN_INTEGRATION=1</c>. No game is started.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Runner")]
public sealed class LegacyMonoGameBuildTests(ITestOutputHelper testOutput) : IDisposable
{
    private static readonly TimeSpan _buildTimeout = TimeSpan.FromMinutes(10);

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-mgup-it-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            try
            {
                Directory.Delete(_temp, recursive: true);
            }
            catch (IOException)
            {
                // A build server may still hold a file. The folder is in the temp directory and can be removed later.
            }
        }
    }

    private async Task<(string Source, string Output, Dictionary<string, string> Before)> MigrateAsync(string fixture, MonoGameTarget? target = null)
    {
        var source = FixturePaths.Of(fixture);
        var before = MigrationTestSupport.Tree(source).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var output = Path.Combine(_temp, fixture);
        var options = new MigrationOptions(output, MonoGameTarget.DesktopGL, false, false, new HashSet<string>(StringComparer.Ordinal), LegacyTarget: target);
        var report = await LegacyMonoGameMigrationTests.AnalyzeAsync(source);
        var result = await new ProjectMigrator().MigrateAsync(source, report, options, false, null, CancellationToken.None);
        Assert.True(result.Completed);
        return (source, output, before);
    }

    private Task<RunnerResult> BuildAsync(string folder, MonoGameTargetKind kind) =>
        new ProjectRunner().BuildAndRunAsync(
            folder,
            new RunnerOptions(kind, Launch: false, _buildTimeout),
            _ => Task.FromResult(false),
            new SyncProgress<RunnerProgress>(p => testOutput.WriteLine($"[{p.Stage}] {p.Detail}")),
            new SyncProgress<OutputLine>(l => testOutput.WriteLine(l.Text)),
            CancellationToken.None);

    [Theory]
    [InlineData("mg37-windowsdx-pkgconfig")]
    [InlineData("mg37-desktopgl-installdir")]
    [InlineData("mg34-windowsgl")]
    [InlineData("mg38-old-pin")]
    [InlineData("mg37-multiplatform-sln")]
    public void The_runner_refuses_the_unmigrated_fixture(string fixture)
    {
        var verdict = MigratedCopyGuard.Check(FixturePaths.Of(fixture));

        Assert.False(verdict.Allowed);
    }

    [Fact]
    public async Task A_migrated_copy_has_the_manifest_marker_and_the_runner_accepts_it()
    {
        var (_, output, _) = await MigrateAsync("mg34-windowsgl");

        var verdict = MigratedCopyGuard.Check(output);

        Assert.True(verdict.Allowed, verdict.Message);
        Assert.True(File.Exists(Path.Combine(output, "xnareborn-manifest.json")));
    }

    [IntegrationFact]
    public async Task Windows_project_builds_as_WindowsDX_on_net8_windows_and_the_source_hash_is_unchanged()
    {
        var (source, output, before) = await MigrateAsync("mg37-windowsdx-pkgconfig");

        var result = await BuildAsync(output, MonoGameTargetKind.WindowsDX);

        // The recorded answer to the open question of the design: does net8.0-windows with WindowsDX 3.8.6.1 restore and build?
        testOutput.WriteLine("WindowsDX net8.0-windows build succeeded: " + result.Build?.Succeeded);
        Assert.NotNull(result.Build);
        Assert.True(result.Build.Succeeded, result.Problem + " " + string.Join("; ", result.Build.Groups.SelectMany(g => g.Items).Select(d => d.Code + ": " + d.Message)));
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "xnareborn-manifest.json")))!;
        Assert.True(manifest["build"]!["succeeded"]!.GetValue<bool>());
        Assert.Equal(before, MigrationTestSupport.Tree(source).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
    }

    [IntegrationFact]
    public async Task Desktop_project_with_a_missing_custom_pipeline_fails_in_the_content_build_not_in_the_compiler()
    {
        var (source, output, before) = await MigrateAsync("mg37-desktopgl-installdir");

        var result = await BuildAsync(output, MonoGameTargetKind.DesktopGL);

        Assert.NotNull(result.Build);
        Assert.False(result.Build.Succeeded);
        var codes = result.Build.Groups.SelectMany(g => g.Items).Select(d => d.Code).ToList();
        testOutput.WriteLine("Diagnostics: " + string.Join(", ", codes));
        Assert.DoesNotContain(codes, c => c.StartsWith("CS", StringComparison.Ordinal));
        Assert.Equal(before, MigrationTestSupport.Tree(source).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
    }

    [IntegrationFact]
    public async Task Windows_project_moved_to_DesktopGL_builds()
    {
        var (_, output, _) = await MigrateAsync("mg37-windowsdx-pkgconfig", MonoGameTarget.DesktopGL);

        var result = await BuildAsync(output, MonoGameTargetKind.DesktopGL);

        Assert.NotNull(result.Build);
        Assert.True(result.Build.Succeeded, string.Join("; ", result.Build.Groups.SelectMany(g => g.Items).Select(d => d.Code + ": " + d.Message)));
    }

    [IntegrationFact]
    public async Task Old_pin_project_builds_after_the_bump()
    {
        // The fixture has no Main (it is never built as shipped), so a copy gets one.
        var copy = Path.Combine(_temp, "old-pin-source");
        MigrationTestSupport.CopyFolder(FixturePaths.Of("mg38-old-pin"), copy);
        await File.WriteAllTextAsync(Path.Combine(copy, "Program.cs"), "namespace Mg38OldPin { public static class Program { public static void Main() { using var game = new Game1(); game.Run(); } } }\n");
        var before = MigrationTestSupport.Tree(copy).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var output = Path.Combine(_temp, "old-pin-out");
        var report = await LegacyMonoGameMigrationTests.AnalyzeAsync(copy);
        var migrated = await new ProjectMigrator().MigrateAsync(copy, report, new MigrationOptions(output, MonoGameTarget.DesktopGL, false, false, new HashSet<string>(StringComparer.Ordinal)), false, null, CancellationToken.None);
        Assert.True(migrated.Completed);

        var result = await BuildAsync(output, MonoGameTargetKind.DesktopGL);

        Assert.NotNull(result.Build);
        Assert.True(result.Build.Succeeded, string.Join("; ", result.Build.Groups.SelectMany(g => g.Items).Select(d => d.Code + ": " + d.Message)));
        Assert.Equal(before, MigrationTestSupport.Tree(copy).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
    }
}
