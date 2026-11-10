using XnaReborn.Runner.Build;
using XnaReborn.Runner.Diagnostics;
using XnaReborn.Runner.Processes;

namespace XnaReborn.Tests.Runner;

[Trait("Category", "Runner")]
public sealed class BuildRunnerTests
{
    private static readonly string _folder = Path.Combine(Path.GetTempPath(), "xr-build");

    private static async Task<(BuildResult Result, FakeProcessRunner Fake)> BuildSampleAsync(string sample, int exitCode)
    {
        var fake = new FakeProcessRunner(_ => FakeProcessRunner.Exit(exitCode, RunnerTestPaths.ReadSample(sample)));
        var result = await new BuildRunner(fake).BuildAsync(_folder, "Game.csproj", TimeSpan.FromMinutes(1), null, CancellationToken.None);
        return (result, fake);
    }

    [Fact]
    public async Task RunsDotnetBuildWithTheContractArguments()
    {
        var (_, fake) = await BuildSampleAsync("build-cs-errors", 1);

        var request = Assert.Single(fake.Requests);
        Assert.Equal("dotnet", request.FileName);
        Assert.Equal(["build", "Game.csproj", "-nologo", "-v:minimal", "-consoleloggerparameters:NoSummary"], request.Arguments);
        Assert.Equal(_folder, request.WorkingDirectory);
        Assert.Equal(TimeSpan.FromMinutes(1), request.Timeout);
    }

    [Fact]
    public void DefaultTimeoutIsFiveMinutes() => Assert.Equal(TimeSpan.FromMinutes(5), BuildRunner.DefaultTimeout);

    [Fact]
    public async Task CsErrors_GiveOneGroupWithTheResolveTexture2DHint()
    {
        var (result, _) = await BuildSampleAsync("build-cs-errors", 1);

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.ExitCode);
        var group = Assert.Single(result.Groups);
        Assert.Equal("H011", group.Hint?.Id);
        Assert.Equal("XNA027", group.Hint?.RuleId);
        var item = Assert.Single(group.Items);
        Assert.Equal("CS0246", item.Code);
        Assert.Contains("ResolveTexture2D", item.Message);
        Assert.Equal(14, item.Line);
    }

    [Fact]
    public async Task MgcbErrors_GiveAContentGroupAndTheMsb3073Hint()
    {
        var (result, _) = await BuildSampleAsync("build-mgcb-errors", 1);

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Groups.Count);
        Assert.Contains(result.Groups, g => g.Items.Any(d => d.Code == "MGCB" && d.AssetPath is not null));
        var builder = Assert.Single(result.Groups, g => g.Hint?.Id == "H022");
        var item = Assert.Single(builder.Items);
        Assert.Equal("MSB3073", item.Code);
    }

    [Fact]
    public async Task MissingFont_GivesTheFontHintLinkedToXna013()
    {
        var (result, _) = await BuildSampleAsync("build-mgcb-font-missing", 1);

        var group = Assert.Single(result.Groups, g => g.Hint?.Id == "H018");
        Assert.Equal("XNA013", group.Hint!.RuleId);
    }

    [Fact]
    public async Task RestoreError_GivesNu1101()
    {
        var (result, _) = await BuildSampleAsync("build-restore-error", 1);

        var group = Assert.Single(result.Groups);
        Assert.Equal("H021", group.Hint?.Id);
        Assert.Equal("NU1101", Assert.Single(group.Items).Code);
    }

    [Fact]
    public async Task RawOutput_KeepsStdoutAndStderrInArrivalOrder()
    {
        var seen = new List<OutputLine>();
        var fake = new FakeProcessRunner(_ => new ProcessResult(0, false, false, "one\ntwo", "err", TimeSpan.Zero));

        var result = await new BuildRunner(fake).BuildAsync(
            _folder, "Game.csproj", TimeSpan.FromMinutes(1), new SyncProgress<OutputLine>(seen.Add), CancellationToken.None);

        Assert.Equal(["one", "two", "err"], seen.Select(l => l.Text));
        Assert.Equal("one\ntwo\nerr", result.RawOutput);
        Assert.True(result.Succeeded);
        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task OutputLinesAreStreamedToTheCaller()
    {
        var seen = new List<string>();
        var fake = new FakeProcessRunner(_ => FakeProcessRunner.Exit(1, RunnerTestPaths.ReadSample("build-cs-errors")));

        await new BuildRunner(fake).BuildAsync(
            _folder, "Game.csproj", TimeSpan.FromMinutes(1), new SyncProgress<OutputLine>(l => seen.Add(l.Text)), CancellationToken.None);

        Assert.Contains(seen, l => l.Contains("CS0246", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExitZero_Succeeds()
    {
        var fake = new FakeProcessRunner(_ => FakeProcessRunner.Ok("  Restored Game.csproj\n  Game -> bin/Debug/net8.0/Game.dll"));

        var result = await new BuildRunner(fake).BuildAsync(_folder, "Game.csproj", TimeSpan.FromMinutes(1), null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task Timeout_IsNotSuccess()
    {
        var fake = new FakeProcessRunner(_ => new ProcessResult(null, true, false, "  Restoring...", string.Empty, TimeSpan.FromMinutes(5)));

        var result = await new BuildRunner(fake).BuildAsync(_folder, "Game.csproj", TimeSpan.FromMinutes(5), null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.True(result.TimedOut);
        Assert.Null(result.ExitCode);
    }

    [Fact]
    public async Task Cancel_IsNotSuccess()
    {
        var fake = new FakeProcessRunner(_ => new ProcessResult(null, false, true, string.Empty, string.Empty, TimeSpan.Zero));

        var result = await new BuildRunner(fake).BuildAsync(_folder, "Game.csproj", TimeSpan.FromMinutes(5), null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.True(result.Cancelled);
    }

    [Fact]
    public async Task ExitZeroWithTimeoutFlag_IsNotSuccess()
    {
        var fake = new FakeProcessRunner(_ => new ProcessResult(0, true, false, string.Empty, string.Empty, TimeSpan.Zero));

        var result = await new BuildRunner(fake).BuildAsync(_folder, "Game.csproj", TimeSpan.FromMinutes(5), null, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task StartError_GivesOneExplainingDiagnostic()
    {
        var fake = new FakeProcessRunner(_ => new ProcessResult(null, false, false, string.Empty, string.Empty, TimeSpan.Zero, "An error occurred trying to start process 'dotnet'."));

        var result = await new BuildRunner(fake).BuildAsync(_folder, "Game.csproj", TimeSpan.FromMinutes(5), null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.ExitCode);
        var item = Assert.Single(Assert.Single(result.Groups).Items);
        Assert.Equal(BuildDiagnosticSeverity.Error, item.Severity);
        Assert.Contains("could not be started", item.Message);
        Assert.Contains(".NET 8 SDK", item.Message);
    }

    [Fact]
    public async Task SetsQuietEnvironmentForDotnet()
    {
        var (_, fake) = await BuildSampleAsync("build-cs-errors", 1);

        var env = Assert.Single(fake.Requests).Environment;
        Assert.NotNull(env);
        Assert.Equal("1", env["DOTNET_CLI_TELEMETRY_OPTOUT"]);
        Assert.Equal("1", env["DOTNET_NOLOGO"]);
    }

    [Fact]
    public async Task RejectsEmptyArguments()
    {
        var runner = new BuildRunner(new FakeProcessRunner(_ => FakeProcessRunner.Ok()));

        await Assert.ThrowsAsync<ArgumentException>(() => runner.BuildAsync(" ", "Game.csproj", TimeSpan.FromMinutes(1), null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => runner.BuildAsync(_folder, "", TimeSpan.FromMinutes(1), null, CancellationToken.None));
    }
}
