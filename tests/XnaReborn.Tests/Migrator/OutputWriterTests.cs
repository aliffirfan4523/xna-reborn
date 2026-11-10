using System.Security.Cryptography;
using System.Text;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Tests.Migrator;

/// <summary>Task 8.1: the output writer stays inside the staging folder and never touches the source.</summary>
[Trait("Category", "Migrator")]
[Trait("Category", "Safety")]
public sealed class OutputWriterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xr-writer-" + Guid.NewGuid().ToString("N"));
    private readonly string _source;
    private readonly OutputSession _session;
    private readonly OutputWriter _writer = new();
    private readonly string _before;

    public OutputWriterTests()
    {
        _source = Path.Combine(_root, "src");
        Directory.CreateDirectory(Path.Combine(_source, "Assets"));
        File.WriteAllBytes(Path.Combine(_source, "Assets", "logo.bin"), [0, 1, 2, 255, 254, 13, 10]);
        File.WriteAllText(Path.Combine(_source, "Game.cs"), "class G {}");
        _session = new OutputGuard().Begin(_source, Path.Combine(_root, "out"), false);
        _before = Hashes(_source);
    }

    public void Dispose()
    {
        _session.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Staging => _session.StagingFolder;

    private static string Hashes(string folder) =>
        string.Join("\n", Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.AllDirectories)
            .Select(e => Path.GetRelativePath(folder, e) + (File.Exists(e) ? ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(e))) : "/"))
            .Order(StringComparer.Ordinal));

    private static FileChange Text(string path, string text, ChangeKind kind = ChangeKind.Created, string? original = null) =>
        new(path, kind, original, text, "r");

    private Task<WriteSummary> Run(params FileChange[] changes) =>
        _writer.WriteAsync(_session, _source, changes, CancellationToken.None);

    private string Src(string rel) => Path.Combine(_source, rel);

    [Fact]
    public async Task Writes_created_modified_copied_manual_and_skipped()
    {
        var summary = await Run(
            Text("New.cs", "new"),
            Text("Mod.cs", "mod", ChangeKind.Modified, "old"),
            new FileChange("Assets/logo.bin", ChangeKind.Copied, null, null, null, Src("Assets/logo.bin")),
            new FileChange("Game.cs", ChangeKind.ManualWork, null, null, "look", Src("Game.cs")),
            new FileChange("Manual.cs", ChangeKind.ManualWork, "a", "b", "look"),
            new FileChange("Gone.cs", ChangeKind.Skipped, null, null, "x"));

        Assert.Equal(3, summary.FilesWritten);
        Assert.Equal(2, summary.FilesCopied);
        Assert.Equal(1, summary.FilesSkipped);
        Assert.Equal(3 + 3 + 1 + 7 + 10, summary.BytesWritten);
        Assert.Equal("new", File.ReadAllText(Path.Combine(Staging, "New.cs")));
        Assert.Equal("mod", File.ReadAllText(Path.Combine(Staging, "Mod.cs")));
        Assert.Equal("b", File.ReadAllText(Path.Combine(Staging, "Manual.cs")));
        Assert.Equal("class G {}", File.ReadAllText(Path.Combine(Staging, "Game.cs")));
        Assert.Equal(File.ReadAllBytes(Src("Assets/logo.bin")), File.ReadAllBytes(Path.Combine(Staging, "Assets", "logo.bin")));
        Assert.False(File.Exists(Path.Combine(Staging, "Gone.cs")));
    }

    [Fact]
    public async Task Keeps_bom_only_when_original_had_one()
    {
        await Run(
            Text("Keep.cs", "x", ChangeKind.Modified, "\uFEFFold"),
            Text("Drop.cs", "x", ChangeKind.Modified, "old"),
            Text("Created.cs", "\uFEFFx", ChangeKind.Created),
            Text("NoOriginal.cs", "x", ChangeKind.Modified));

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'x' }, File.ReadAllBytes(Path.Combine(Staging, "Keep.cs")));
        Assert.Equal(new[] { (byte)'x' }, File.ReadAllBytes(Path.Combine(Staging, "Drop.cs")));
        Assert.Equal(new[] { (byte)'x' }, File.ReadAllBytes(Path.Combine(Staging, "Created.cs")));
        Assert.Equal(new[] { (byte)'x' }, File.ReadAllBytes(Path.Combine(Staging, "NoOriginal.cs")));
    }

    [Fact]
    public async Task Keeps_line_endings_and_writes_utf8()
    {
        const string text = "a\r\nb\nc\r\nü€";
        await Run(Text("Le.cs", text));
        Assert.Equal(Encoding.UTF8.GetBytes(text), File.ReadAllBytes(Path.Combine(Staging, "Le.cs")));
    }

    [Fact]
    public async Task Creates_nested_folders_and_accepts_both_separators()
    {
        await Run(Text("a/b/c.cs", "1"), Text("d\\e.cs", "2"));
        Assert.Equal("1", File.ReadAllText(Path.Combine(Staging, "a", "b", "c.cs")));
        Assert.Equal("2", File.ReadAllText(Path.Combine(Staging, "d", "e.cs")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("/abs.cs")]
    [InlineData("\\abs.cs")]
    [InlineData("C:\\abs.cs")]
    [InlineData("..")]
    [InlineData("../evil.cs")]
    [InlineData("a/../../evil.cs")]
    [InlineData("a\\..\\..\\evil.cs")]
    [InlineData("a/./b.cs")]
    [InlineData("a//b.cs")]
    [InlineData("a/")]
    [InlineData("a:stream")]
    [InlineData("a/b?.cs")]
    public async Task Invalid_paths_write_nothing(string bad)
    {
        var stagingBefore = Hashes(Staging);
        var rootBefore = Hashes(_root);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Run(Text("Good.cs", "g"), Text(bad, "x")));

        if (bad.Trim().Length > 0)
        {
            Assert.Contains(bad, ex.Message);
        }

        Assert.Equal(stagingBefore, Hashes(Staging));
        Assert.Equal(rootBefore, Hashes(_root));
    }

    [Fact]
    public async Task Duplicate_paths_are_rejected_ignoring_case_and_separator()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Run(Text("A/B.cs", "1"), Text("a\\b.CS", "2")));
        Assert.False(Directory.Exists(Path.Combine(Staging, "A")));
    }

    [Fact]
    public async Task File_and_folder_collisions_are_rejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Run(Text("a", "1"), Text("a/b.cs", "2")));
        Assert.False(File.Exists(Path.Combine(Staging, "a")));
    }

    [Fact]
    public async Task Source_path_outside_source_root_is_refused_and_nothing_is_written()
    {
        var outside = Path.Combine(_root, "outside.txt");
        File.WriteAllText(outside, "secret");
        var sibling = _source + "-evil";
        Directory.CreateDirectory(sibling);
        File.WriteAllText(Path.Combine(sibling, "f.txt"), "x");

        foreach (var path in new[] { outside, Path.Combine(sibling, "f.txt"), Path.Combine(_source, "..", "outside.txt") })
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => Run(Text("Good.cs", "g"), new FileChange("Stolen.txt", ChangeKind.Copied, null, null, null, path)));
            Assert.Contains("Stolen.txt", ex.Message);
        }

        Assert.False(File.Exists(Path.Combine(Staging, "Good.cs")));
        Assert.False(File.Exists(Path.Combine(Staging, "Stolen.txt")));
    }

    [Fact]
    public async Task Missing_source_file_or_source_path_is_refused()
    {
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Run(Text("Good.cs", "g"), new FileChange("Nope.bin", ChangeKind.Copied, null, null, null, Src("nope.bin"))));
        Assert.Contains("Nope.bin", missing.Message);

        var none = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Run(new FileChange("NoSrc.bin", ChangeKind.ManualWork, null, null, "r")));
        Assert.Contains("NoSrc.bin", none.Message);

        var folder = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Run(new FileChange("Dir.bin", ChangeKind.Copied, null, null, null, Src("Assets"))));
        Assert.Contains("Dir.bin", folder.Message);

        Assert.False(File.Exists(Path.Combine(Staging, "Good.cs")));
    }

    [Fact]
    public async Task Created_or_modified_without_text_is_refused()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Run(new FileChange("Empty.cs", ChangeKind.Created, null, null, "r")));
        Assert.Contains("Empty.cs", ex.Message);
    }

    [Fact]
    public async Task Cancellation_stops_between_files()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _writer.WriteAsync(_session, _source, [Text("A.cs", "1"), Text("B.cs", "2")], cts.Token));
        Assert.False(File.Exists(Path.Combine(Staging, "A.cs")));
        Assert.False(File.Exists(Path.Combine(Staging, "B.cs")));
    }

    [Fact]
    public async Task Source_tree_is_unchanged_and_nothing_appears_outside_staging()
    {
        var rootBefore = Hashes(_root);

        await Run(
            Text("New.cs", "n"),
            Text("deep/er/Mod.cs", "m", ChangeKind.Modified, "\uFEFFo"),
            new FileChange("Assets/logo.bin", ChangeKind.Copied, null, null, null, Src("Assets/logo.bin")),
            new FileChange("Game.cs", ChangeKind.ManualWork, null, null, "r", Src("Game.cs")));

        Assert.Equal(_before, Hashes(_source));

        var after = Hashes(_root).Split('\n');
        var added = after.Except(rootBefore.Split('\n')).ToArray();
        var stagingName = Path.GetFileName(Staging);
        Assert.NotEmpty(added);
        Assert.All(added, line => Assert.StartsWith(stagingName + Path.DirectorySeparatorChar, line, StringComparison.Ordinal));
        Assert.Equal(
            rootBefore.Split('\n').Where(l => !l.StartsWith(stagingName, StringComparison.Ordinal)).Order(StringComparer.Ordinal),
            after.Where(l => !l.StartsWith(stagingName, StringComparison.Ordinal)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Empty_change_list_writes_nothing()
    {
        var summary = await Run();
        Assert.Equal(new WriteSummary(0, 0, 0, 0), summary);
    }
}
