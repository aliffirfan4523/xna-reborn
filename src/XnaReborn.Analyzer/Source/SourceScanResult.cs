using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Source;

/// <summary>The result of scanning the C# files of one project.</summary>
/// <param name="Findings">Matches ordered by file path, line, column, and rule order.</param>
/// <param name="Diagnostics">Problems found while scanning, ordered by file path and then code.</param>
public sealed record SourceScanResult(IReadOnlyList<Finding> Findings, IReadOnlyList<Diagnostic> Diagnostics);
