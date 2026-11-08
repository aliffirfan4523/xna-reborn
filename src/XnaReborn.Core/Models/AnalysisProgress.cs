namespace XnaReborn.Core.Models;

/// <summary>A progress update sent while an analysis runs.</summary>
/// <param name="Stage">Name of the stage that is running.</param>
/// <param name="Completed">Units of work finished in this stage.</param>
/// <param name="Total">Total units of work in this stage.</param>
/// <param name="Current">Item being worked on now, or null.</param>
public sealed record AnalysisProgress(string Stage, int Completed, int Total, string? Current);
