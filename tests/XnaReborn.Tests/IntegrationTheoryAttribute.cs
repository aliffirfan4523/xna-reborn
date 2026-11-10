namespace XnaReborn.Tests;

/// <summary>
/// A <see cref="TheoryAttribute"/> for tests that need the network (NuGet restore). They are skipped unless the
/// environment variable <c>XNAREBORN_INTEGRATION</c> is <c>1</c>. See <see cref="IntegrationFactAttribute"/>.
/// </summary>
public sealed class IntegrationTheoryAttribute : TheoryAttribute
{
    /// <summary>Creates the attribute and sets <see cref="FactAttribute.Skip"/> when the tests are not enabled.</summary>
    public IntegrationTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable(IntegrationFactAttribute.EnvironmentVariable) != "1")
        {
            Skip = $"Integration test: set {IntegrationFactAttribute.EnvironmentVariable}=1 to run it (it restores NuGet packages).";
        }
    }
}
