using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Discovery;

/// <summary>The outcome of looking for XNA and MonoGame projects under a path.</summary>
/// <param name="Root">Absolute path of the root folder.</param>
/// <param name="Projects">Every .csproj found, sorted by relative path.</param>
/// <param name="NoProjectReason">Why there is nothing to migrate, or null when at least one XNA or legacy MonoGame project exists.</param>
/// <param name="Diagnostics">Problems met during discovery.</param>
public sealed record DiscoveryResult(
    string Root,
    IReadOnlyList<DiscoveredProject> Projects,
    string? NoProjectReason,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>The discovered projects that use XNA.</summary>
    public IEnumerable<DiscoveredProject> XnaProjects => Projects.Where(p => p.IsXna);

    /// <summary>The discovered projects that use the SharpDX Toolkit.</summary>
    public IEnumerable<DiscoveredProject> SharpDxToolkitProjects => Projects.Where(p => p.IsSharpDxToolkit);

    /// <summary>The discovered projects that use XNA or the SharpDX Toolkit.</summary>
    public IEnumerable<DiscoveredProject> MigratableProjects => Projects.Where(p => p.IsXna || p.IsSharpDxToolkit);

    /// <summary>The discovered MonoGame projects, legacy and current.</summary>
    public IEnumerable<DiscoveredProject> MonoGameProjects => Projects.Where(p => p.IsMonoGame);
}
