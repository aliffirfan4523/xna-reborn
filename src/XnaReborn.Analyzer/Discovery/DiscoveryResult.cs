using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Discovery;

/// <summary>The outcome of looking for XNA projects under a path.</summary>
/// <param name="Root">Absolute path of the root folder.</param>
/// <param name="Projects">Every .csproj found, sorted by relative path.</param>
/// <param name="NoProjectReason">Why no XNA project was found, or null when at least one exists.</param>
/// <param name="Diagnostics">Problems met during discovery.</param>
public sealed record DiscoveryResult(
    string Root,
    IReadOnlyList<DiscoveredProject> Projects,
    string? NoProjectReason,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>The discovered projects that use XNA.</summary>
    public IEnumerable<DiscoveredProject> XnaProjects => Projects.Where(p => p.IsXna);
}
