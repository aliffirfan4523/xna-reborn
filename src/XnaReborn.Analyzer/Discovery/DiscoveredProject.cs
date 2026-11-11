using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Discovery;

/// <summary>A project file found during discovery.</summary>
/// <param name="RelativePath">Path relative to the discovery root, using '/' separators.</param>
/// <param name="FullPath">Absolute path of the .csproj file.</param>
/// <param name="Family">What kind of game project this is. <see cref="ProjectFamily.Other"/> also covers a file that could not be read.</param>
public sealed record DiscoveredProject(string RelativePath, string FullPath, ProjectFamily Family)
{
    /// <summary>True when the project references XNA Game Studio or the XNA Framework.</summary>
    public bool IsXna => Family == ProjectFamily.Xna;

    /// <summary>True when the project is a MonoGame project, legacy or current.</summary>
    public bool IsMonoGame => Family is ProjectFamily.MonoGameLegacy or ProjectFamily.MonoGameCurrent;
}
