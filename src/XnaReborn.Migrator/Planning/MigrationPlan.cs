namespace XnaReborn.Migrator.Planning;

/// <summary>The ordered list of steps a migration will run.</summary>
/// <param name="Items">Steps after exclusions were applied.</param>
public sealed record MigrationPlan(IReadOnlyList<PlanItem> Items);
