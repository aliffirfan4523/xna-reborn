using XnaReborn.Runner.Diagnostics;
using XnaReborn.Runner.Hints;
using XnaReborn.Runner.Processes;

namespace XnaReborn.Runner.Build;

/// <summary>
/// Builds a migrated copy with <c>dotnet build</c>, streams the output, and turns it into grouped diagnostics with hints.
/// Restore is part of the build. Never uses a shell and never asks for elevation.
/// </summary>
public sealed class BuildRunner
{
    /// <summary>The time a build may take before it is stopped: 5 minutes.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    private static readonly IReadOnlyDictionary<string, string> _environment = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DOTNET_NOLOGO"] = "1",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
    };

    private readonly IProcessRunner _runner;
    private readonly HintMatcher _matcher;

    /// <summary>Creates a build runner.</summary>
    /// <param name="runner">Runs the dotnet process.</param>
    /// <param name="matcher">Finds hints for diagnostics. Defaults to a matcher over the shipped catalog.</param>
    public BuildRunner(IProcessRunner runner, HintMatcher? matcher = null)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
        _matcher = matcher ?? new HintMatcher();
    }

    /// <summary>Builds a project or solution.</summary>
    /// <param name="migratedFolder">The migrated copy; the working directory of the build.</param>
    /// <param name="buildTarget">The project or solution file to build.</param>
    /// <param name="timeout">Stops the build (and its process tree) after this long.</param>
    /// <param name="lines">Receives each output line while the build runs, or null.</param>
    /// <param name="cancellationToken">Stops the build. The result then has <see cref="BuildResult.Cancelled"/> set.</param>
    /// <returns>The result. Never throws for a failed build, a timeout, a cancel or a missing dotnet command.</returns>
    public async Task<BuildResult> BuildAsync(
        string migratedFolder,
        string buildTarget,
        TimeSpan timeout,
        IProgress<OutputLine>? lines,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(migratedFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildTarget);

        var capture = new CapturingProgress(lines);
        var request = new ProcessRequest(
            "dotnet",
            ["build", buildTarget, "-nologo", "-v:minimal", "-consoleloggerparameters:NoSummary"],
            migratedFolder,
            timeout,
            _environment);

        var result = await _runner.RunAsync(request, capture, cancellationToken).ConfigureAwait(false);

        if (result.StartError is not null)
        {
            var diagnostic = new BuildDiagnostic(
                BuildDiagnosticSeverity.Error,
                "DOTNET",
                $"The dotnet command could not be started: {result.StartError} Install the .NET 8 SDK from https://dotnet.microsoft.com/download and try again.",
                null,
                null,
                null,
                null,
                null);
            return new BuildResult(false, result.Duration, DiagnosticGrouper.Group([diagnostic], _matcher.Match), string.Empty, null, false, false);
        }

        // The lines arrive in output order. A runner that never reports lines still has its two captured streams.
        var raw = capture.HasLines ? capture.Text : string.Join('\n', new[] { result.Stdout, result.Stderr }.Where(s => s.Length > 0));
        var groups = DiagnosticGrouper.Group(ParseInOrder(raw), _matcher.Match);
        var succeeded = result.ExitCode is 0 && !result.TimedOut && !result.Cancelled;
        return new BuildResult(succeeded, result.Duration, groups, raw, result.ExitCode, result.TimedOut, result.Cancelled);
    }

    // Both parsers read each line on its own, so parsing line by line keeps the diagnostics in output order.
    private static List<BuildDiagnostic> ParseInOrder(string raw)
    {
        var all = new List<BuildDiagnostic>();
        foreach (var line in raw.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            all.AddRange(MsBuildOutputParser.Parse(line));
            all.AddRange(MgcbOutputParser.Parse(line));
        }

        return all;
    }

    private sealed class CapturingProgress(IProgress<OutputLine>? inner) : IProgress<OutputLine>
    {
        private readonly List<string> _lines = [];
        private readonly object _gate = new();

        public bool HasLines
        {
            get
            {
                lock (_gate)
                {
                    return _lines.Count > 0;
                }
            }
        }

        public string Text
        {
            get
            {
                lock (_gate)
                {
                    return string.Join('\n', _lines);
                }
            }
        }

        public void Report(OutputLine value)
        {
            lock (_gate)
            {
                _lines.Add(value.Text);
            }

            inner?.Report(value);
        }
    }
}
