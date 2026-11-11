using XnaReborn.App.Services;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;

namespace XnaReborn.App.Tests;

public sealed class FlashServiceTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xnareborn-appflash-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private static string FlashFixture(string name) => Path.Combine(RepoPaths.Fixture("flash"), name);

    [Fact]
    public async Task A_flash_folder_is_analyzed_into_the_shared_report_shape()
    {
        var report = await new AnalysisService().AnalyzeAsync(FlashFixture("flx-platformer"), null, TestContext.Current.CancellationToken);

        Assert.Equal("flash-to-monogame", report.CatalogName);
        var project = Assert.Single(report.Projects);
        Assert.Equal("PlatformerGame", project.AssemblyName);
        Assert.Contains(report.Findings, f => f.RuleId == "FL027");
        Assert.NotEmpty(report.TriggeredRules);
        Assert.InRange(report.Score, 0, 100);
    }

    [Fact]
    public async Task An_xna_folder_still_goes_through_the_xna_analyzer()
    {
        var report = await new AnalysisService().AnalyzeAsync(RepoPaths.Fixture("xna40-basic"), null, TestContext.Current.CancellationToken);
        Assert.NotEqual("flash-to-monogame", report.CatalogName);
    }

    [Fact]
    public async Task A_flash_project_migrates_through_the_migration_service()
    {
        var analysis = new AnalysisService();
        var migration = new MigrationService();
        var source = FlashFixture("flx-hello");
        var report = await analysis.AnalyzeAsync(source, null, TestContext.Current.CancellationToken);
        var plan = migration.CreatePlan(report, new HashSet<string>());
        Assert.NotEmpty(plan.Items);

        var output = Path.Combine(_temp, "out");
        var options = new MigrationOptions(output, MonoGameTarget.DesktopGL, false, false, new HashSet<string>());
        var stages = new List<string>();
        var result = await migration.MigrateAsync(source, report, options, false, new SyncProgress<MigrationProgress>(p => stages.Add(p.Stage)), TestContext.Current.CancellationToken);

        Assert.True(result.Completed);
        Assert.True(File.Exists(Path.Combine(output, "HelloGame.csproj")));
        Assert.Contains("Done", stages);
        Assert.Contains(result.Manifest.Changes, c => c.Path == "Program.cs");
    }

    [Fact]
    public async Task A_dry_run_writes_nothing()
    {
        var source = FlashFixture("flx-hello");
        var report = await new AnalysisService().AnalyzeAsync(source, null, TestContext.Current.CancellationToken);
        var output = Path.Combine(_temp, "dry");
        var options = new MigrationOptions(output, MonoGameTarget.DesktopGL, false, true, new HashSet<string>());

        var result = await new MigrationService().MigrateAsync(source, report, options, false, null, TestContext.Current.CancellationToken);

        Assert.False(result.Completed);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task An_existing_output_folder_with_files_is_refused()
    {
        var source = FlashFixture("flx-hello");
        var report = await new AnalysisService().AnalyzeAsync(source, null, TestContext.Current.CancellationToken);
        var output = Path.Combine(_temp, "busy");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "x.txt"), "x");
        var options = new MigrationOptions(output, MonoGameTarget.DesktopGL, false, false, new HashSet<string>());

        await Assert.ThrowsAsync<OutputGuardException>(() => new MigrationService().MigrateAsync(source, report, options, false, null, TestContext.Current.CancellationToken));
    }
}
