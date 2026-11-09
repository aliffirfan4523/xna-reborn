namespace XnaReborn.Migrator.Output;

/// <summary>Thrown when <see cref="OutputGuard.Begin"/> is refused.</summary>
/// <param name="result">The refusing verdict.</param>
public sealed class OutputGuardException(GuardResult result) : Exception(result.Message)
{
    /// <summary>The verdict that caused the exception.</summary>
    public GuardResult Result { get; } = result;
}
