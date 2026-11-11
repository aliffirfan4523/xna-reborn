using XnaReborn.Core.Models.Xact;

namespace XnaReborn.Migrator.Audio;

/// <summary>One converted <c>.xap</c>: what was read and what the content step planned from it. The migrator turns the list of these into the XACT catalog.</summary>
/// <param name="Project">The parsed <c>.xap</c>.</param>
/// <param name="Plan">The planned wave assets. A wave the pipeline cannot build is marked missing, as a wave file that does not exist is.</param>
public sealed record XactProjectPlan(XapProject Project, XactPlan Plan);
