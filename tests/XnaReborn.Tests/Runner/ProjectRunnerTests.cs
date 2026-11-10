using System.Text.Json.Nodes;
using XnaReborn.Migrator.Output;
using XnaReborn.Runner;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Hints;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Run;
using XnaReborn.Runner.Safety;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Migrator;

namespace XnaReborn.Tests.Runner;

[Trait("Category", "Runner")]
[Trait("Category", "Process")]
public sealed class ProjectRunnerTests : IDisposable
{
    private static readonly TimeSpan _window = TimeSpan.FromMilliseconds(1500);
    private static readonly Func<LaunchConfirmation, Task<bool>> _yes = _ => Task.FromResult(true);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xr-runner-" + Guid.NewGuid().ToString("N"));
    private readonly List<RunHandle> _started = [];

    public ProjectRunnerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var handle in _started.Where(h => !h.HasExited))
        {
            handle.StopAsync().GetAwaiter().GetResult();
        }

        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private string MakeCopy(bool completed = true)
    {
        var folder = Path.Combine(_dir, "copy");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "xnareborn-manifest.json"),
            ManifestJsonWriter.Write(new Manifest(1, "0.1.0", "xna-to-monogame", completed, [], null)));
        File.WriteAllText(Path.Combine(folder, "xnareborn-manifest.md"), "# XNA Reborn migration manifest\n");
        File.WriteAllText(Path.Combine(folder, "Game.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        return folder;
    }

    private static FakeProcessRunner Dotnet(ProcessResult build, string version = "8.0.100") => new(request => request.Arguments[0] switch
    {
        "--version" => FakeProcessRunner.Ok(version),
        "--list-sdks" => FakeProcessRunner.Ok(version + " [C:\\dotnet\\sdk]"),
        "build" => build,
        _ => throw new InvalidOperationException("Unexpected dotnet command: " + string.Join(' ', request.Arguments)),
    });

    private static ProcessResult BuildOk() => FakeProcessRunner.Ok("  Game -> bin/Debug/net8.0/Game.dll");

    private static ProcessResult BuildFailed() => FakeProcessRunner.Exit(1, RunnerTestPaths.ReadSample("build-cs-errors"));

    private static Func<string, GameLocatorResult> TestGame(params string[] arguments) => folder =>
    {
        var exe = RunnerTestPaths.TestGame();
        return new GameLocatorResult(
            new GameTarget(Path.Combine(folder, "Game.csproj"), "TestGame", "net8.0", exe) { LaunchArguments = arguments },
            null);
    };

    private static RunnerOptions Options(bool launch, TimeSpan? crashWindow = null) =>
        new(MonoGameTargetKind.DesktopGL, launch, null, crashWindow);

    private async Task<RunnerResult> RunAsync(
        ProjectRunner runner,
        string folder,
        bool launch,
        Func<LaunchConfirmation, Task<bool>>? confirm = null,
        List<RunnerProgress>? progress = null,
        TimeSpan? crashWindow = null)
    {
        var result = await runner.BuildAndRunAsync(
            folder,
            Options(launch, crashWindow),
            confirm ?? _yes,
            progress is null ? null : new SyncProgress<RunnerProgress>(progress.Add),
            null,
            CancellationToken.None);
        if (result.RunningGame is not null)
        {
            _started.Add(result.RunningGame);
        }

        return result;
    }

    private static ProjectRunner Runner(FakeProcessRunner fake, Func<string, GameLocatorResult>? locate = null) =>
        new(fake, new GameLauncher(_window), null, locate);

    [Fact]
    public async Task OriginalXnaFolder_IsRefused_AndNothingRuns()
    {
        var original = FixturePaths.Of("xna40-basic");
        var fake = Dotnet(BuildOk());
        var confirmed = false;

        var result = await RunAsync(Runner(fake, TestGame()), original, launch: true, confirm: _ => { confirmed = true; return Task.FromResult(true); });

        Assert.False(result.Succeeded);
        Assert.False(result.Guard.Allowed);
        Assert.Equal(result.Guard.Message, result.Problem);
        Assert.Null(result.Environment);
        Assert.Null(result.Build);
        Assert.Null(result.Game);
        Assert.Empty(fake.Requests);
        Assert.False(confirmed);
        Assert.False(File.Exists(Path.Combine(original, "xnareborn-manifest.json")));
    }

    [Fact]
    public async Task IncompleteCopy_IsRefused()
    {
        var folder = MakeCopy(completed: false);
        var fake = Dotnet(BuildOk());

        var result = await RunAsync(Runner(fake), folder, launch: false);

        Assert.False(result.Guard.Allowed);
        Assert.Empty(fake.Requests);
        Assert.Null(result.Build);
    }

    [Fact]
    public async Task MissingSdk_StopsBeforeTheBuild()
    {
        var folder = MakeCopy();
        var fake = new FakeProcessRunner(_ => new ProcessResult(null, false, false, string.Empty, string.Empty, TimeSpan.Zero, "dotnet not found"));

        var result = await RunAsync(Runner(fake), folder, launch: true);

        Assert.False(result.Succeeded);
        Assert.True(result.Guard.Allowed);
        Assert.NotNull(result.Environment);
        Assert.False(result.Environment.SdkCompatible);
        Assert.Contains(".NET 8 SDK", result.Problem);
        Assert.Null(result.Build);
        Assert.DoesNotContain(fake.Requests, r => r.Arguments[0] == "build");
        Assert.Null(JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "xnareborn-manifest.json")))!["build"]);
    }

    [Fact]
    public async Task OldSdk_StopsBeforeTheBuild()
    {
        var folder = MakeCopy();
        var fake = Dotnet(BuildOk(), version: "6.0.400");

        var result = await RunAsync(Runner(fake), folder, launch: false);

        Assert.False(result.Succeeded);
        Assert.Contains("8.0", result.Problem);
        Assert.Null(result.Build);
    }

    [Fact]
    public async Task BuildSuccess_WithoutLaunch_Succeeds_AndRecordsTheBuild()
    {
        var folder = MakeCopy();
        var fake = Dotnet(BuildOk());
        var confirmed = false;

        var result = await RunAsync(Runner(fake, TestGame()), folder, launch: false, confirm: _ => { confirmed = true; return Task.FromResult(true); });

        Assert.True(result.Succeeded);
        Assert.Null(result.Problem);
        Assert.True(result.Build!.Succeeded);
        Assert.Null(result.Game);
        Assert.Null(result.RunningGame);
        Assert.False(confirmed);
        var build = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "xnareborn-manifest.json")))!["build"]!;
        Assert.True(build["attempted"]!.GetValue<bool>());
        Assert.True(build["succeeded"]!.GetValue<bool>());
        Assert.EndsWith("## Build\n\nSucceeded.\n", File.ReadAllText(Path.Combine(folder, "xnareborn-manifest.md")));
    }

    [Fact]
    public async Task BuildCommand_RunsOnTheGuardsProject()
    {
        var folder = MakeCopy();
        var fake = Dotnet(BuildOk());

        await RunAsync(Runner(fake), folder, launch: false);

        var build = Assert.Single(fake.Requests, r => r.Arguments[0] == "build");
        Assert.Equal(Path.Combine(folder, "Game.csproj"), build.Arguments[1]);
        Assert.Equal(folder, build.WorkingDirectory);
        Assert.Equal(TimeSpan.FromMinutes(5), build.Timeout);
    }

    [Fact]
    public async Task BuildTimeoutOption_IsUsed()
    {
        var folder = MakeCopy();
        var fake = Dotnet(BuildOk());

        await Runner(fake).BuildAndRunAsync(
            folder, new RunnerOptions(MonoGameTargetKind.DesktopGL, false, TimeSpan.FromSeconds(42)), _yes, null, null, CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(42), Assert.Single(fake.Requests, r => r.Arguments[0] == "build").Timeout);
    }

    [Fact]
    public async Task BuildFailure_StopsBeforeLaunch_AndRecordsTheErrors()
    {
        var folder = MakeCopy();
        var fake = Dotnet(BuildFailed());
        var confirmed = false;

        var result = await RunAsync(Runner(fake, TestGame()), folder, launch: true, confirm: _ => { confirmed = true; return Task.FromResult(true); });

        Assert.False(result.Succeeded);
        Assert.False(result.Build!.Succeeded);
        Assert.Equal("The build failed.", result.Problem);
        Assert.Null(result.Game);
        Assert.Null(result.RunningGame);
        Assert.False(confirmed);
        var group = Assert.Single(result.Build.Groups);
        Assert.Equal("XNA027", group.Hint?.RuleId);
        var build = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "xnareborn-manifest.json")))!["build"]!;
        Assert.False(build["succeeded"]!.GetValue<bool>());
        var errors = build["errors"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        Assert.Contains(errors, e => e.StartsWith("CS0246: ", StringComparison.Ordinal) && e.Contains("ResolveTexture2D", StringComparison.Ordinal));
        Assert.Contains("Failed with 1 error(s).", File.ReadAllText(Path.Combine(folder, "xnareborn-manifest.md")));
    }

    [Fact]
    public async Task BuildTimeout_IsReportedInTheProblem()
    {
        var folder = MakeCopy();
        var fake = Dotnet(new ProcessResult(null, true, false, string.Empty, string.Empty, TimeSpan.FromMinutes(5)));

        var result = await RunAsync(Runner(fake), folder, launch: false);

        Assert.False(result.Succeeded);
        Assert.True(result.Build!.TimedOut);
        Assert.Contains("did not finish", result.Problem);
    }

    [Fact]
    public async Task BuildOutput_IsStreamedToTheCaller()
    {
        var folder = MakeCopy();
        var lines = new List<string>();

        await Runner(Dotnet(BuildFailed())).BuildAndRunAsync(
            folder, Options(false), _yes, null, new SyncProgress<OutputLine>(l => lines.Add(l.Text)), CancellationToken.None);

        Assert.Contains(lines, l => l.Contains("CS0246", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LaunchDeclined_KeepsTheBuild_AndStartsNothing()
    {
        var folder = MakeCopy();
        var fake = Dotnet(BuildOk());

        var result = await RunAsync(Runner(fake, TestGame()), folder, launch: true, confirm: _ => Task.FromResult(false));

        Assert.False(result.Succeeded);
        Assert.Equal("Launch declined.", result.Problem);
        Assert.True(result.Build!.Succeeded);
        Assert.NotNull(result.Game);
        Assert.Null(result.RunningGame);
        Assert.Null(result.StartupFailure);
    }

    [Fact]
    public async Task GameNotFound_IsAProblem()
    {
        var folder = MakeCopy();

        var result = await RunAsync(
            Runner(Dotnet(BuildOk()), _ => new GameLocatorResult(null, "No game project was found.")),
            folder,
            launch: true);

        Assert.False(result.Succeeded);
        Assert.Equal("No game project was found.", result.Problem);
        Assert.True(result.Build!.Succeeded);
        Assert.Null(result.Game);
    }

    [Fact]
    public async Task RealGameLocator_ReportsAnUnbuiltGame()
    {
        var folder = MakeCopy();
        File.WriteAllText(
            Path.Combine(folder, "Game.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");

        var result = await RunAsync(new ProjectRunner(Dotnet(BuildOk()), new GameLauncher(_window)), folder, launch: true);

        Assert.False(result.Succeeded);
        Assert.Contains("Build the project first", result.Problem);
    }

    [Fact]
    public async Task EarlyCrash_ReturnsStartupFailureWithRuntimeHints()
    {
        var folder = MakeCopy();
        var locate = TestGame("--stderr", "Microsoft.Xna.Framework.Content.ContentLoadException: Could not find file 'Content/hero.xnb'", "--exit", "1");

        var result = await RunAsync(Runner(Dotnet(BuildOk()), locate), folder, launch: true);

        Assert.False(result.Succeeded);
        Assert.Null(result.RunningGame);
        var session = result.StartupFailure!;
        Assert.Equal(1, session.ExitCode);
        Assert.True(session.StartupFailure);
        Assert.Contains("hero.xnb", session.Stderr);
        Assert.Contains(session.RuntimeHints, h => h.Id == "R001");
        Assert.Contains("exit code 1", result.Problem);
        Assert.True(result.Build!.Succeeded);
    }

    [Fact]
    public async Task EarlyCrash_WithTheOptionWindow_IsAlsoAStartupFailure()
    {
        var folder = MakeCopy();
        var locate = TestGame("--sleep", "300", "--stderr", "Failed to create graphics device", "--exit", "5");

        var result = await RunAsync(Runner(Dotnet(BuildOk()), locate), folder, launch: true, crashWindow: TimeSpan.FromSeconds(10));

        Assert.False(result.Succeeded);
        var session = result.StartupFailure!;
        Assert.Equal(5, session.ExitCode);
        Assert.True(session.StartupFailure);
        Assert.Contains(session.RuntimeHints, h => h.Id == "R004");
    }

    [Fact]
    public async Task CleanExitInsideTheWindow_IsNotSuccess()
    {
        var folder = MakeCopy();

        var result = await RunAsync(Runner(Dotnet(BuildOk()), TestGame("--stdout", "bye", "--exit", "0")), folder, launch: true);

        Assert.False(result.Succeeded);
        Assert.Null(result.RunningGame);
        Assert.Equal(0, result.StartupFailure!.ExitCode);
        Assert.False(result.StartupFailure.StartupFailure);
        Assert.Contains("closed right after it started", result.Problem);
    }

    [Fact]
    public async Task LongRunningGame_IsReturnedRunning_AndTheCallerStopsIt()
    {
        var folder = MakeCopy();

        var result = await RunAsync(Runner(Dotnet(BuildOk()), TestGame("--stdout", "up")), folder, launch: true);

        Assert.True(result.Succeeded);
        Assert.Null(result.Problem);
        Assert.Null(result.StartupFailure);
        var handle = Assert.IsType<RunHandle>(result.RunningGame);
        Assert.False(handle.HasExited);
        Assert.Equal(RunnerTestPaths.TestGame(), result.Game!.ExecutablePath);

        await handle.StopAsync();
        var session = await handle.Completion;
        Assert.True(session.StoppedByUser);
        Assert.Contains("up", session.Stdout);
    }

    [Fact]
    public async Task GameStartsInItsOwnOutputFolder()
    {
        var folder = MakeCopy();

        var result = await RunAsync(Runner(Dotnet(BuildOk()), TestGame("--cwd", "--sleep", "100")), folder, launch: true);

        Assert.True(result.Succeeded);
        await result.RunningGame!.StopAsync();
        var session = await result.RunningGame.Completion;
        Assert.Equal(Path.GetDirectoryName(RunnerTestPaths.TestGame()), session.Stdout.Trim(), ignoreCase: OperatingSystem.IsWindows());
    }

    [Fact]
    public async Task Progress_StagesComeInOrder()
    {
        var folder = MakeCopy();
        var stages = new List<RunnerProgress>();

        var result = await RunAsync(Runner(Dotnet(BuildOk()), TestGame()), folder, launch: true, progress: stages);

        Assert.True(result.Succeeded);
        Assert.Equal(
            ["Checking folder", "Checking environment", "Building", "Launching", "Running"],
            stages.Select(s => s.Stage));
        Assert.Equal(Path.Combine(folder, "Game.csproj"), stages[2].Detail);
    }

    [Fact]
    public async Task Progress_StopsAtTheStepThatStoppedTheRun()
    {
        var folder = MakeCopy();
        var stages = new List<RunnerProgress>();

        await RunAsync(Runner(Dotnet(BuildFailed()), TestGame()), folder, launch: true, progress: stages);

        Assert.Equal(["Checking folder", "Checking environment", "Building"], stages.Select(s => s.Stage));
    }

    [Fact]
    public async Task Confirmation_IsAskedOnceForTwoRunsOfTheSameGame()
    {
        var folder = MakeCopy();
        var runner = Runner(Dotnet(BuildOk()), TestGame());
        var asked = 0;
        Task<bool> Confirm(LaunchConfirmation c)
        {
            asked++;
            Assert.Contains("normal user rights", c.Message);
            return Task.FromResult(true);
        }

        var first = await RunAsync(runner, folder, launch: true, confirm: Confirm);
        var second = await RunAsync(runner, folder, launch: true, confirm: Confirm);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(1, asked);
        await first.RunningGame!.StopAsync();
        await second.RunningGame!.StopAsync();
    }

    [Fact]
    public async Task UnexpectedException_BecomesAProblem()
    {
        var folder = MakeCopy();
        var fake = new FakeProcessRunner(r => r.Arguments[0] == "build"
            ? throw new InvalidOperationException("disk exploded")
            : r.Arguments[0] == "--version" ? FakeProcessRunner.Ok("8.0.100") : FakeProcessRunner.Ok("8.0.100 [C:\\sdk]"));

        var result = await RunAsync(Runner(fake), folder, launch: false);

        Assert.False(result.Succeeded);
        Assert.Contains("disk exploded", result.Problem);
        Assert.True(result.Guard.Allowed);
        Assert.NotNull(result.Environment);
    }

    [Fact]
    public async Task ManifestThatCannotBeUpdated_IsAProblem()
    {
        var folder = MakeCopy();
        var fake = new FakeProcessRunner(r =>
        {
            if (r.Arguments[0] == "build")
            {
                // Breaks the manifest while the build "runs", so the guard (already passed) cannot catch it.
                File.WriteAllText(Path.Combine(folder, "xnareborn-manifest.json"), "{ broken");
                return BuildOk();
            }

            return r.Arguments[0] == "--version" ? FakeProcessRunner.Ok("8.0.100") : FakeProcessRunner.Ok("8.0.100 [C:\\sdk]");
        });

        var result = await RunAsync(Runner(fake, TestGame()), folder, launch: true);

        Assert.False(result.Succeeded);
        Assert.Contains("migration manifest", result.Problem);
        Assert.Null(result.Game);
    }

    [Fact]
    public async Task Cancellation_Throws()
    {
        var folder = MakeCopy();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var fake = new FakeProcessRunner(request => request.Arguments[0] == "build"
            ? new ProcessResult(null, false, true, string.Empty, string.Empty, TimeSpan.Zero)
            : FakeProcessRunner.Ok("8.0.100 [C:\\sdk]"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Runner(fake).BuildAndRunAsync(folder, Options(false), _yes, null, null, cts.Token));
    }

    [Fact]
    public async Task CancelledBuild_ThrowsAndDoesNotTouchTheManifest()
    {
        var folder = MakeCopy();
        using var cts = new CancellationTokenSource();
        var fake = new FakeProcessRunner(request =>
        {
            if (request.Arguments[0] != "build")
            {
                return FakeProcessRunner.Ok("8.0.100 [C:\\sdk]");
            }

            cts.Cancel();
            return new ProcessResult(null, false, true, string.Empty, string.Empty, TimeSpan.Zero);
        });
        var before = File.ReadAllText(Path.Combine(folder, "xnareborn-manifest.json"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Runner(fake).BuildAndRunAsync(folder, Options(false), _yes, null, null, cts.Token));

        Assert.Equal(before, File.ReadAllText(Path.Combine(folder, "xnareborn-manifest.json")));
    }

    [Fact]
    public async Task CancelDuringTheStartupWait_StopsTheGame_AndThrows()
    {
        var folder = MakeCopy();
        using var cts = new CancellationTokenSource();
        var runner = Runner(Dotnet(BuildOk()), TestGame("--stdout", "up"));

        var run = runner.BuildAndRunAsync(folder, Options(true, TimeSpan.FromSeconds(30)), _yes, new SyncProgress<RunnerProgress>(p =>
        {
            if (p.Stage == RunnerProgress.Launching)
            {
                _ = Task.Delay(500).ContinueWith(_ => cts.Cancel(), TaskScheduler.Default);
            }
        }), null, cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public void Succeeded_NeedsEveryPart()
    {
        var ok = new GuardVerdict(true, "ok", "p");
        var env = new EnvironmentInfo("8.0.100", ["8.0.100"], true, "Windows", true, null);
        var build = new XnaReborn.Runner.Build.BuildResult(true, TimeSpan.Zero, [], string.Empty, 0, false, false);

        Assert.True(new RunnerResult(ok, env, build, null, null, null, null).Succeeded);
        Assert.False(new RunnerResult(ok with { Allowed = false }, env, build, null, null, null, null).Succeeded);
        Assert.False(new RunnerResult(ok, env with { SdkCompatible = false }, build, null, null, null, null).Succeeded);
        Assert.False(new RunnerResult(ok, env with { TargetSupported = false }, build, null, null, null, null).Succeeded);
        Assert.False(new RunnerResult(ok, null, build, null, null, null, null).Succeeded);
        Assert.False(new RunnerResult(ok, env, build with { Succeeded = false }, null, null, null, null).Succeeded);
        Assert.False(new RunnerResult(ok, env, null, null, null, null, null).Succeeded);
        Assert.False(new RunnerResult(ok, env, build, null, null, null, "Launch declined.").Succeeded);
        Assert.False(new RunnerResult(ok, env, build, null, null, new RunSession(1, true, string.Empty, string.Empty, [], false), null).Succeeded);
    }
}
