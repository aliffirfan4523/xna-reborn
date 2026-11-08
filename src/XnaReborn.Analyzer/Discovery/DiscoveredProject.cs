namespace XnaReborn.Analyzer.Discovery;

/// <summary>A project file found during discovery.</summary>
/// <param name="RelativePath">Path relative to the discovery root, using '/' separators.</param>
/// <param name="FullPath">Absolute path of the .csproj file.</param>
/// <param name="IsXna">True when the project references XNA Game Studio or the XNA Framework.</param>
public sealed record DiscoveredProject(string RelativePath, string FullPath, bool IsXna);
