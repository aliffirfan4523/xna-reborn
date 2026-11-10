using System.Diagnostics;
using System.Threading.Channels;

namespace XnaReborn.Runner.Processes;

/// <summary>
/// Starts a process without a shell, reads its output line by line in arrival order, and stops the whole
/// process tree on timeout or cancellation. Never requests elevation and never uses the network.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan _readerGrace = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public Task<ProcessResult> RunAsync(ProcessRequest request, IProgress<OutputLine>? lines, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RunCoreAsync(request, lines is null ? null : lines.Report, cancellationToken);
    }

    /// <summary>
    /// Starts the process and returns its output lines as a channel. The channel is completed when the process
    /// has ended and all output was delivered. Read <paramref name="completion"/> for the final result.
    /// </summary>
    /// <param name="request">What to run.</param>
    /// <param name="cancellationToken">Stops the process tree.</param>
    /// <param name="completion">Completes with how the process ended (never faults for a non-zero exit, timeout, cancel or start failure).</param>
    /// <returns>A reader of output lines in arrival order.</returns>
    public ChannelReader<OutputLine> Stream(ProcessRequest request, CancellationToken cancellationToken, out Task<ProcessResult> completion)
    {
        ArgumentNullException.ThrowIfNull(request);
        var channel = Channel.CreateUnbounded<OutputLine>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        completion = Task.Run(async () =>
        {
            try
            {
                return await RunCoreAsync(request, line => channel.Writer.TryWrite(line), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        });
        return channel.Reader;
    }

    private static async Task<ProcessResult> RunCoreAsync(ProcessRequest request, Action<OutputLine>? sink, CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();
        if (cancellationToken.IsCancellationRequested)
        {
            return new ProcessResult(null, false, true, string.Empty, string.Empty, started.Elapsed);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
        };
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (request.Environment is not null)
        {
            foreach (var pair in request.Environment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException or PlatformNotSupportedException or IOException)
        {
            return new ProcessResult(null, false, false, string.Empty, string.Empty, started.Elapsed, ex.Message);
        }

        var gate = new object();
        var stdout = new List<string>();
        var stderr = new List<string>();

        void Add(string text, bool isError)
        {
            lock (gate)
            {
                (isError ? stderr : stdout).Add(text);
                sink?.Invoke(new OutputLine(text, isError));
            }
        }

        async Task PumpAsync(StreamReader reader, bool isError)
        {
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                Add(line, isError);
            }
        }

        var readers = Task.WhenAll(
            Task.Run(() => PumpAsync(process.StandardOutput, false)),
            Task.Run(() => PumpAsync(process.StandardError, true)));

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (request.Timeout is { } timeout)
        {
            limit.CancelAfter(timeout);
        }

        var timedOut = false;
        var cancelled = false;
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = cancellationToken.IsCancellationRequested;
            timedOut = !cancelled;
            KillTree(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        // Pipes close once the tree is gone. Do not hang forever if a detached grandchild keeps them open.
        await Task.WhenAny(readers, Task.Delay(timedOut || cancelled ? _readerGrace : Timeout.InfiniteTimeSpan)).ConfigureAwait(false);

        int? exitCode = timedOut || cancelled ? null : process.ExitCode;
        string[] outLines;
        string[] errLines;
        lock (gate)
        {
            outLines = [.. stdout];
            errLines = [.. stderr];
        }

        return new ProcessResult(exitCode, timedOut, cancelled, string.Join('\n', outLines), string.Join('\n', errLines), started.Elapsed);
    }

    private static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // Already exited, or access denied for a child; the wait that follows decides the outcome.
        }
    }
}
