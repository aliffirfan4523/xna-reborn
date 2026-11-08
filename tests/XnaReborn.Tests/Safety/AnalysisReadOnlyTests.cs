using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reporting;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Reporting;

namespace XnaReborn.Tests.Safety;

/// <summary>Task 9.1: analysis leaves the analyzed project exactly as it was.</summary>
[Trait("Category", "Safety")]
public class AnalysisReadOnlyTests
{
    public static IEnumerable<object[]> NameRows() => FixturePaths.NameRows();

    private static string CatalogPath =>
        Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json"));

    [Theory]
    [MemberData(nameof(NameRows))]
    public async Task Analyzing_a_fixture_changes_no_file_and_adds_none(string name)
    {
        var folder = FixturePaths.Of(name);
        var before = FolderState.Capture(folder);

        await new ProjectAnalyzer().AnalyzeAsync(folder, SampleCatalog.Create(), null, CancellationToken.None);

        Assert.Equal(before, FolderState.Capture(folder));
    }

    [Fact]
    public async Task Analyzing_the_whole_fixtures_folder_changes_nothing()
    {
        var before = FolderState.Capture(FixturePaths.Root);

        var report = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Root, SampleCatalog.Create(), null, CancellationToken.None);

        Assert.NotEmpty(report.Projects);
        Assert.Equal(before, FolderState.Capture(FixturePaths.Root));
    }

    [Fact]
    public async Task Analyzing_with_the_real_catalog_changes_nothing_including_the_catalog()
    {
        var catalogBefore = FolderState.Capture(Path.GetDirectoryName(CatalogPath)!);
        var fixturesBefore = FolderState.Capture(FixturePaths.Root);
        var catalog = await RuleCatalogLoader.LoadAsync(CatalogPath, new ReadOnlyFileSystem(), CancellationToken.None);

        await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Root, catalog, null, CancellationToken.None);

        Assert.Equal(catalogBefore, FolderState.Capture(Path.GetDirectoryName(CatalogPath)!));
        Assert.Equal(fixturesBefore, FolderState.Capture(FixturePaths.Root));
    }

    [Theory]
    [MemberData(nameof(NameRows))]
    public async Task Analysis_works_on_a_copy_whose_files_are_all_marked_read_only(string name)
    {
        var copy = Path.Combine(Path.GetTempPath(), "xnareborn-ro-" + Guid.NewGuid().ToString("N"));
        CopyFolder(FixturePaths.Of(name), copy);
        try
        {
            foreach (var file in Directory.EnumerateFiles(copy, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.ReadOnly);
            }

            var before = FolderState.Capture(copy);
            var readOnly = await new ProjectAnalyzer().AnalyzeAsync(copy, SampleCatalog.Create(), null, CancellationToken.None);
            var normal = await new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of(name), SampleCatalog.Create(), null, CancellationToken.None);

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
    public void Folder_capture_notices_a_changed_added_or_removed_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), "xnareborn-capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder, "a.txt");
            File.WriteAllText(file, "one");
            var original = FolderState.Capture(folder);

            File.WriteAllText(file, "two");
            Assert.NotEqual(original, FolderState.Capture(folder));

            File.WriteAllText(file, "one");
            File.WriteAllText(Path.Combine(folder, "b.txt"), "x");
            Assert.NotEqual(original, FolderState.Capture(folder));

            File.Delete(Path.Combine(folder, "b.txt"));
            Directory.CreateDirectory(Path.Combine(folder, "sub"));
            Assert.NotEqual(original, FolderState.Capture(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
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
