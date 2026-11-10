using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using XnaReborn.Runner.Processes;

namespace XnaReborn.Runner.Environment;

/// <summary>Which MonoGame platform the migrated game targets.</summary>
public enum MonoGameTargetKind
{
    /// <summary>MonoGame.Framework.DesktopGL: Windows, Linux and macOS.</summary>
    DesktopGL,

    /// <summary>MonoGame.Framework.WindowsDX: Windows only.</summary>
    WindowsDX,
}

/// <summary>Finds out whether this machine can build and run a migrated copy.</summary>
public sealed partial class DotnetLocator
{
    private const string _downloadUrl = "https://dotnet.microsoft.com/download";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly IProcessRunner _runner;
    private readonly Func<OSPlatform, bool> _isOs;
    private readonly string _osName;

    /// <summary>Creates a locator for the current machine.</summary>
    /// <param name="runner">Runs the dotnet commands.</param>
    public DotnetLocator(IProcessRunner runner)
        : this(runner, RuntimeInformation.IsOSPlatform, DescribeOs())
    {
    }

    /// <summary>Creates a locator with an injectable operating system check, for tests.</summary>
    /// <param name="runner">Runs the dotnet commands.</param>
    /// <param name="isOs">Tells whether the machine runs the given platform.</param>
    /// <param name="osName">Short operating system name for <see cref="EnvironmentInfo.Os"/>.</param>
    public DotnetLocator(IProcessRunner runner, Func<OSPlatform, bool> isOs, string osName)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(isOs);
        _runner = runner;
        _isOs = isOs;
        _osName = osName;
    }

    /// <summary>Detects the dotnet SDK and whether the target can run here.</summary>
    /// <param name="target">The MonoGame target.</param>
    /// <param name="cancellationToken">Cancels the detection.</param>
    /// <returns>What was found. Never throws for a missing dotnet or an unsupported target.</returns>
    public async Task<EnvironmentInfo> DetectAsync(MonoGameTargetKind target, CancellationToken cancellationToken)
    {
        var cwd = System.Environment.CurrentDirectory;
        var version = await _runner.RunAsync(new ProcessRequest("dotnet", ["--version"], cwd, _timeout), null, cancellationToken).ConfigureAwait(false);
        var targetSupported = target == MonoGameTargetKind.DesktopGL || _isOs(OSPlatform.Windows);

        if (version.StartError is not null || version.ExitCode is not 0)
        {
            return new EnvironmentInfo(null, [], false, _osName, targetSupported, Combine(NoDotnet(), TargetProblem(target, targetSupported)));
        }

        var list = await _runner.RunAsync(new ProcessRequest("dotnet", ["--list-sdks"], cwd, _timeout), null, cancellationToken).ConfigureAwait(false);
        var sdks = list.ExitCode is 0 ? ParseSdks(list.Stdout) : [];
        var compatible = sdks.Any(s => MajorOf(s) >= 8);
        var dotnetVersion = version.Stdout.Trim().Split('\n')[0].Trim();
        var problem = compatible
            ? null
            : $"No .NET SDK 8.0 or later was found. Install the .NET 8 SDK from {_downloadUrl} and try again.";
        return new EnvironmentInfo(dotnetVersion, sdks, compatible, _osName, targetSupported, Combine(problem, TargetProblem(target, targetSupported)));
    }

    private static string NoDotnet() =>
        $"The dotnet command was not found. Install the .NET 8 SDK from {_downloadUrl} and try again.";

    private static string? TargetProblem(MonoGameTargetKind target, bool supported) =>
        supported ? null : $"The {target} target needs Windows. Use the DesktopGL target on this operating system.";

    private static string? Combine(string? a, string? b) =>
        a is null ? b : b is null ? a : a + " " + b;

    private static List<string> ParseSdks(string output)
    {
        var result = new List<string>();
        foreach (var line in output.Split('\n'))
        {
            var m = SdkLine().Match(line.Trim());
            if (m.Success)
            {
                result.Add(m.Groups["v"].Value);
            }
        }

        result.Sort(CompareVersions);
        return result;
    }

    private static int MajorOf(string version) =>
        int.TryParse(version.Split('.')[0], out var major) ? major : 0;

    private static int CompareVersions(string a, string b)
    {
        var pa = Numbers(a);
        var pb = Numbers(b);
        for (var i = 0; i < 3; i++)
        {
            var c = pa[i].CompareTo(pb[i]);
            if (c != 0)
            {
                return c;
            }
        }

        // A release sorts after its previews.
        var aPre = a.Contains('-', StringComparison.Ordinal);
        var bPre = b.Contains('-', StringComparison.Ordinal);
        if (aPre != bPre)
        {
            return aPre ? -1 : 1;
        }

        return string.CompareOrdinal(a, b);
    }

    private static int[] Numbers(string version)
    {
        var core = version.Split('-')[0].Split('.');
        var n = new int[3];
        for (var i = 0; i < 3 && i < core.Length; i++)
        {
            _ = int.TryParse(core[i], out n[i]);
        }

        return n;
    }

    private static string DescribeOs()
    {
        var v = System.Environment.OSVersion.Version;
        var name = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsLinux() ? "Linux" : "Unknown OS";
        return $"{name} {v}";
    }

    [GeneratedRegex(@"^(?<v>\d+\.\d+\.\d+(?:-[0-9A-Za-z.\-]+)?)\s+\[", RegexOptions.CultureInvariant)]
    private static partial Regex SdkLine();
}
