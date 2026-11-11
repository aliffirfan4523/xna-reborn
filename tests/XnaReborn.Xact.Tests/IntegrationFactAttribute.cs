namespace XnaReborn.Xact.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that need the network (NuGet restore) and the audio tools. They are skipped unless the
/// environment variable <c>XNAREBORN_INTEGRATION</c> is <c>1</c>.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    /// <summary>Creates the attribute and sets <see cref="FactAttribute.Skip"/> when the tests are not enabled.</summary>
    public IntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("XNAREBORN_INTEGRATION") != "1")
        {
            Skip = "Integration test: set XNAREBORN_INTEGRATION=1 to run it (it restores NuGet packages and runs dotnet-mgcb, which needs ffmpeg and ffprobe).";
        }
    }
}
