namespace XnaReborn.Migrator;

/// <summary>A progress update from <see cref="ProjectMigrator"/>.</summary>
/// <param name="Stage">Name of the stage that is running, such as "Rewriting source".</param>
/// <param name="Completed">Steps of this stage that are done.</param>
/// <param name="Total">Steps in this stage.</param>
/// <param name="Current">The item being worked on, such as a relative file path, or null.</param>
public sealed record MigrationProgress(string Stage, int Completed, int Total, string? Current);
