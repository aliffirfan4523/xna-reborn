using System.Diagnostics;
using XnaReborn.Runner.Run;

namespace XnaReborn.Tests.Runner;

[Trait("Category", "Runner")]
[Trait("Category", "Process")]
public sealed class GameLauncherTests
{
    private static readonly TimeSpan _window = TimeSpan.FromMilliseconds(1500);

    [Fact]
    public async Task Declined_StartsNothing()
    {
        var launcher = new GameLauncher(_window);
        var game = FindGame();

        var handle = await launcher.LaunchAsync(game, Path.GetDirectoryName(game)!, _ => Task.FromResult(false), CancellationToken.None);

        Assert.Null(handle);
    }

    [Fact]
    public async Task ConfirmationMessage_ExplainsRights()
    {
        var launcher = new GameLauncher(_window);
        var game = FindGame();
        LaunchConfirmation? seen = null;

        var handle = await launcher.LaunchAsync(game, Path.GetDirectoryName(game)!, c => { seen = c; return Task.FromResult(false); }, CancellationToken.None);

        Assert.Null(handle);
        Assert.NotNull(seen);
        Assert.Equal(Path.GetFullPath(game), seen.GamePath);
        Assert.Contains("normal user rights", seen.Message);
    }

    [Fact]
    public async Task AsksOncePerPath()
    {
        var launcher = new GameLauncher(_window);
        var game = FindGame();
        var dir = Path.GetDirectoryName(game)!;
        var asked = 0;
        Task<bool> Confirm(LaunchConfirmation c)
        {
            asked++;
            return Task.FromResult(true);
        }

        var first = await launcher.LaunchAsync(game, dir, Confirm, CancellationToken.None);
        var second = await launcher.LaunchAsync(game, dir, Confirm, CancellationToken.None);
        Assert.NotNull(first);
        Assert.NotNull(second);
        await first.StopAsync();
        await second.StopAsync();
        Assert.Equal(1, asked);

        var dll = Path.ChangeExtension(game, ".dll");
        var third = await launcher.LaunchAsync(dll, dir, Confirm, CancellationToken.None);
        Assert.NotNull(third);
        await third.StopAsync();
        Assert.Equal(2, asked);
    }

    [Fact]
    public async Task EarlyCrash_IsStartupFailureWithStderr()
    {
        var handle = await Launch("--stderr", "boom", "--exit", "3");

        var session = await handle.Completion;

        Assert.Equal(3, session.ExitCode);
        Assert.True(session.StartupFailure);
        Assert.False(session.StoppedByUser);
        Assert.Contains("boom", session.Stderr);
        Assert.Empty(session.RuntimeHints);
        Assert.False(await handle.WaitForStartupAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CleanEarlyExit_IsNotStartupFailure()
    {
        var handle = await Launch("--stdout", "hi", "--exit", "0");

        var session = await handle.Completion;

        Assert.Equal(0, session.ExitCode);
        Assert.False(session.StartupFailure);
        Assert.Contains("hi", session.Stdout);
    }

    [Fact]
    public async Task NonZeroExitAfterWindow_IsNotStartupFailure()
    {
        var handle = await Launch("--sleep", "2500", "--exit", "4");

        Assert.True(await handle.WaitForStartupAsync(CancellationToken.None));
        var session = await handle.Completion;

        Assert.Equal(4, session.ExitCode);
        Assert.False(session.StartupFailure);
    }

    [Fact]
    public async Task LongRunningGame_SurvivesWindowAndStopKillsIt()
    {
        var handle = await Launch("--stdout", "up");

        Assert.True(await handle.WaitForStartupAsync(CancellationToken.None));
        Assert.False(handle.HasExited);

        await handle.StopAsync();
        var session = await handle.Completion;

        Assert.True(session.StoppedByUser);
        Assert.Null(session.ExitCode);
        Assert.False(session.StartupFailure);
        Assert.Contains("up", session.Stdout);
        Assert.True(handle.HasExited);
        Assert.True(IsGone(handle.ProcessId));
    }

    [Fact]
    public async Task WorkingDirectory_IsHonored()
    {
        var work = Directory.CreateTempSubdirectory("xr-launch-");
        try
        {
            var handle = await LaunchIn(work.FullName, new[] { "--cwd", "--exit", "0" });
            var session = await handle.Completion;

            Assert.Equal(work.FullName, session.Stdout.Trim(), ignoreCase: OperatingSystem.IsWindows());
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task MissingFile_FailsClearly()
    {
        var launcher = new GameLauncher(_window);
        var asked = false;

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => launcher.LaunchAsync(
            Path.Combine(Path.GetTempPath(), "xr-missing", "nope.exe"),
            Path.GetTempPath(),
            _ => { asked = true; return Task.FromResult(true); },
            CancellationToken.None));

        Assert.Contains("Build the project first", ex.Message);
        Assert.False(asked);
    }

    [Fact]
    public async Task DllPath_RunsThroughDotnetHost()
    {
        var dll = Path.ChangeExtension(FindGame(), ".dll");
        var handle = await new GameLauncher(_window).LaunchAsync(
            dll, Path.GetDirectoryName(dll)!, _ => Task.FromResult(true), CancellationToken.None);
        Assert.NotNull(handle);
        Assert.True(await handle.WaitForStartupAsync(CancellationToken.None));

        using var process = Process.GetProcessById(handle.ProcessId);
        Assert.Contains("dotnet", process.ProcessName, StringComparison.OrdinalIgnoreCase);

        await handle.StopAsync();
        Assert.True(IsGone(handle.ProcessId));
    }

    private static Task<RunHandle> Launch(params string[] args) =>
        LaunchIn(Path.GetDirectoryName(FindGame())!, args);

    private static async Task<RunHandle> LaunchIn(string workingDirectory, string[] args)
    {
        var handle = await new GameLauncher(_window).LaunchAsync(
            FindGame(), workingDirectory, _ => Task.FromResult(true), CancellationToken.None, args);
        return handle ?? throw new InvalidOperationException("Launch was declined.");
    }

    private static string FindGame()
    {
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "XnaReborn.sln")))
            {
                var exe = OperatingSystem.IsWindows() ? "XnaReborn.TestGame.exe" : "XnaReborn.TestGame";
                var path = Path.Combine(dir.FullName, "tests", "XnaReborn.TestGame", "bin", config, "net8.0", exe);
                Assert.True(File.Exists(path), $"Test game not built: {path}");
                return path;
            }
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static bool IsGone(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
}
