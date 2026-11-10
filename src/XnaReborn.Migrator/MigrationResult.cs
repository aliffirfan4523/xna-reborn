using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;

namespace XnaReborn.Migrator;

/// <summary>The outcome of one migration run.</summary>
/// <param name="Plan">The plan that ran.</param>
/// <param name="Changes">Every computed file change, including for a dry run.</param>
/// <param name="Manifest">The manifest for the run.</param>
/// <param name="Completed">True when the output was written and moved into place. False for a dry run or a failed run.</param>
public sealed record MigrationResult(
    MigrationPlan Plan,
    IReadOnlyList<FileChange> Changes,
    Manifest Manifest,
    bool Completed);
