using XnaReborn.Runner.Hints;

namespace XnaReborn.Runner.Build;

/// <summary>The outcome of building a migrated copy.</summary>
/// <param name="Succeeded">True when the build exited with code 0.</param>
/// <param name="Duration">How long the build took.</param>
/// <param name="Groups">Parsed diagnostics, grouped by hint.</param>
/// <param name="RawOutput">The full build output, kept next to the parsed diagnostics.</param>
/// <param name="ExitCode">Process exit code, or null when it never exited (timeout or cancel).</param>
/// <param name="TimedOut">True when the build hit its timeout and was stopped.</param>
/// <param name="Cancelled">True when the caller cancelled the build.</param>
public sealed record BuildResult(
    bool Succeeded,
    TimeSpan Duration,
    IReadOnlyList<DiagnosticGroup> Groups,
    string RawOutput,
    int? ExitCode,
    bool TimedOut,
    bool Cancelled);
