namespace XnaReborn.Migrator.Output;

/// <summary>The verdict of an <see cref="OutputGuard"/> check.</summary>
/// <param name="Allowed">True when the migration may write to the output location.</param>
/// <param name="Problem">The reason for refusal, or <see cref="GuardProblem.None"/> when allowed.</param>
/// <param name="Message">A plain sentence telling the user what happened and what to do.</param>
public sealed record GuardResult(bool Allowed, GuardProblem Problem, string Message);
