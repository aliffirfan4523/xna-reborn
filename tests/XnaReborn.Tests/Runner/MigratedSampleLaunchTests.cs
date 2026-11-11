using System.Text;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Flash.Converter;
using XnaReborn.Migrator;
using XnaReborn.Runner;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Run;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Migrator;
using Xunit.Abstractions;

namespace XnaReborn.Tests.Runner;

/// <summary>
/// Migrates the SharpDX and Flash sample fixtures with the real tools, builds them, starts each game, lets it run for
/// about 10 seconds and stops it. Opens windows, so it needs <c>XNAREBORN_INTEGRATION=1</c> and <c>XNAREBORN_LAUNCH=1</c>.
/// Output goes to <c>XNAREBORN_LAUNCH_OUT</c> (default: the temp folder), never into the repository.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Runner")]
public sealed class MigratedSampleLaunchTests(ITestOutputHelper testOutput)
{
    private static readonly TimeSpan _buildTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan _upTime = TimeSpan.FromSeconds(10);

    // A 1x1 white PNG.
    private const string _onePixelPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY/j///9/AAn7A/0FQ0XKAAAAAElFTkSuQmCC";

    private static string OutRoot =>
        Environment.GetEnvironmentVariable("XNAREBORN_LAUNCH_OUT") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Path.GetTempPath(), "xr-launch-check");

    private static string FlashFixture(string name) => Path.Combine(FixturePaths.Root, "flash", name);

    [LaunchIntegrationFact]
    public async Task Sdx_basic_migrates_builds_and_stays_up()
    {
        // The fixture's Balls.dds is a text placeholder the content builder cannot decode, so the copy to migrate gets a real PNG.
        // The fixture itself is not touched.
        var source = Fresh("sdx-basic-source");
        MigrationTestSupport.CopyFolder(FixturePaths.Of("sdx-basic"), source);
        var project = Path.Combine(source, "SdxBasic.csproj");
        File.WriteAllText(project, File.ReadAllText(project).Replace("Content\\Balls.dds", "Content\\Balls.png", StringComparison.Ordinal).Replace("<Link>Balls.dds</Link>", "<Link>Balls.png</Link>", StringComparison.Ordinal));
        File.Delete(Path.Combine(source, "Content", "Balls.dds"));
        File.WriteAllBytes(Path.Combine(source, "Content", "Balls.png"), Convert.FromBase64String(_onePixelPngBase64));

        var rules = Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "..", "rules"));
        var catalogs = await RuleCatalogLoader.LoadAllAsync([Path.Combine(rules, "xna-to-monogame.json"), Path.Combine(rules, "sharpdx-toolkit-to-monogame.json")], new ReadOnlyFileSystem(), CancellationToken.None);
        var report = await new ProjectAnalyzer().AnalyzeAsync(source, catalogs, null, CancellationToken.None);
        var folder = Fresh("sdx-basic");
        var result = await new ProjectMigrator().MigrateAsync(source, report, MigrationTestSupport.Options(folder), false, null, CancellationToken.None);
        Assert.True(result.Completed);
        await RunAsync("sdx-basic", folder);
    }
    [LaunchIntegrationTheory]
    [InlineData("flx-hello")]
    [InlineData("flx-platformer")]
    public async Task Flash_sample_migrates_builds_and_stays_up(string fixture)
    {
        var folder = Fresh(fixture);
        await FlashMigrator.MigrateAsync(FlashFixture(fixture), folder);
        await RunAsync(fixture, folder);
    }

    private static string Fresh(string name)
    {
        var folder = Path.Combine(OutRoot, name);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        return folder;
    }

    private async Task RunAsync(string name, string folder)
    {
        var log = new StringBuilder();
        void Line(string text)
        {
            lock (log)
            {
                log.AppendLine(text);
            }

            testOutput.WriteLine(text);
        }

        var runner = new ProjectRunner(launcher: new GameLauncher(_upTime));
        var result = await runner.BuildAndRunAsync(
            folder,
            new RunnerOptions(MonoGameTargetKind.DesktopGL, true, _buildTimeout, _upTime),
            _ => Task.FromResult(true),
            new SyncProgress<RunnerProgress>(p => Line($"[{p.Stage}] {p.Detail}")),
            new SyncProgress<OutputLine>(l => Line(l.Text)),
            CancellationToken.None);

        try
        {
            Line("Problem: " + result.Problem);
            if (result.StartupFailure is { } failure)
            {
                Line($"Startup failure, exit {failure.ExitCode}\nSTDOUT:\n{failure.Stdout}\nSTDERR:\n{failure.Stderr}");
                foreach (var hint in failure.RuntimeHints)
                {
                    Line("Hint: " + hint.Id + " " + hint.Title);
                }
            }

            Assert.True(result.Succeeded, result.Problem + " " + result.StartupFailure?.Stderr);
            Assert.NotNull(result.RunningGame);
            Assert.False(result.RunningGame.HasExited);
        }
        finally
        {
            if (result.RunningGame is not null)
            {
                await result.RunningGame.StopAsync();
                var session = await result.RunningGame.Completion;
                Line($"Stopped. STDOUT:\n{session.Stdout}\nSTDERR:\n{session.Stderr}");
            }

            Directory.CreateDirectory(OutRoot);
            File.WriteAllText(Path.Combine(OutRoot, name + ".log"), log.ToString());
        }
    }
}

/// <summary>A theory that opens a game window: skipped unless both integration variables are 1.</summary>
public sealed class LaunchIntegrationTheoryAttribute : TheoryAttribute
{
    /// <summary>Sets <see cref="FactAttribute.Skip"/> unless <c>XNAREBORN_INTEGRATION</c> and <c>XNAREBORN_LAUNCH</c> are both 1.</summary>
    public LaunchIntegrationTheoryAttribute()
    {
        if (new LaunchIntegrationFactAttribute().Skip is { } skip)
        {
            Skip = skip;
        }
    }
}
