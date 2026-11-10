namespace XnaReborn.App.ViewModels;

/// <summary>One line of the live build log.</summary>
/// <param name="Number">1-based position of the line in this run.</param>
/// <param name="Text">The line without its newline.</param>
/// <param name="IsError">True when the line came from standard error.</param>
public sealed record BuildRunLogLine(int Number, string Text, bool IsError);
