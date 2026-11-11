using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Migrator;
using Xunit.Abstractions;

namespace XnaReborn.Tests.Device;

/// <summary>
/// Device-to-desktop task 10: migrates the device fixtures to a temp folder and builds the result with the .NET SDK against MonoGame.
/// Needs NuGet, so it runs only when <c>XNAREBORN_INTEGRATION=1</c>. The migrated games are built, never run.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DeviceIntegrationTests(ITestOutputHelper testOutput) : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-device-it-" + Guid.NewGuid().ToString("N"));

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
                // A build server may still hold a file. The folder is in the temp directory.
            }
        }
    }

    [IntegrationFact]
    public async Task Migrated_phone_fixture_builds_without_device_symbols_or_phone_references()
    {
        var original = FixturePaths.Of("device-wp7-xna");
        var before = MigrationTestSupport.Tree(original);
        var output = Path.Combine(_temp, "phone");

        var result = await MigrationTestSupport.MigrateAsync(original, MigrationTestSupport.Options(output));
        Assert.True(result.Completed);

        var (exitCode, text) = await MigrationIntegrationTests.RunBuildAsync(output);
        testOutput.WriteLine(text);

        Assert.True(exitCode == 0, $"dotnet build failed with exit code {exitCode}.{Environment.NewLine}{text}");
        var game = await File.ReadAllTextAsync(Path.Combine(output, "Game1.cs"));
        Assert.DoesNotContain("WINDOWS_PHONE", game, StringComparison.Ordinal);
        Assert.DoesNotContain("#if", game, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Phone", await File.ReadAllTextAsync(Path.Combine(output, "Wp7Xna.csproj")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(output, "DEVICE-CONTROLS.md")));
        Assert.Equal(before, MigrationTestSupport.Tree(original));
    }

    [IntegrationFact]
    public async Task Migrated_copy_solution_builds_only_the_windows_project()
    {
        var output = Path.Combine(_temp, "copy");

        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("device-xbox360-copy-solution"), MigrationTestSupport.Options(output));
        Assert.True(result.Completed);

        var (exitCode, text) = await MigrationIntegrationTests.RunBuildAsync(Path.Combine(output, "Game"));
        testOutput.WriteLine(text);

        Assert.True(exitCode == 0, $"dotnet build failed with exit code {exitCode}.{Environment.NewLine}{text}");
        Assert.False(Directory.Exists(Path.Combine(output, "Xbox 360 Copy of Game")));
    }

    [IntegrationFact]
    public async Task Two_migrations_of_the_same_fixture_give_the_same_output()
    {
        var first = Path.Combine(_temp, "one");
        var second = Path.Combine(_temp, "two");

        await MigrationTestSupport.MigrateAsync(FixturePaths.Of("device-wp7-xna"), MigrationTestSupport.Options(first));
        await MigrationTestSupport.MigrateAsync(FixturePaths.Of("device-wp7-xna"), MigrationTestSupport.Options(second));

        Assert.Equal(MigrationTestSupport.Tree(first), MigrationTestSupport.Tree(second));
    }
}
