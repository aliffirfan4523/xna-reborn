namespace XnaReborn.Runner.Publish;

/// <summary>How a publish ended.</summary>
public enum PublishStatus
{
    /// <summary>The output folder holds the published game.</summary>
    Published,

    /// <summary>The output folder already has content and nothing was done. Ask the user, then publish again with confirmation.</summary>
    NeedsConfirmation,

    /// <summary>A guard said no (not a migrated copy, unknown RID, WindowsDX copy for a non-Windows RID). Nothing was done.</summary>
    Refused,

    /// <summary>The publish ran and failed, or did not produce the program.</summary>
    Failed,

    /// <summary>The publish took longer than its time limit and was stopped.</summary>
    TimedOut,

    /// <summary>The caller cancelled the publish.</summary>
    Cancelled,
}

/// <summary>What one publish did.</summary>
/// <param name="Status">How it ended.</param>
/// <param name="Message">A plain message for the user.</param>
/// <param name="OutputFolder">The folder the game is (or would be) published to, or null when none was worked out.</param>
/// <param name="Rid">The runtime identifier, or null when it was not valid.</param>
/// <param name="Program">Full path of the published program, or null when it was not published.</param>
/// <param name="Files">File names at the top of the output folder, sorted. Empty unless published.</param>
/// <param name="Duration">How long the publish ran. Zero when nothing ran.</param>
public sealed record PublishResult(
    PublishStatus Status,
    string Message,
    string? OutputFolder,
    string? Rid,
    string? Program,
    IReadOnlyList<string> Files,
    TimeSpan Duration)
{
    /// <summary>True when the game was published.</summary>
    public bool Succeeded => Status == PublishStatus.Published;
}
