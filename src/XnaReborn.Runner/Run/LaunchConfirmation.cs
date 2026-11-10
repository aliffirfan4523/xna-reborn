namespace XnaReborn.Runner.Run;

/// <summary>What the caller is asked to approve before a game is started.</summary>
/// <param name="GamePath">Full path of the program that would run.</param>
/// <param name="Message">Plain-language explanation of what starting the game means.</param>
public sealed record LaunchConfirmation(string GamePath, string Message);
