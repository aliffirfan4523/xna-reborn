using XnaReborn.Analyzer.Discovery;
using XnaReborn.Analyzer.IO;

namespace XnaReborn.Tests.Discovery;

/// <summary>Discovery keeps going when a subfolder cannot be read.</summary>
public sealed class UnreadableFolderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xnareborn-unreadable-" + Guid.NewGuid().ToString("N"));

    public UnreadableFolderTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "open"));
        Directory.CreateDirectory(Path.Combine(_root, "locked"));
        File.WriteAllText(Path.Combine(_root, "open", "Open.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_root, "locked", "Locked.csproj"), "<Project />");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Unreadable_subfolder_gives_a_diagnostic_and_other_projects_are_still_found()
    {
        var lockedPath = Path.Combine(_root, "locked");
        var fs = new ThrowingFileSystem(new ReadOnlyFileSystem(), lockedPath, new UnauthorizedAccessException("Access denied."));

        var result = await new ProjectDiscovery(fs).DiscoverAsync(_root, CancellationToken.None);

        var project = Assert.Single(result.Projects);
        Assert.Equal("open/Open.csproj", project.RelativePath);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ANA007", diagnostic.Code);
        Assert.Equal("locked", diagnostic.File);
        Assert.Contains("Access denied.", diagnostic.Message);
    }

    [Fact]
    public async Task Folder_removed_during_the_scan_gives_a_diagnostic_instead_of_an_exception()
    {
        var lockedPath = Path.Combine(_root, "locked");
        var fs = new ThrowingFileSystem(new ReadOnlyFileSystem(), lockedPath, new DirectoryNotFoundException("Gone."));

        var result = await new ProjectDiscovery(fs).DiscoverAsync(_root, CancellationToken.None);

        Assert.Equal("ANA007", Assert.Single(result.Diagnostics).Code);
        Assert.Single(result.Projects);
    }

    [Fact]
    public async Task Unreadable_root_folder_gives_a_diagnostic_and_a_no_project_reason()
    {
        var fs = new ThrowingFileSystem(new ReadOnlyFileSystem(), _root, new UnauthorizedAccessException("Access denied."));

        var result = await new ProjectDiscovery(fs).DiscoverAsync(_root, CancellationToken.None);

        Assert.Empty(result.Projects);
        Assert.Equal("ANA007", Assert.Single(result.Diagnostics).Code);
        Assert.NotNull(result.NoProjectReason);
    }

    private sealed class ThrowingFileSystem(IReadOnlyFileSystem inner, string failingDirectory, Exception failure) : IReadOnlyFileSystem
    {
        public bool FileExists(string path) => inner.FileExists(path);

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);

        public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern) =>
            Check(directory, () => inner.EnumerateFiles(directory, searchPattern));

        public IReadOnlyList<string> EnumerateDirectories(string directory) =>
            Check(directory, () => inner.EnumerateDirectories(directory));

        public Stream OpenRead(string path) => inner.OpenRead(path);

        public string ReadAllText(string path) => inner.ReadAllText(path);

        private T Check<T>(string directory, Func<T> read) =>
            string.Equals(directory, failingDirectory, StringComparison.Ordinal) ? throw failure : read();
    }
}
