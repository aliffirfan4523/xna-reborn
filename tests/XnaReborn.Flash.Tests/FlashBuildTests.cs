using System.Diagnostics;
using XnaReborn.Flash.Converter;

namespace XnaReborn.Flash.Tests;

/// <summary>Builds converted games with the .NET SDK. Needs the MonoGame packages in the local NuGet cache, so it only runs with XNAREBORN_INTEGRATION=1. No game is started.</summary>
public sealed class FlashBuildTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xnareborn-flashbuild-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    [SkippableIntegrationTheory]
    [InlineData("flx-hello")]
    [InlineData("flx-platformer")]
    public async Task Converted_fixture_builds_without_errors(string fixture)
    {
        var result = await FlashMigrator.MigrateAsync(FlashFixtures.Of(fixture), Path.Combine(_temp, fixture));
        var psi = new ProcessStartInfo("dotnet", $"build \"{Path.Combine(result.OutputFolder, result.ProjectFile)}\" -nologo -v q")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = result.OutputFolder,
        };
        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, output);
    }
}

/// <summary>A theory that is skipped unless XNAREBORN_INTEGRATION=1.</summary>
public sealed class SkippableIntegrationTheoryAttribute : TheoryAttribute
{
    public SkippableIntegrationTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("XNAREBORN_INTEGRATION") != "1")
        {
            Skip = "Set XNAREBORN_INTEGRATION=1 to build converted games.";
        }
    }
}
