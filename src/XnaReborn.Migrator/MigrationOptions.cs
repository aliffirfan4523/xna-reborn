namespace XnaReborn.Migrator;

/// <summary>What the user chose for one migration run.</summary>
/// <param name="OutputFolder">Folder that receives the migrated copy. Must be outside the source folder.</param>
/// <param name="Target">MonoGame platform package to use.</param>
/// <param name="EnableShims">Whether to generate compatibility shims for dead APIs. Off by default.</param>
/// <param name="DryRun">When true, compute the plan and changes but write nothing.</param>
/// <param name="ExcludedPlanItemIds">Ids of plan items the user switched off.</param>
/// <param name="SubstituteFonts">
/// When true, a <c>.spritefont</c> that names a font only XNA Game Studio installed gets a Windows font in the migrated copy.
/// When false (the default), such an asset is listed as manual work and left out of <c>Content.mgcb</c>.
/// </param>
/// <param name="AllowWindowResize">When true, the migrated game's window can be resized and the game keeps its own resolution, scaled to fit. Off by default.</param>
/// <param name="FullscreenToggle">When true, Alt+Enter switches between a window and borderless fullscreen at the game's own resolution, scaled to fit. Off by default.</param>
/// <param name="HighDpi">When true, the game process asks Windows for per-monitor DPI awareness so the window is not blurred by system scaling. Off by default.</param>
/// <param name="RewriteEffects">
/// When true (the default), <c>.fx</c> files in the migrated copy get per-target shader model macros, sampler fixes and markers
/// for constructs MonoGame rejects. When false they are copied unchanged.
/// </param>
/// <param name="LegacyTarget">
/// For an old MonoGame project: the platform package to use instead of the one chosen from the project's old platform
/// (DirectX Windows becomes WindowsDX, everything else DesktopGL). Null keeps that choice. Ignored for XNA projects.
/// </param>
public sealed record MigrationOptions(
    string OutputFolder,
    MonoGameTarget Target,
    bool EnableShims,
    bool DryRun,
    IReadOnlySet<string> ExcludedPlanItemIds,
    bool SubstituteFonts = false,
    bool AllowWindowResize = false,
    bool FullscreenToggle = false,
    bool HighDpi = false,
    MonoGameTarget? LegacyTarget = null,
    bool RewriteEffects = true);
