namespace XnaReborn.Migrator;

/// <summary>What the user chose for one migration run.</summary>
/// <param name="OutputFolder">Folder that receives the migrated copy. Must be outside the source folder.</param>
/// <param name="Target">MonoGame platform package to use.</param>
/// <param name="EnableShims">Whether to generate compatibility shims for dead APIs. Off by default.</param>
/// <param name="DryRun">When true, compute the plan and changes but write nothing.</param>
/// <param name="ExcludedPlanItemIds">Ids of plan items the user switched off.</param>
public sealed record MigrationOptions(
    string OutputFolder,
    MonoGameTarget Target,
    bool EnableShims,
    bool DryRun,
    IReadOnlySet<string> ExcludedPlanItemIds);
