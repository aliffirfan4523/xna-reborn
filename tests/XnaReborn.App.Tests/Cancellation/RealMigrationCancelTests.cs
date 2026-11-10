using System.Security.Cryptography;
using XnaReborn.App.Services;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;

namespace XnaReborn.App.Tests.Cancellation;

/// <summary>
/// Studio UI task 9.2: cancelling the real migrator, as the Migrate button does, never leaves a half-written output folder
/// without a marker. These tests use the real <see cref="MigrationService"/> on a fixture and a temp folder, with no fakes.
/// </summary>
public sealed class RealMigrationCancelTests
{
    private static readonly string _source = RepoPaths.Fixture("xna31-rewritable");

    [Fact]
    public async Task Cancelling_while_files_are_written_leaves_no_output_folder_and_a_marked_staging_folder()
    {
        using var temp = new TempFolder();
        var before = HashTree(_source);

        await CancelAtWritingAsync(Path.Combine(temp.Path, "out"), confirmedNonEmpty: false);

        Assert.False(Directory.Exists(Path.Combine(temp.Path, "out")));
        var staging = Assert.Single(Directory.GetDirectories(temp.Path));
        Assert.EndsWith(".xnareborn-incomplete", staging, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(staging, "INCOMPLETE.txt")));
        Assert.Equal(before, HashTree(_source));
    }

    [Fact]
    public async Task Cancelling_leaves_an_existing_output_folder_exactly_as_it_was()
    {
        using var temp = new TempFolder();
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "keep.txt"), "keep", TestContext.Current.CancellationToken);

        await CancelAtWritingAsync(output, confirmedNonEmpty: true);

        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(output, "keep.txt"), TestContext.Current.CancellationToken));
        Assert.Single(Directory.GetFileSystemEntries(output));
        Assert.DoesNotContain(Directory.GetDirectories(temp.Path), d => d.Contains(".backup-", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(Assert.Single(Directory.GetDirectories(temp.Path), d => d.EndsWith(".xnareborn-incomplete", StringComparison.Ordinal)), "INCOMPLETE.txt")));
    }

    private static async Task CancelAtWritingAsync(string output, bool confirmedNonEmpty)
    {
        var report = await new AnalysisService().AnalyzeAsync(_source, null, TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        var options = new MigrationOptions(output, MonoGameTarget.DesktopGL, false, false, new HashSet<string>());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new MigrationService().MigrateAsync(_source, report, options, confirmedNonEmpty, new CancelAtStage("Writing files", cts), cts.Token));
    }

    private static string HashTree(string folder) =>
        string.Join(
            "\n",
            Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => Path.GetRelativePath(folder, f) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)))));

    /// <summary>Cancels as soon as the migrator reports the stage, on the migrator's own thread, so the timing does not vary.</summary>
    private sealed class CancelAtStage(string stage, CancellationTokenSource cts) : IProgress<MigrationProgress>
    {
        public void Report(MigrationProgress value)
        {
            if (value.Stage == stage)
            {
                cts.Cancel();
            }
        }
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xnareborn-cancel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
