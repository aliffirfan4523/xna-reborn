using System.Diagnostics;
using System.Text;
using XnaReborn.Runner.Hints;

namespace XnaReborn.Runner.Run;

/// <summary>A running game started by <see cref="GameLauncher"/>.</summary>
public sealed class RunHandle
{
    private readonly Process _process;
    private readonly TimeSpan _earlyCrashWindow;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();
    private readonly object _gate = new();
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _stopRequested;
    private int _exitedFlag;

    internal RunHandle(Process process, TimeSpan earlyCrashWindow)
    {
        _process = process;
        _earlyCrashWindow = earlyCrashWindow;
        ProcessId = process.Id;
        process.OutputDataReceived += (_, e) => Append(_stdout, e.Data);
        process.ErrorDataReceived += (_, e) => Append(_stderr, e.Data);
        process.Exited += (_, _) => OnExited();
        process.EnableRaisingEvents = true;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // The process may have ended before the handler was attached.
        if (HasProcessEnded())
        {
            OnExited();
        }

        Completion = BuildSessionAsync();
    }

    /// <summary>Operating system process id of the game.</summary>
    public int ProcessId { get; }

    /// <summary>True once the game process has ended.</summary>
    public bool HasExited => Volatile.Read(ref _exitedFlag) == 1;

    /// <summary>Completes when the game has ended and its output is fully captured. Never faults for a game crash.</summary>
    public Task<RunSession> Completion { get; }

    /// <summary>Waits through the early-crash window.</summary>
    /// <param name="cancellationToken">Stops waiting (the game keeps running).</param>
    /// <returns>True when the game is still running after the window, false when it exited inside it.</returns>
    public async Task<bool> WaitForStartupAsync(CancellationToken cancellationToken)
    {
        var remaining = _earlyCrashWindow - _clock.Elapsed;
        if (remaining > TimeSpan.Zero)
        {
            var delay = Task.Delay(remaining, cancellationToken);
            await Task.WhenAny(_exited.Task, delay).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return !HasExited;
    }

    /// <summary>Stops the game and everything it started, then waits for the session to complete.</summary>
    /// <returns>A task that completes when the process tree is gone.</returns>
    public async Task StopAsync()
    {
        if (!HasExited)
        {
            Interlocked.Exchange(ref _stopRequested, 1);
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Already exiting.
            }
        }

        await Completion.ConfigureAwait(false);
    }

    private static void Append(StringBuilder builder, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (builder)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(line);
        }
    }

    private bool HasProcessEnded()
    {
        try
        {
            return _process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private void OnExited()
    {
        if (Interlocked.Exchange(ref _exitedFlag, 1) == 0)
        {
            lock (_gate)
            {
                _exitedAt = _clock.Elapsed;
            }

            _exited.TrySetResult();
        }
    }

    private TimeSpan _exitedAt;

    private async Task<RunSession> BuildSessionAsync()
    {
        await _exited.Task.ConfigureAwait(false);

        // Drains the asynchronous output readers.
        await Task.Run(_process.WaitForExit).ConfigureAwait(false);

        var stopped = Volatile.Read(ref _stopRequested) == 1;
        int? exitCode = stopped ? null : _process.ExitCode;
        TimeSpan exitedAt;
        lock (_gate)
        {
            exitedAt = _exitedAt;
        }

        var startupFailure = !stopped && exitCode is not null and not 0 && exitedAt < _earlyCrashWindow;
        _process.Dispose();

        string stdout;
        string stderr;
        lock (_stdout)
        {
            stdout = _stdout.ToString();
        }

        lock (_stderr)
        {
            stderr = _stderr.ToString();
        }

        return new RunSession(exitCode, startupFailure, stdout, stderr, Array.Empty<FixHint>(), stopped);
    }
}
