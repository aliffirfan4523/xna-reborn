using System.Diagnostics;
using XnaReborn.Runner.Processes;

namespace XnaReborn.Tests.Runner;

[Trait("Category", "Runner")]
[Trait("Category", "Process")]
public sealed class ProcessRunnerTests
{
    private static readonly string _exe = LocateTestGame();

    private static string LocateTestGame()
    {
        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var configuration = new DirectoryInfo(baseDir).Parent!.Name;
        var dir = new DirectoryInfo(baseDir);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "XnaReborn.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var folder = Path.Combine(dir.FullName, "tests", "XnaReborn.TestGame", "bin", configuration, "net8.0");
        var name = OperatingSystem.IsWindows() ? "XnaReborn.TestGame.exe" : "XnaReborn.TestGame";
        var path = Path.Combine(folder, name);
        Assert.True(File.Exists(path), "TestGame was not built: " + path);
        return path;
    }

    private static ProcessRequest Game(string exe, TimeSpan? timeout, params string[] args) =>
        new(exe, args, Path.GetDirectoryName(exe)!, timeout);

    private sealed class Collector : IProgress<OutputLine>
    {
        private readonly List<(OutputLine Line, bool Ended)> _items = [];
        public Func<bool> Ended { get; set; } = () => false;
        public void Report(OutputLine value)
        {
            lock (_items)
            {
                _items.Add((value, Ended()));
            }
        }

        public List<(OutputLine Line, bool Ended)> Snapshot()
        {
            lock (_items)
            {
                return [.. _items];
            }
        }
    }

    [Fact]
    public async Task Success_captures_stdout_and_stderr_in_order()
    {
        var lines = new Collector();
        var result = await new ProcessRunner().RunAsync(
            Game(_exe, TimeSpan.FromSeconds(30), "--stdout", "one", "--stdout", "two", "--stderr", "err", "--stdout", "three", "--exit", "0"),
            lines,
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.False(result.Cancelled);
        Assert.Null(result.StartError);
        Assert.Equal("one\ntwo\nthree", result.Stdout);
        Assert.Equal("err", result.Stderr);
        Assert.Equal(["one", "two", "three"], lines.Snapshot().Where(x => !x.Line.IsError).Select(x => x.Line.Text));
        Assert.Equal(["err"], lines.Snapshot().Where(x => x.Line.IsError).Select(x => x.Line.Text));
    }

    [Fact]
    public async Task Non_zero_exit_code_is_reported()
    {
        var result = await new ProcessRunner().RunAsync(
            Game(_exe, TimeSpan.FromSeconds(30), "--stderr", "boom", "--exit", "3"), null, CancellationToken.None);

        Assert.Equal(3, result.ExitCode);
        Assert.Equal("boom", result.Stderr);
    }

    [Fact]
    public async Task Working_directory_is_honored()
    {
        var folder = Path.Combine(Path.GetTempPath(), "xnareborn-runner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var request = new ProcessRequest(_exe, ["--cwd", "--exit", "0"], folder, TimeSpan.FromSeconds(30));
            var result = await new ProcessRunner().RunAsync(request, null, CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar), result.Stdout.Trim().TrimEnd(Path.DirectorySeparatorChar));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task Arguments_with_spaces_and_quotes_arrive_unchanged()
    {
        var text = "a \"quoted\" value & more";
        var result = await new ProcessRunner().RunAsync(
            Game(_exe, TimeSpan.FromSeconds(30), "--stdout", text, "--exit", "0"), null, CancellationToken.None);

        Assert.Equal(text, result.Stdout);
    }

    // The environment-variable case is skipped on purpose: TestGame cannot print a variable, and it may not be edited.

    [Fact]
    public async Task Timeout_kills_the_process_and_reports_TimedOut()
    {
        var watch = Stopwatch.StartNew();
        var result = await new ProcessRunner().RunAsync(
            Game(_exe, TimeSpan.FromMilliseconds(800), "--stdout", "started"), null, CancellationToken.None);
        watch.Stop();

        Assert.True(result.TimedOut);
        Assert.False(result.Cancelled);
        Assert.Null(result.ExitCode);
        Assert.Equal("started", result.Stdout);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), "Took " + watch.Elapsed);
    }

    [Fact]
    public async Task Cancellation_kills_the_process_and_reports_Cancelled()
    {
        using var cts = new CancellationTokenSource();
        var lines = new Collector();
        var task = new ProcessRunner().RunAsync(Game(_exe, null, "--stdout", "started"), lines, cts.Token);

        await WaitUntilAsync(() => lines.Snapshot().Count > 0);
        cts.Cancel();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.True(result.Cancelled);
        Assert.False(result.TimedOut);
        Assert.Null(result.ExitCode);
    }

    [Fact]
    public async Task Already_cancelled_token_starts_nothing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await new ProcessRunner().RunAsync(Game(_exe, null, "--exit", "0"), null, cts.Token);

        Assert.True(result.Cancelled);
        Assert.Null(result.ExitCode);
    }

    [Fact]
    public async Task Process_is_really_gone_after_timeout_and_after_cancellation()
    {
        // Run a private copy of the game so its executable path identifies exactly this process.
        var copy = CopyGame();
        try
        {
            var runner = new ProcessRunner();

            var timedOut = await runner.RunAsync(Game(copy, TimeSpan.FromMilliseconds(800), "--stdout", "up"), null, CancellationToken.None);
            Assert.True(timedOut.TimedOut);
            Assert.Empty(FindProcesses(copy));

            using var cts = new CancellationTokenSource();
            var lines = new Collector();
            var task = runner.RunAsync(Game(copy, null, "--stdout", "up"), lines, cts.Token);
            await WaitUntilAsync(() => lines.Snapshot().Count > 0);
            Assert.Single(FindProcesses(copy));
            cts.Cancel();
            var cancelled = await task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(cancelled.Cancelled);
            Assert.Empty(FindProcesses(copy));
        }
        finally
        {
            TryDelete(Path.GetDirectoryName(copy)!);
        }
    }

    [Fact]
    public async Task Missing_file_returns_StartError_instead_of_throwing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "xnareborn-does-not-exist-" + Guid.NewGuid().ToString("N") + ".exe");
        var result = await new ProcessRunner().RunAsync(
            new ProcessRequest(missing, [], Path.GetTempPath(), TimeSpan.FromSeconds(5)), null, CancellationToken.None);

        Assert.NotNull(result.StartError);
        Assert.NotEmpty(result.StartError);
        Assert.Null(result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task Missing_working_directory_returns_StartError()
    {
        var folder = Path.Combine(Path.GetTempPath(), "xnareborn-no-folder-" + Guid.NewGuid().ToString("N"));
        var result = await new ProcessRunner().RunAsync(
            new ProcessRequest(_exe, ["--exit", "0"], folder, TimeSpan.FromSeconds(5)), null, CancellationToken.None);

        Assert.NotNull(result.StartError);
        Assert.Null(result.ExitCode);
    }

    [Fact]
    public async Task Lines_arrive_before_the_process_ends()
    {
        var ended = false;
        var lines = new Collector { Ended = () => ended };
        var result = await new ProcessRunner().RunAsync(
            Game(_exe, TimeSpan.FromSeconds(30), "--stdout", "a", "--sleep", "500", "--stdout", "b", "--exit", "0"),
            lines,
            CancellationToken.None);
        ended = true;

        Assert.Equal(0, result.ExitCode);
        var seen = lines.Snapshot();
        Assert.Equal(["a", "b"], seen.Select(x => x.Line.Text));
        Assert.False(seen[0].Ended);
    }

    [Fact]
    public async Task Lines_are_delivered_while_the_process_is_still_running()
    {
        var lines = new Collector();
        var task = new ProcessRunner().RunAsync(
            Game(_exe, TimeSpan.FromSeconds(30), "--stdout", "a", "--sleep", "1500", "--stdout", "b", "--exit", "0"),
            lines,
            CancellationToken.None);

        await WaitUntilAsync(() => lines.Snapshot().Count >= 1);
        Assert.False(task.IsCompleted);
        Assert.Equal("a", lines.Snapshot()[0].Line.Text);
        await task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Stream_yields_lines_and_completes_with_the_result()
    {
        var reader = new ProcessRunner().Stream(
            Game(_exe, TimeSpan.FromSeconds(30), "--stdout", "x", "--stderr", "y", "--exit", "4"),
            CancellationToken.None,
            out var completion);

        var seen = new List<OutputLine>();
        await foreach (var line in reader.ReadAllAsync())
        {
            seen.Add(line);
        }

        var result = await completion;
        Assert.Equal(4, result.ExitCode);
        Assert.Contains(new OutputLine("x", false), seen);
        Assert.Contains(new OutputLine("y", true), seen);
        Assert.Equal(2, seen.Count);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for output.");
            await Task.Delay(25);
        }
    }

    private static string CopyGame()
    {
        var source = Path.GetDirectoryName(_exe)!;
        var target = Path.Combine(Path.GetTempPath(), "xnareborn-game-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        return Path.Combine(target, Path.GetFileName(_exe));
    }

    private static List<int> FindProcesses(string exePath)
    {
        var ids = new List<int>();
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exePath)))
        {
            using (process)
            {
                try
                {
                    if (!process.HasExited && string.Equals(process.MainModule?.FileName, exePath, StringComparison.OrdinalIgnoreCase))
                    {
                        ids.Add(process.Id);
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Exited or not inspectable: not ours.
                }
            }
        }

        return ids;
    }

    private static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, true);
        }
        catch (IOException)
        {
            // Best effort cleanup of a temp folder.
        }
    }
}
