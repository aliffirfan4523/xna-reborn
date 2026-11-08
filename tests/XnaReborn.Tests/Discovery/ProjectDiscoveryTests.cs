using XnaReborn.Analyzer.Discovery;
using XnaReborn.Analyzer.IO;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Discovery;

/// <summary>Checks that project discovery finds the right projects and reports clear reasons otherwise.</summary>
public sealed class ProjectDiscoveryTests : IDisposable
{
    private const string _xnaProject = "<Project><PropertyGroup><XnaFrameworkVersion>v4.0</XnaFrameworkVersion></PropertyGroup></Project>";

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xna-disc-" + Guid.NewGuid().ToString("N"));

    /// <summary>Deletes the temporary folder used by a test.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private static Task<DiscoveryResult> RunAsync(string path, CancellationToken ct = default) =>
        new ProjectDiscovery(new ReadOnlyFileSystem()).DiscoverAsync(path, ct);

    private void Write(string relative, string content)
    {
        var full = Path.Combine(_temp, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    /// <summary>Folder mode on xna40-basic finds one XNA project.</summary>
    [Fact]
    public async Task Folder_Xna40Basic_FindsOne_xnaProject()
    {
        var r = await RunAsync(FixturePaths.Of("xna40-basic"));
        var p = Assert.Single(r.Projects);
        Assert.Equal("Xna40Basic.csproj", p.RelativePath);
        Assert.True(p.IsXna);
        Assert.Null(r.NoProjectReason);
        Assert.Empty(r.Diagnostics);
    }

    /// <summary>Folder mode on xna31-legacy finds one XNA project.</summary>
    [Fact]
    public async Task Folder_Xna31Legacy_FindsOne_xnaProject()
    {
        var r = await RunAsync(FixturePaths.Of("xna31-legacy"));
        var p = Assert.Single(r.Projects);
        Assert.Equal("Xna31Legacy.csproj", p.RelativePath);
        Assert.True(p.IsXna);
        Assert.Null(r.NoProjectReason);
        Assert.Empty(r.Diagnostics);
    }

    /// <summary>Folder mode on xna40-gamerservices finds one XNA project.</summary>
    [Fact]
    public async Task Folder_Xna40GamerServices_FindsOne_xnaProject()
    {
        var r = await RunAsync(FixturePaths.Of("xna40-gamerservices"));
        var p = Assert.Single(r.Projects);
        Assert.Equal("Xna40GamerServices.csproj", p.RelativePath);
        Assert.True(p.IsXna);
        Assert.Null(r.NoProjectReason);
        Assert.Empty(r.Diagnostics);
    }

    /// <summary>Folder mode on xna40-custom-content finds one XNA project.</summary>
    [Fact]
    public async Task Folder_Xna40CustomContent_FindsOne_xnaProject()
    {
        var r = await RunAsync(FixturePaths.Of("xna40-custom-content"));
        var p = Assert.Single(r.Projects);
        Assert.Equal("CustomContent.csproj", p.RelativePath);
        Assert.True(p.IsXna);
        Assert.Null(r.NoProjectReason);
        Assert.Empty(r.Diagnostics);
    }

    /// <summary>Folder mode on multi-project-sln finds two projects and reports the missing one.</summary>
    [Fact]
    public async Task Folder_MultiProjectSln_FindsTwoProjectsAndReportsMissingOne()
    {
        var r = await RunAsync(FixturePaths.Of("multi-project-sln"));
        AssertMultiProject(r);
    }

    /// <summary>Passing the .sln path gives the same projects as folder mode.</summary>
    [Fact]
    public async Task SolutionPath_MultiProjectSln_FindsTwoProjectsAndReportsMissingOne()
    {
        var r = await RunAsync(Path.Combine(FixturePaths.Of("multi-project-sln"), "MultiProject.sln"));
        AssertMultiProject(r);
    }

    /// <summary>Folder mode on not-xna lists the project and says none use XNA.</summary>
    [Fact]
    public async Task Folder_NotXna_ListsProjectWithNoXnaReason()
    {
        var r = await RunAsync(FixturePaths.Of("not-xna"));
        var p = Assert.Single(r.Projects);
        Assert.Equal("NotXna.csproj", p.RelativePath);
        Assert.False(p.IsXna);
        Assert.Equal("Found 1 project(s), but none use XNA.", r.NoProjectReason);
        Assert.Empty(r.Diagnostics);
    }

    /// <summary>Folder mode on empty-folder finds nothing.</summary>
    [Fact]
    public async Task Folder_EmptyFolder_FindsNoProjects()
    {
        var r = await RunAsync(FixturePaths.Of("empty-folder"));
        Assert.Empty(r.Projects);
        Assert.Equal("No .csproj files found.", r.NoProjectReason);
        Assert.Empty(r.Diagnostics);
    }

    /// <summary>A .csproj path gives only that project, relative to its folder.</summary>
    [Fact]
    public async Task ProjectPath_Xna40Basic_FindsThatProjectOnly()
    {
        var r = await RunAsync(Path.Combine(FixturePaths.Of("xna40-basic"), "Xna40Basic.csproj"));
        var p = Assert.Single(r.Projects);
        Assert.Equal("Xna40Basic.csproj", p.RelativePath);
        Assert.True(p.IsXna);
        Assert.Null(r.NoProjectReason);
    }

    /// <summary>A path that does not exist gives a 'Path not found' reason without throwing.</summary>
    [Fact]
    public async Task MissingPath_ReturnsPathNotFoundReason()
    {
        var missing = Path.Combine(_temp, "nope");
        var r = await RunAsync(missing);
        Assert.Empty(r.Projects);
        Assert.Equal("Path not found: " + missing, r.NoProjectReason);
    }

    /// <summary>A .txt file gives an 'Unsupported file type' reason.</summary>
    [Fact]
    public async Task TextFilePath_ReturnsUnsupportedFileTypeReason()
    {
        Write("notes.txt", "hello");
        var r = await RunAsync(Path.Combine(_temp, "notes.txt"));
        Assert.Empty(r.Projects);
        Assert.Equal("Unsupported file type '.txt'. Open a folder, a .sln, or a .csproj.", r.NoProjectReason);
    }

    /// <summary>Folders named bin, obj, .git and .vs are not searched.</summary>
    [Fact]
    public async Task Folder_SkipsBinObjGitAndVsDirectories()
    {
        Write("real.csproj", _xnaProject);
        foreach (var dir in new[] { "bin", "obj", ".git", ".vs" })
        {
            Write(Path.Combine(dir, "x.csproj"), _xnaProject);
        }

        var r = await RunAsync(_temp);
        var p = Assert.Single(r.Projects);
        Assert.Equal("real.csproj", p.RelativePath);
    }

    /// <summary>A .csproj that is not valid XML is listed as non-XNA with ANA001.</summary>
    [Fact]
    public async Task Folder_MalformedCsproj_ReportsAna001AndIsNotXna()
    {
        Write("bad.csproj", "<Project><Unclosed>");
        var r = await RunAsync(_temp);
        var p = Assert.Single(r.Projects);
        Assert.False(p.IsXna);
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal("ANA001", d.Code);
        Assert.Equal("bad.csproj", d.File);
        Assert.StartsWith("Project file could not be read: ", d.Message, StringComparison.Ordinal);
    }

    /// <summary>Projects come back sorted by relative path.</summary>
    [Fact]
    public async Task Folder_ReturnsProjectsInSortedOrder()
    {
        Write(Path.Combine("b", "B.csproj"), _xnaProject);
        Write(Path.Combine("a", "A.csproj"), _xnaProject);
        Write("C.csproj", _xnaProject);
        var r = await RunAsync(_temp);
        Assert.Equal(["a/A.csproj", "b/B.csproj", "C.csproj"], r.Projects.Select(p => p.RelativePath).ToArray());
    }

    /// <summary>A cancelled token makes discovery throw OperationCanceledException.</summary>
    [Fact]
    public async Task CancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(FixturePaths.Of("xna40-basic"), cts.Token));
    }

    private static void AssertMultiProject(DiscoveryResult r)
    {
        Assert.Equal(["Game/Game.csproj", "Library/Library.csproj"], r.Projects.Select(p => p.RelativePath).ToArray());
        Assert.All(r.Projects, p => Assert.True(p.IsXna));
        Assert.Null(r.NoProjectReason);
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal("ANA006", d.Code);
        Assert.Equal("Missing/Missing.csproj", d.File);
    }
}
