namespace XnaReborn.Core.Models;

/// <summary>A problem with the analysis itself, not with the analyzed project.</summary>
/// <param name="Code">Diagnostic code, such as "ANA001".</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="File">Relative path of the file involved, or null.</param>
public sealed record Diagnostic(string Code, string Message, string? File);
