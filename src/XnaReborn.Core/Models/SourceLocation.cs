namespace XnaReborn.Core.Models;

/// <summary>A position in a source file.</summary>
/// <param name="File">Path relative to the project root, using forward slashes.</param>
/// <param name="Line">1-based line number.</param>
/// <param name="Column">1-based column number.</param>
public sealed record SourceLocation(string File, int Line, int Column);
