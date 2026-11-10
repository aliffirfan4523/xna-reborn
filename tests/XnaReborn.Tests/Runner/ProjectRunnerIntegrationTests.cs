using System.Text.Json.Nodes;
using XnaReborn.Runner;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Run;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Migrator;
using Xunit.Abstractions;

namespace XnaReborn.Tests.Runner;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that open a game window. They need <c>XNAREBORN_INTEGRATION=1</c> (NuGet)
/// and also <c>XNAREBORN_LAUNCH=1</c>, so a normal integration run never starts a game on the machine.
/// </summary>
public sealed class LaunchIntegrationFactAttribute : FactAttribute
{
    /// <summary>Name of the environment variable that allows tests to start a game.</summary>
    public const string EnvironmentVariable = "XNAREBORN_LAUNCH";

    /// <summary>Creates the attribute and sets <see cref="FactAttribute.Skip"/> unless both variables are <c>1</c>.</summary>
    public LaunchIntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(IntegrationFactAttribute.EnvironmentVariable) != "1")
        {
            Skip = $"Integration test: set {IntegrationFactAttribute.EnvironmentVariable}=1 to run it (it restores NuGet packages).";
        }
        else if (Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
        {
            Skip = $"Launch test: set {EnvironmentVariable}=1 as well to run it (it opens a game window).";
        }
    }
}

/// <summary>
/// Task 7 and 10.3: migrates fixtures and runs the whole pipeline with real <c>dotnet build</c> calls.
/// Needs the network for NuGet, so it runs only when <c>XNAREBORN_INTEGRATION=1</c>.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Runner")]
public sealed class ProjectRunnerIntegrationTests(ITestOutputHelper testOutput) : IDisposable
{
    private static readonly TimeSpan _buildTimeout = TimeSpan.FromMinutes(10);

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-runner-it-" + Guid.NewGuid().ToString("N"));

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

    private async Task<string> MigrateAsync(string fixture)
    {
        var output = Path.Combine(_temp, fixture);
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of(fixture), MigrationTestSupport.Options(output));
        Assert.True(result.Completed);
        return output;
    }

    private Task<RunnerResult> RunAsync(ProjectRunner runner, string folder, bool launch, TimeSpan? crashWindow = null) =>
        runner.BuildAndRunAsync(
            folder,
            new RunnerOptions(MonoGameTargetKind.DesktopGL, launch, _buildTimeout, crashWindow),
            _ => Task.FromResult(launch),
            new SyncProgress<RunnerProgress>(p => testOutput.WriteLine($"[{p.Stage}] {p.Detail}")),
            new SyncProgress<OutputLine>(l => testOutput.WriteLine(l.Text)),
            CancellationToken.None);

    private static JsonNode ReadManifest(string folder) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "xnareborn-manifest.json")))!;

    [IntegrationFact]
    public async Task Migrated_rewritable_fixture_builds_and_the_build_is_recorded()
    {
        var folder = await MigrateAsync("xna31-rewritable");

        var result = await RunAsync(new ProjectRunner(), folder, launch: false);

        Assert.True(result.Succeeded, result.Problem);
        Assert.True(result.Build!.Succeeded);
        var build = ReadManifest(folder)["build"]!;
        Assert.True(build["attempted"]!.GetValue<bool>());
        Assert.True(build["succeeded"]!.GetValue<bool>());
        Assert.Empty(build["errors"]!.AsArray());
        Assert.Contains("Succeeded.", File.ReadAllText(Path.Combine(folder, "xnareborn-manifest.md")));

        // The built game is findable, without starting it.
        var located = GameLocator.Find(folder);
        Assert.True(located.Target is not null, located.Problem);
        Assert.True(File.Exists(located.Target.ExecutablePath), located.Target.ExecutablePath);
    }

    [IntegrationFact]
    public async Task Migrated_legacy_fixture_fails_with_grouped_diagnostics_and_the_manifest_records_them()
    {
        var folder = await MigrateAsync("xna31-legacy");

        var result = await RunAsync(new ProjectRunner(), folder, launch: true);

        Assert.False(result.Succeeded);
        Assert.False(result.Build!.Succeeded);
        Assert.Null(result.Game);
        Assert.Null(result.RunningGame);
        var group = Assert.Single(
            result.Build.Groups,
            g => g.Items.Any(d => d.Code == "CS0246" && d.Message.Contains("ResolveTexture2D", StringComparison.Ordinal)));
        Assert.Equal("XNA027", group.Hint?.RuleId);

        var build = ReadManifest(folder)["build"]!;
        Assert.True(build["attempted"]!.GetValue<bool>());
        Assert.False(build["succeeded"]!.GetValue<bool>());
        var errors = build["errors"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        Assert.Contains(errors, e => e.StartsWith("CS0246: ", StringComparison.Ordinal) && e.Contains("ResolveTexture2D", StringComparison.Ordinal));
        Assert.Matches(@"Failed with \d+ error\(s\)\.", File.ReadAllText(Path.Combine(folder, "xnareborn-manifest.md")));
    }

    [LaunchIntegrationFact]
    public async Task Migrated_rewritable_fixture_launches_and_survives_the_startup_window()
    {
        var folder = await MigrateAsync("xna31-rewritable");

        var result = await RunAsync(new ProjectRunner(), folder, launch: true, crashWindow: TimeSpan.FromSeconds(3));

        try
        {
            Assert.True(result.Succeeded, result.Problem + " " + result.StartupFailure?.Stderr);
            Assert.NotNull(result.RunningGame);
            Assert.False(result.RunningGame.HasExited);
        }
        finally
        {
            if (result.RunningGame is not null)
            {
                await result.RunningGame.StopAsync();
            }
        }
    }
}
