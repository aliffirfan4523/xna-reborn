using XnaReborn.Runner.Hints;

namespace XnaReborn.Runner.Run;

/// <summary>One launch of a built game.</summary>
/// <param name="ExitCode">Exit code, or null when the game is still running or was stopped before it exited on its own.</param>
/// <param name="StartupFailure">True when the game exited with a non-zero code inside the early-crash window.</param>
/// <param name="Stdout">Captured standard output.</param>
/// <param name="Stderr">Captured standard error.</param>
/// <param name="RuntimeHints">Hints for runtime failures recognized in the captured output.</param>
/// <param name="StoppedByUser">True when the caller stopped the game.</param>
public sealed record RunSession(
    int? ExitCode,
    bool StartupFailure,
    string Stdout,
    string Stderr,
    IReadOnlyList<FixHint> RuntimeHints,
    bool StoppedByUser);
