namespace XnaReborn.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that need the network (NuGet restore). They are skipped unless the
/// environment variable <c>XNAREBORN_INTEGRATION</c> is <c>1</c>.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    /// <summary>Name of the environment variable that turns the integration tests on.</summary>
    public const string EnvironmentVariable = "XNAREBORN_INTEGRATION";

    /// <summary>Creates the attribute and sets <see cref="FactAttribute.Skip"/> when the tests are not enabled.</summary>
    public IntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
        {
            Skip = $"Integration test: set {EnvironmentVariable}=1 to run it (it restores NuGet packages).";
        }
    }
}
