using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Publish;

namespace XnaReborn.Tests.Runner;

/// <summary>Publish arguments, output folder, guards and result handling, with a fake process runner. No network and no dotnet.</summary>
[Trait("Category", "Runner")]
public sealed class ProjectPublisherTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xr-publish-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Targets_are_win_linux_and_both_mac_runtime_identifiers()
    {
        Assert.Equal(["win-x64", "linux-x64", "osx-x64", "osx-arm64"], PublishTarget.All.Select(t => t.Rid));
        Assert.Equal([true, false, false, false], PublishTarget.All.Select(t => t.IsWindows));
    }

    [Theory]
    [InlineData("linux-x64", true)]
    [InlineData("osx-arm64", true)]
    [InlineData("linux-x64 --no-restore", false)]
    [InlineData("LINUX-X64", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Find_matches_a_supported_rid_exactly(string? rid, bool found) => Assert.Equal(found, PublishTarget.Find(rid) is not null);

    [Fact]
    public void Arguments_ask_for_a_release_self_contained_single_file_publish()
    {
        var args = ProjectPublisher.BuildArguments("C:/copy/Game.csproj", "linux-x64", "C:/copy-publish/linux-x64");

        Assert.Equal(
            [
                "publish", "C:/copy/Game.csproj", "-c", "Release", "-r", "linux-x64", "--self-contained", "true",
                "-p:PublishSingleFile=true", "-o", "C:/copy-publish/linux-x64", "-nologo", "-v:minimal",
            ],
            args);
    }

    [Fact]
    public void Arguments_reject_empty_values()
    {
        Assert.Throws<ArgumentException>(() => ProjectPublisher.BuildArguments("", "linux-x64", "o"));
        Assert.Throws<ArgumentException>(() => ProjectPublisher.BuildArguments("p", "", "o"));
        Assert.Throws<ArgumentException>(() => ProjectPublisher.BuildArguments("p", "linux-x64", " "));
    }

    [Fact]
    public void Output_folder_is_a_sibling_of_the_migrated_copy_per_rid()
    {
        var copy = Path.Combine(_root, "Game-monogame");

        var folder = ProjectPublisher.OutputFolderFor(copy + Path.DirectorySeparatorChar, "osx-arm64");

        Assert.Equal(Path.Combine(_root, "Game-monogame-publish", "osx-arm64"), folder);
        Assert.False(folder.StartsWith(copy + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Output_folder_rejects_an_unknown_rid_and_a_drive_root()
    {
        Assert.Throws<ArgumentException>(() => ProjectPublisher.OutputFolderFor(Path.Combine(_root, "x"), "win-arm64"));
        Assert.Throws<ArgumentException>(() => ProjectPublisher.OutputFolderFor(Path.GetPathRoot(_root)!, "win-x64"));
    }

    [Fact]
    public async Task Unknown_rid_is_refused_and_nothing_runs()
    {
        var copy = MakeCopy(desktopGL: true);
        var fake = new FakeProcessRunner(_ => FakeProcessRunner.Ok());

        var result = await new ProjectPublisher(fake).PublishAsync(copy, "solaris-x64", false, null, null, CancellationToken.None);

        Assert.Equal(PublishStatus.Refused, result.Status);
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task A_folder_that_is_not_a_migrated_copy_is_refused_and_nothing_runs()
    {
        var folder = Path.Combine(_root, "plain");
        Directory.CreateDirectory(folder);
        var fake = new FakeProcessRunner(_ => FakeProcessRunner.Ok());

        var result = await new ProjectPublisher(fake).PublishAsync(folder, "win-x64", false, null, null, CancellationToken.None);

        Assert.Equal(PublishStatus.Refused, result.Status);
        Assert.Contains("xnareborn-manifest.json", result.Message, StringComparison.Ordinal);
        Assert.Empty(fake.Requests);
        Assert.False(Directory.Exists(Path.Combine(_root, "plain-publish")));
    }

    [Theory]
    [InlineData("linux-x64")]
    [InlineData("osx-x64")]
    [InlineData("osx-arm64")]
    public async Task A_WindowsDX_copy_is_refused_for_every_non_windows_rid(string rid)
    {
        var copy = MakeCopy(desktopGL: false);
        var fake = new FakeProcessRunner(_ => FakeProcessRunner.Ok());

        var result = await new ProjectPublisher(fake).PublishAsync(copy, rid, false, null, null, CancellationToken.None);

        Assert.Equal(PublishStatus.Refused, result.Status);
        Assert.Contains("DesktopGL", result.Message, StringComparison.Ordinal);
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task A_WindowsDX_copy_may_be_published_for_windows()
    {
        var copy = MakeCopy(desktopGL: false);
        var fake = new FakeProcessRunner(request => Produce(request, "Game.exe"));

        var result = await new ProjectPublisher(fake).PublishAsync(copy, "win-x64", false, null, null, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
    }

    [Theory]
    [InlineData("win-x64", "Game.exe")]
    [InlineData("linux-x64", "Game")]
    [InlineData("osx-x64", "Game")]
    [InlineData("osx-arm64", "Game")]
    public async Task A_DesktopGL_copy_publishes_for_each_rid_and_reports_the_program(string rid, string program)
    {
        var copy = MakeCopy(desktopGL: true);
        var fake = new FakeProcessRunner(request => Produce(request, program));

        var result = await new ProjectPublisher(fake).PublishAsync(copy, rid, false, null, null, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var expectedFolder = Path.Combine(_root, "Game-monogame-publish", rid);
        Assert.Equal(expectedFolder, result.OutputFolder);
        Assert.Equal(Path.Combine(expectedFolder, program), result.Program);
        Assert.Equal([program], result.Files);
        var request = Assert.Single(fake.Requests);
        Assert.Equal("dotnet", request.FileName);
        Assert.Equal(Path.GetFullPath(copy), request.WorkingDirectory);
        Assert.Equal(["-r", rid], request.Arguments.SkipWhile(a => a != "-r").Take(2));
        Assert.Contains("-p:PublishSingleFile=true", request.Arguments);
        Assert.Contains("--self-contained", request.Arguments);
        Assert.Equal(expectedFolder, request.Arguments[request.Arguments.ToList().IndexOf("-o") + 1]);
    }

    [Fact]
    public async Task Output_never_goes_inside_the_migrated_copy()
    {
        var copy = MakeCopy(desktopGL: true);
        var fake = new FakeProcessRunner(request => Produce(request, "Game.exe"));

        var result = await new ProjectPublisher(fake).PublishAsync(copy, "win-x64", false, null, null, CancellationToken.None);

        var relative = Path.GetRelativePath(copy, result.OutputFolder!);
        Assert.StartsWith("..", relative, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_output_folder_with_content_needs_confirmation_and_nothing_runs()
    {
        var copy = MakeCopy(desktopGL: true);
        var existing = ProjectPublisher.OutputFolderFor(copy, "win-x64");
        Directory.CreateDirectory(existing);
        var keep = Path.Combine(existing, "keep.txt");
        File.WriteAllText(keep, "mine");
        var fake = new FakeProcessRunner(_ => FakeProcessRunner.Ok());

        var result = await new ProjectPublisher(fake).PublishAsync(copy, "win-x64", false, null, null, CancellationToken.None);

        Assert.Equal(PublishStatus.NeedsConfirmation, result.Status);
        Assert.Equal(existing, result.OutputFolder);
        Assert.Empty(fake.Requests);
        Assert.Equal("mine", File.ReadAllText(keep));
    }

    [Fact]
    public async Task A_confirmed_publish_uses_the_existing_folder_and_keeps_it_when_it_fails()
    {
        var copy = MakeCopy(desktopGL: true);
        var existing = ProjectPublisher.OutputFolderFor(copy, "win-x64");
        Directory.CreateDirectory(existing);
        File.WriteAllText(Path.Combine(existing, "keep.txt"), "mine");
        var fake = new FakeProcessRunner(_ => FakeProcessRunner.Exit(1, "error NU1101"));

        var result = await new ProjectPublisher(fake).PublishAsync(copy, "win-x64", true, null, null, CancellationToken.None);

        Assert.Equal(PublishStatus.Failed, result.Status);
        Assert.Single(fake.Requests);
        Assert.True(File.Exists(Path.Combine(existing, "keep.txt")));
    }

    [Fact]
    public async Task An_empty_existing_folder_needs_no_confirmation()
    {
        var copy = MakeCopy(desktopGL: true);
        Directory.CreateDirectory(ProjectPublisher.OutputFolderFor(copy, "win-x64"));
        var fake = new FakeProcessRunner(request => Produce(request, "Game.exe"));

        var result = await new ProjectPublisher(fake).PublishAsync(copy, "win-x64", false, null, null, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
    }

    [Fact]
    public async Task A_failed_publish_removes_the_folder_it_created()
    {
        var copy = MakeCopy(desktopGL: true);
        var fake = new FakeProcessRunner(request =>
        {
            Directory.CreateDirectory(OutputOf(request));
            File.WriteAllText(Path.Combine(OutputOf(request), "half.dll"), "x");
            return FakeProcessRunner.Exit(1, "error");
        });

        var result = await new ProjectPublisher(fake).PublishAsync(copy, "linux-x64", false, null, null, CancellationToken.None);

        Assert.Equal(PublishStatus.Failed, result.Status);
        Assert.False(Directory.Exists(ProjectPublisher.OutputFolderFor(copy, "linux-x64")));
    }

    [Fact]
    public async Task A_publish_that_makes_no_program_is_a_failure()
    {
        var copy = MakeCopy(desktopGL: true);
        var fake = new FakeProcessRunner(request =>
        {
            Directory.CreateDirectory(OutputOf(request));
            return FakeProcessRunner.Ok();
        });

        var result = await new ProjectPublisher(fake).PublishAsync(copy, "win-x64", false, null, null, CancellationToken.None);

        Assert.Equal(PublishStatus.Failed, result.Status);
        Assert.Contains("was not created", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancel_timeout_and_a_missing_dotnet_each_get_their_own_status()
    {
        var copy = MakeCopy(desktopGL: true);
        var cancelled = new FakeProcessRunner(_ => new ProcessResult(null, false, true, "", "", TimeSpan.Zero));
        var timedOut = new FakeProcessRunner(_ => new ProcessResult(null, true, false, "", "", TimeSpan.Zero));
        var missing = new FakeProcessRunner(_ => new ProcessResult(null, false, false, "", "", TimeSpan.Zero, "File not found."));

        var a = await new ProjectPublisher(cancelled).PublishAsync(copy, "win-x64", false, null, null, CancellationToken.None);
        var b = await new ProjectPublisher(timedOut).PublishAsync(copy, "win-x64", false, null, TimeSpan.FromMinutes(2), CancellationToken.None);
        var c = await new ProjectPublisher(missing).PublishAsync(copy, "win-x64", false, null, null, CancellationToken.None);

        Assert.Equal(PublishStatus.Cancelled, a.Status);
        Assert.Equal(PublishStatus.TimedOut, b.Status);
        Assert.Contains("2 minutes", b.Message, StringComparison.Ordinal);
        Assert.Equal(PublishStatus.Failed, c.Status);
        Assert.Contains("dotnet", c.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Output_lines_reach_the_progress_callback()
    {
        var copy = MakeCopy(desktopGL: true);
        var fake = new FakeProcessRunner(request =>
        {
            Produce(request, "Game.exe");
            return FakeProcessRunner.Ok("Restoring\nGame -> out");
        });
        var seen = new List<string>();

        await new ProjectPublisher(fake).PublishAsync(copy, "win-x64", false, new SyncProgress<OutputLine>(l => seen.Add(l.Text)), null, CancellationToken.None);

        Assert.Equal(["Restoring", "Game -> out"], seen);
    }

    private static string OutputOf(ProcessRequest request) => request.Arguments[request.Arguments.ToList().IndexOf("-o") + 1];

    // Pretends dotnet publish worked: writes the program into the -o folder.
    private static ProcessResult Produce(ProcessRequest request, string program)
    {
        Directory.CreateDirectory(OutputOf(request));
        File.WriteAllText(Path.Combine(OutputOf(request), program), "x");
        return FakeProcessRunner.Ok();
    }

    // A minimal completed migrated copy: manifest plus one game project.
    private string MakeCopy(bool desktopGL)
    {
        var copy = Path.Combine(_root, "Game-monogame");
        Directory.CreateDirectory(copy);
        File.WriteAllText(Path.Combine(copy, "xnareborn-manifest.json"), """{ "version": 1, "completed": true }""");
        var package = desktopGL ? "MonoGame.Framework.DesktopGL" : "MonoGame.Framework.WindowsDX";
        File.WriteAllText(
            Path.Combine(copy, "Game.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net8.0</TargetFramework><AssemblyName>Game</AssemblyName></PropertyGroup>
              <ItemGroup><PackageReference Include="{package}" Version="3.8.6.1" /></ItemGroup>
            </Project>
            """);
        return copy;
    }
}
