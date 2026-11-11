using XnaReborn.Core.Mgcb;

namespace XnaReborn.Analyzer.Reading;

/// <summary>A Content.mgcb file that was read and parsed for a project.</summary>
/// <param name="Path">Path relative to the analysis root, using '/' separators.</param>
/// <param name="File">The parsed file.</param>
public sealed record ParsedMgcb(string Path, MgcbFile File);
