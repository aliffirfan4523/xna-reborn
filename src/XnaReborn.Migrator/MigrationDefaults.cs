using XnaReborn.Core.Models;

namespace XnaReborn.Migrator;

/// <summary>The choices the migrator suggests before the user decides.</summary>
public static class MigrationDefaults
{
    /// <summary>
    /// Suggests the MonoGame platform for an analyzed source. A report with a SharpDX Toolkit project gets WindowsDX, because the Toolkit
    /// effects are HLSL for Direct3D and WindowsDX is the MonoGame platform that compiles effects with the Direct3D shader compiler.
    /// Every other report gets DesktopGL, the default for XNA projects. The user can still pick either.
    /// </summary>
    /// <param name="report">The analysis report, or null.</param>
    /// <returns>The suggested target.</returns>
    public static MonoGameTarget TargetFor(AnalysisReport? report) =>
        report is not null && report.Projects.Any(p => p.Family == ProjectFamily.SharpDxToolkit)
            ? MonoGameTarget.WindowsDX
            : MonoGameTarget.DesktopGL;
}
