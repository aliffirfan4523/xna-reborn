using XnaReborn.Migrator.Modern;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Publish;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Migrator;
using Xunit.Abstractions;

namespace XnaReborn.Tests.Runner;

/// <summary>
/// Migrates a fixture (with the window extras on, so the helper is compiled against real MonoGame) and runs a real
/// <c>dotnet publish</c> for win-x64 and linux-x64. Needs the network for NuGet and runtime packs, so it runs only when
/// <c>XNAREBORN_INTEGRATION=1</c>. It checks that files exist; it never starts the published program.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Runner")]
public sealed class PublishIntegrationTests(ITestOutputHelper testOutput) : IDisposable
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(20);

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-publish-it-" + Guid.NewGuid().ToString("N"));

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
    public async Task Migrated_fixture_with_extras_publishes_as_one_file_for_win_x64()
    {
        var copy = await MigrateAsync();

        var result = await PublishAsync(copy, "win-x64");

        Assert.True(result.Succeeded, result.Message);
        Assert.True(File.Exists(result.Program), result.Program);
        Assert.EndsWith("Xna31Rewritable.exe", result.Program, StringComparison.Ordinal);
        Assert.Contains("Xna31Rewritable.exe", result.Files);

        // Single file: the game and MonoGame are inside the .exe, not beside it.
        Assert.DoesNotContain("MonoGame.Framework.dll", result.Files);
        Assert.DoesNotContain("Xna31Rewritable.dll", result.Files);

        // The original fixture and the migrated copy hold no publish output.
        Assert.False(Directory.Exists(Path.Combine(FixturePaths.Of("xna31-rewritable"), "bin")));
        Assert.StartsWith("..", Path.GetRelativePath(copy, result.OutputFolder!), StringComparison.Ordinal);
    }

    [IntegrationTheory]
    [InlineData("linux-x64")]
    [InlineData("osx-x64")]
    [InlineData("osx-arm64")]
    public async Task Migrated_fixture_publishes_for_linux_and_macos_from_windows(string rid)
    {
        var copy = await MigrateAsync();

        var result = await PublishAsync(copy, rid);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(File.Exists(result.Program), result.Program);
        Assert.EndsWith(Path.Combine(rid, "Xna31Rewritable"), result.Program, StringComparison.Ordinal);
        Assert.DoesNotContain("Xna31Rewritable.exe", result.Files);
    }

    [IntegrationFact]
    public async Task A_second_publish_into_the_same_folder_waits_for_confirmation()
    {
        var copy = await MigrateAsync();
        var first = await PublishAsync(copy, "win-x64");
        Assert.True(first.Succeeded, first.Message);

        var second = await new ProjectPublisher().PublishAsync(copy, "win-x64", false, null, _timeout, CancellationToken.None);

        Assert.Equal(PublishStatus.NeedsConfirmation, second.Status);
        Assert.True(File.Exists(first.Program));
    }

    private async Task<string> MigrateAsync()
    {
        var output = Path.Combine(_temp, "Xna31Rewritable-monogame");
        var options = MigrationTestSupport.Options(output) with { AllowWindowResize = true, FullscreenToggle = true, HighDpi = true };
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna31-rewritable"), options);
        Assert.True(result.Completed);
        Assert.Contains(result.Changes, c => c.RelativePath == ModernExtrasGenerator.FileName);
        return output;
    }

    private Task<PublishResult> PublishAsync(string copy, string rid) =>
        new ProjectPublisher().PublishAsync(
            copy,
            rid,
            overwriteConfirmed: false,
            new SyncProgress<OutputLine>(l => testOutput.WriteLine(l.Text)),
            _timeout,
            CancellationToken.None);
}
