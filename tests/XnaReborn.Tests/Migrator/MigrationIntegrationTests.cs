using System.Diagnostics;
using System.Text;
using XnaReborn.Tests.Fixtures;
using Xunit.Abstractions;

namespace XnaReborn.Tests.Migrator;

/// <summary>
/// Migrates the buildable XNA 3.1 fixture and the XNA 4.0 content fixture and builds the results with the .NET SDK against MonoGame.
/// Needs the network for NuGet, so it runs only when <c>XNAREBORN_INTEGRATION=1</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MigrationIntegrationTests(ITestOutputHelper testOutput) : IDisposable
{
    private static readonly TimeSpan _buildTimeout = TimeSpan.FromMinutes(10);

    // A real 1x1 white PNG (RGBA, 120 bytes). The fixture's own hero.png is only a placeholder that the content builder cannot decode.
    private static readonly byte[] _onePixelPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x01, 0x73, 0x52, 0x47, 0x42, 0x00, 0xAE, 0xCE, 0x1C, 0xE9, 0x00, 0x00,
        0x00, 0x04, 0x67, 0x41, 0x4D, 0x41, 0x00, 0x00, 0xB1, 0x8F, 0x0B, 0xFC, 0x61, 0x05, 0x00, 0x00,
        0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00, 0x0E, 0xC3, 0x00, 0x00, 0x0E, 0xC3, 0x01, 0xC7,
        0x6F, 0xA8, 0x64, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x18, 0x57, 0x63, 0xF8, 0xFF,
        0xFF, 0xFF, 0x7F, 0x00, 0x09, 0xFB, 0x03, 0xFD, 0x05, 0x43, 0x45, 0xCA, 0x00, 0x00, 0x00, 0x00,
        0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
    ];

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

    [IntegrationFact]
    public async Task Migrated_copy_builds_inside_a_folder_with_central_package_management_and_strict_build_props()
    {
        var parent = Path.Combine(_temp, "parent");
        Directory.CreateDirectory(parent);
        File.WriteAllText(
            Path.Combine(parent, "Directory.Packages.props"),
            "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup></Project>\n");
        File.WriteAllText(
            Path.Combine(parent, "Directory.Build.props"),
            "<Project><PropertyGroup><TreatWarningsAsErrors>true</TreatWarningsAsErrors><Nullable>enable</Nullable></PropertyGroup></Project>\n");

        var output = Path.Combine(parent, "child");
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna31-rewritable"), MigrationTestSupport.Options(output));
        Assert.True(result.Completed);

        var (exitCode, text) = await RunBuildAsync(output);
        testOutput.WriteLine(text);

        Assert.True(exitCode == 0, $"dotnet build failed with exit code {exitCode}.{Environment.NewLine}{text}");
    }

    [IntegrationFact]
    public async Task Migrated_basic_fixture_builds_its_content_into_xnb_files()
    {
        var source = Path.Combine(_temp, "source");
        MigrationTestSupport.CopyFolder(FixturePaths.Of("xna40-basic"), source);
        File.WriteAllBytes(Path.Combine(source, "Content", "Textures", "hero.png"), _onePixelPng);
        var output = Path.Combine(_temp, "migrated-content");

        var result = await MigrationTestSupport.MigrateAsync(source, MigrationTestSupport.Options(output));
        Assert.True(result.Completed);
        Assert.NotNull(result.Manifest.Content);

        var (exitCode, text) = await RunBuildAsync(output);
        testOutput.WriteLine(text);

        Assert.True(exitCode == 0, $"dotnet build failed with exit code {exitCode}.{Environment.NewLine}{text}");
        var built = Path.Combine(output, "bin", "Debug", "net8.0");
        Assert.True(File.Exists(Path.Combine(built, "Content", "Textures", "hero.xnb")), "Content/Textures/hero.xnb is missing from " + built);
        Assert.True(File.Exists(Path.Combine(built, "Content", "Fonts", "Font.xnb")), "Content/Fonts/Font.xnb is missing from " + built);
    }

    [IntegrationFact]
    public async Task Migrated_prebuilt_fixture_builds_and_ships_its_prebuilt_assets()
    {
        var output = Path.Combine(_temp, "migrated-prebuilt");
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna40-prebuilt"), MigrationTestSupport.Options(output));
        Assert.True(result.Completed);

        var (exitCode, text) = await RunBuildAsync(output);
        testOutput.WriteLine(text);

        Assert.True(exitCode == 0, $"dotnet build failed with exit code {exitCode}.{Environment.NewLine}{text}");
        var built = Path.Combine(output, "bin", "Debug", "net8.0");
        Assert.True(File.Exists(Path.Combine(built, "Content", "hero.xnb")), "Content/hero.xnb is missing from " + built);
        Assert.True(File.Exists(Path.Combine(built, "Content", "sheet.xnb")), "Content/sheet.xnb is missing from " + built);
        Assert.True(File.Exists(Path.Combine(built, "Data", "levels.txt")), "Data/levels.txt is missing from " + built);
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
