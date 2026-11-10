using System.Diagnostics;
using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.ViewModels;
using XnaReborn.Migrator;
using XnaReborn.Runner;
using XnaReborn.Runner.Build;
using XnaReborn.Runner.Diagnostics;
using XnaReborn.Runner.Hints;
using XnaReborn.Runner.Run;
using XnaReborn.Runner.Safety;

namespace XnaReborn.App.Tests.BuildRun;

internal static class BuildRunTestData
{
    public static readonly string OutputFolder = Path.Combine(Path.GetTempPath(), "xnareborn-ui-test-output");

    public static readonly FixHint GamerServicesHint = new("H001", "GamerServices no longer exists", "Remove the using and the Xbox Live code.", "XNA009");

    public static readonly FixHint RuntimeHint = new("R001", "A graphics driver is missing", "Install the OpenGL driver.", null);

    /// <summary>Puts a completed migration with options in the session and opens the Build and run page.</summary>
    public static async Task<BuildRunViewModel> OpenAsync(TestServices t, MonoGameTarget target = MonoGameTarget.DesktopGL)
    {
        t.Session.SourcePath = RepoPaths.Fixture("xna40-basic");
        t.Session.Report = SampleData.Report();
        t.Session.MigrationResult = SampleData.Migration(completed: true);
        t.Session.MigrationOptions = new MigrationOptions(OutputFolder, target, false, false, new HashSet<string>());
        await t.OpenPageAsync(WorkflowStep.BuildRun);
        return (BuildRunViewModel)t.Main.CurrentPage;
    }

    public static BuildDiagnostic Error(string code, string message, string? file = "Game1.cs", int? line = 12) =>
        new(BuildDiagnosticSeverity.Error, code, message, file, line, 5, null, null);

    public static BuildDiagnostic Warning(string code, string message) =>
        new(BuildDiagnosticSeverity.Warning, code, message, "Game1.cs", 3, 1, null, null);

    public static BuildResult FailedBuild(params DiagnosticGroup[] groups) =>
        new(false, TimeSpan.FromSeconds(3.2), groups, "raw output", 1, false, false);

    public static RunnerResult Result(BuildResult? build = null, RunHandle? running = null, RunSession? failure = null, string? problem = null) =>
        new(new GuardVerdict(true, "OK", "game.csproj"), SampleData.Environment(), build, null, running, failure, problem);

    public static BuildResult SucceededBuild() => new(true, TimeSpan.FromSeconds(1.5), [], "ok", 0, false, false);

    /// <summary>
    /// Starts a harmless long-lived system process (ping or sleep, not a game) through the real launcher, to get a real
    /// <see cref="RunHandle"/>. Returns null when this machine has neither program.
    /// </summary>
    public static async Task<RunHandle?> StartIdleProcessAsync()
    {
        string program;
        string[] arguments;
        if (OperatingSystem.IsWindows())
        {
            program = Path.Combine(Environment.SystemDirectory, "PING.EXE");
            arguments = ["-n", "60", "127.0.0.1"];
        }
        else
        {
            program = File.Exists("/bin/sleep") ? "/bin/sleep" : "/usr/bin/sleep";
            arguments = ["60"];
        }

        if (!File.Exists(program))
        {
            return null;
        }

        return await new GameLauncher().LaunchAsync(
            program, Path.GetDirectoryName(program)!, _ => Task.FromResult(true), CancellationToken.None, arguments);
    }
}
