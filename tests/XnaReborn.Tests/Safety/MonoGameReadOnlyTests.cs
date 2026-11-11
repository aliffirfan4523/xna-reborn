using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reporting;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Safety;

/// <summary>Task 10: analyzing the MonoGame upgrade fixtures leaves them exactly as they were, and the new code has no write APIs.</summary>
[Trait("Category", "Safety")]
public sealed class MonoGameReadOnlyTests
{
    public static IEnumerable<object[]> NameRows() => FixturePaths.MonoGameNameRows();

    private static string RepoRoot => Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", ".."));

    private static async Task<IReadOnlyList<RuleCatalog>> LoadCatalogsAsync() =>
        await RuleCatalogLoader.LoadAllAsync(
            [Path.Combine(RepoRoot, "rules", "xna-to-monogame.json"), Path.Combine(RepoRoot, "rules", "monogame-upgrade.json")],
            new ReadOnlyFileSystem(),
            CancellationToken.None);

    [Theory]
    [MemberData(nameof(NameRows))]
    public async Task Analyzing_a_fixture_changes_no_file_and_adds_none(string name)
    {
        var folder = FixturePaths.Of(name);
        var before = FolderState.Capture(folder);

        var report = await new ProjectAnalyzer().AnalyzeAsync(folder, await LoadCatalogsAsync(), null, CancellationToken.None);

        Assert.Equal(before, FolderState.Capture(folder));
        Assert.NotNull(report);
    }

    [Fact]
    public async Task Analyzing_the_whole_fixtures_folder_with_both_catalogs_changes_nothing()
    {
        var before = FolderState.Capture(FixturePaths.Root);
        var catalogs = await LoadCatalogsAsync();

        var report = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Root, catalogs, null, CancellationToken.None);

        Assert.NotEmpty(report.Projects);
        Assert.Equal(before, FolderState.Capture(FixturePaths.Root));
    }

    [Fact]
    public async Task Analyzing_with_the_catalogs_changes_neither_catalog_file()
    {
        var before = FolderState.Capture(Path.Combine(RepoRoot, "rules"));

        await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of("mg37-desktopgl-installdir"), await LoadCatalogsAsync(), null, CancellationToken.None);

        Assert.Equal(before, FolderState.Capture(Path.Combine(RepoRoot, "rules")));
    }

    [Theory]
    [MemberData(nameof(NameRows))]
    public async Task Analysis_works_on_a_copy_whose_files_are_all_marked_read_only(string name)
    {
        var copy = Path.Combine(Path.GetTempPath(), "xnareborn-mgro-" + Guid.NewGuid().ToString("N"));
        CopyFolder(FixturePaths.Of(name), copy);
        try
        {
            foreach (var file in Directory.EnumerateFiles(copy, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.ReadOnly);
            }

            var catalogs = await LoadCatalogsAsync();
            var before = FolderState.Capture(copy);
            var readOnly = await new ProjectAnalyzer().AnalyzeAsync(copy, catalogs, null, CancellationToken.None);
            var normal = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of(name), catalogs, null, CancellationToken.None);

            Assert.Equal(before, FolderState.Capture(copy));
            Assert.Equal(JsonReportWriter.Write(normal), JsonReportWriter.Write(readOnly));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(copy, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(copy, recursive: true);
        }
    }

    [Fact]
    public void The_protobuild_placeholder_is_never_executed_and_the_analyzer_has_no_process_api()
    {
        // The write-API scan flags Process.Start, so a clean scan means nothing in the analyzer can run Protobuild.exe.
        var files = Directory.EnumerateFiles(Path.Combine(RepoRoot, "src", "XnaReborn.Analyzer"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .ToList();

        Assert.DoesNotContain(files, f => File.ReadAllText(f).Contains("Process.Start", StringComparison.Ordinal) || File.ReadAllText(f).Contains("ProcessStartInfo", StringComparison.Ordinal));
        Assert.Contains(files, f => f.EndsWith("MgcbSettingMatcher.cs", StringComparison.Ordinal));
        Assert.Contains(files, f => f.EndsWith("PackagesConfigReader.cs", StringComparison.Ordinal));
        Assert.Contains(files, f => f.EndsWith("MonoGameVersionDetector.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void The_mgcb_parser_in_core_has_no_write_or_file_apis()
    {
        var files = Directory.EnumerateFiles(Path.Combine(RepoRoot, "src", "XnaReborn.Core", "Mgcb"), "*.cs").ToList();

        Assert.NotEmpty(files);
        var violations = files.SelectMany(f => WriteApiScanner.FindViolations(File.ReadAllText(f), Path.GetFileName(f))).ToList();
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
        Assert.DoesNotContain(files, f => File.ReadAllText(f).Contains("System.IO", StringComparison.Ordinal));
    }

    private static void CopyFolder(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }
    }
}
