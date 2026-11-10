using XnaReborn.Runner.Processes;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Runner;

/// <summary>A process runner that answers from a function and reports the output lines it would have printed.</summary>
internal sealed class FakeProcessRunner(Func<ProcessRequest, ProcessResult> respond) : IProcessRunner
{
    private readonly List<ProcessRequest> _requests = [];

    /// <summary>Every request received, in order.</summary>
    public IReadOnlyList<ProcessRequest> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    public Task<ProcessResult> RunAsync(ProcessRequest request, IProgress<OutputLine>? lines, CancellationToken cancellationToken)
    {
        lock (_requests)
        {
            _requests.Add(request);
        }

        var result = respond(request);
        if (lines is not null)
        {
            Report(lines, result.Stdout, false);
            Report(lines, result.Stderr, true);
        }

        return Task.FromResult(result);
    }

    public static ProcessResult Ok(string stdout = "") => new(0, false, false, stdout, string.Empty, TimeSpan.FromMilliseconds(5));

    public static ProcessResult Exit(int code, string stdout) => new(code, false, false, stdout, string.Empty, TimeSpan.FromMilliseconds(5));

    private static void Report(IProgress<OutputLine> lines, string text, bool isError)
    {
        if (text.Length == 0)
        {
            return;
        }

        foreach (var line in text.Split('\n'))
        {
            lines.Report(new OutputLine(line, isError));
        }
    }
}

/// <summary>A progress object that runs its handler on the calling thread, so tests see events in order.</summary>
internal sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}

/// <summary>Paths used by several Runner tests.</summary>
internal static class RunnerTestPaths
{
    /// <summary>Folder with the real build output samples.</summary>
    public static string Samples { get; } =
        Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "XnaReborn.Tests", "Runner", "Samples"));

    public static string ReadSample(string name) => File.ReadAllText(Path.Combine(Samples, name + ".txt"));

    /// <summary>The built stand-in game (tests/XnaReborn.TestGame).</summary>
    public static string TestGame()
    {
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var exe = OperatingSystem.IsWindows() ? "XnaReborn.TestGame.exe" : "XnaReborn.TestGame";
        var path = Path.Combine(FixturePaths.Root, "..", "XnaReborn.TestGame", "bin", config, "net8.0", exe);
        path = Path.GetFullPath(path);
        Assert.True(File.Exists(path), "Test game not built: " + path);
        return path;
    }
}
