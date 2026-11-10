using System.Diagnostics;
using System.Text;
using XnaReborn.Tests.Fixtures;
using Xunit.Abstractions;

namespace XnaReborn.Tests.Migrator;

/// <summary>
/// Migrates the buildable XNA 3.1 fixture and builds the result with the .NET SDK against MonoGame.
/// Needs the network for NuGet, so it runs only when <c>XNAREBORN_INTEGRATION=1</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MigrationIntegrationTests(ITestOutputHelper testOutput) : IDisposable
{
    private static readonly TimeSpan _buildTimeout = TimeSpan.FromMinutes(10);

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-integration-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            try
            {
                Directory.Delete(_temp, recursive: true);
            }
            catch (IOException)
            {
                // A build server may still hold a file. The folder is in the temp directory and can be removed later.
            }
        }
    }

    [IntegrationFact]
    public async Task Migrated_rewritable_fixture_builds_against_MonoGame()
    {
        var output = Path.Combine(_temp, "migrated");
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna31-rewritable"), MigrationTestSupport.Options(output));
        Assert.True(result.Completed);

        var (exitCode, text) = await RunBuildAsync(output);
        testOutput.WriteLine(text);

        Assert.True(exitCode == 0, $"dotnet build failed with exit code {exitCode}.{Environment.NewLine}{text}");
    }

    private static async Task<(int ExitCode, string Output)> RunBuildAsync(string folder)
    {
        var start = new ProcessStartInfo("dotnet", "build --nologo -v minimal")
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        var log = new StringBuilder();
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => Append(log, e.Data);
        process.ErrorDataReceived += (_, e) => Append(log, e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = new CancellationTokenSource(_buildTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return (-1, $"dotnet build did not finish within {_buildTimeout.TotalMinutes} minutes.{Environment.NewLine}{Snapshot(log)}");
        }

        // Flush the redirected streams.
        process.WaitForExit();
        return (process.ExitCode, Snapshot(log));
    }

    private static void Append(StringBuilder log, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (log)
        {
            log.AppendLine(line);
        }
    }

    private static string Snapshot(StringBuilder log)
    {
        lock (log)
        {
            return log.ToString();
        }
    }
}
