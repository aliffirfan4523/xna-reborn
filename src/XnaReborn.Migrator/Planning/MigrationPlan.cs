namespace XnaReborn.Migrator.Planning;

/// <summary>The ordered list of steps a migration will run.</summary>
/// <param name="Items">Steps after exclusions were applied.</param>
/// <param name="ExcludedItems">Steps the user switched off, kept so a UI can show them unchecked.</param>
public sealed record MigrationPlan(IReadOnlyList<PlanItem> Items, IReadOnlyList<PlanItem> ExcludedItems);
