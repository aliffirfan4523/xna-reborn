namespace XnaReborn.Runner.Processes;

/// <summary>What to start.</summary>
/// <param name="FileName">Program to run, such as "dotnet".</param>
/// <param name="Arguments">Arguments, passed one by one (no shell quoting).</param>
/// <param name="WorkingDirectory">Working directory of the process.</param>
/// <param name="Timeout">Stop the process tree after this long, or null for no limit.</param>
/// <param name="Environment">Extra environment variables, or null.</param>
public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan? Timeout,
    IReadOnlyDictionary<string, string>? Environment = null);

/// <summary>One line of process output, in arrival order.</summary>
/// <param name="Text">The line without its newline.</param>
/// <param name="IsError">True for standard error.</param>
public sealed record OutputLine(string Text, bool IsError);

/// <summary>How a process ended.</summary>
/// <param name="ExitCode">Exit code, or null when the process was stopped (timeout or cancel) or never started.</param>
/// <param name="TimedOut">True when the timeout stopped it.</param>
/// <param name="Cancelled">True when the caller's token stopped it.</param>
/// <param name="Stdout">All standard output.</param>
/// <param name="Stderr">All standard error.</param>
/// <param name="Duration">Wall time from start to end.</param>
/// <param name="StartError">Why the process could not be started (for example, file not found), or null.</param>
public sealed record ProcessResult(
    int? ExitCode,
    bool TimedOut,
    bool Cancelled,
    string Stdout,
    string Stderr,
    TimeSpan Duration,
    string? StartError = null);

/// <summary>Runs a process to completion while streaming its output. Implemented by <c>ProcessRunner</c>; tests use fakes.</summary>
public interface IProcessRunner
{
    /// <summary>Runs the process. On timeout or cancellation the whole process tree is stopped and a result is still returned.</summary>
    /// <param name="request">What to run.</param>
    /// <param name="lines">Receives each output line as it arrives, or null.</param>
    /// <param name="cancellationToken">Stops the process tree.</param>
    /// <returns>How the process ended. Never throws for a non-zero exit, timeout, cancel or start failure.</returns>
    Task<ProcessResult> RunAsync(ProcessRequest request, IProgress<OutputLine>? lines, CancellationToken cancellationToken);
}
