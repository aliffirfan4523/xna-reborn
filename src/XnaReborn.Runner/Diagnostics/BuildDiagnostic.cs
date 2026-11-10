namespace XnaReborn.Runner.Diagnostics;

/// <summary>How serious a build diagnostic is.</summary>
public enum BuildDiagnosticSeverity
{
    /// <summary>The build failed because of it.</summary>
    Error,

    /// <summary>The build went on, but something may be wrong.</summary>
    Warning,

    /// <summary>For information only.</summary>
    Info,
}

/// <summary>
/// One structured build error or warning. Named <c>BuildDiagnostic</c> rather than <c>Diagnostic</c>
/// so it does not clash with the analyzer's <c>XnaReborn.Core.Models.Diagnostic</c>.
/// </summary>
/// <param name="Severity">Error, warning or info.</param>
/// <param name="Code">Code such as CS0246, MSB3202, NU1101, or MGCB for content builder errors.</param>
/// <param name="Message">The message text.</param>
/// <param name="File">Source file path as printed, or null.</param>
/// <param name="Line">1-based line, or null.</param>
/// <param name="Column">1-based column, or null.</param>
/// <param name="Project">Project file path from the trailing <c>[...]</c> of an MSBuild line, or null.</param>
/// <param name="AssetPath">Content asset path for content builder errors, or null.</param>
public sealed record BuildDiagnostic(
    BuildDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? File,
    int? Line,
    int? Column,
    string? Project,
    string? AssetPath);
