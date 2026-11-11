using System.Text.RegularExpressions;
using XnaReborn.Runner.Diagnostics;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Safety;

namespace XnaReborn.Runner.Effects;

/// <summary>Which graphics API the effect compiler targets.</summary>
public enum EffectCompilerProfile
{
    /// <summary>MojoShader to GLSL, for DesktopGL. Runs on any system.</summary>
    OpenGL,

    /// <summary>HLSL to DirectX 11 bytecode, for WindowsDX. Needs Windows.</summary>
    DirectX11,
}

/// <summary>How one effect fared in the pre-build check.</summary>
public enum EffectCheckStatus
{
    /// <summary>The compiler accepted the effect.</summary>
    Passed,

    /// <summary>The compiler rejected the effect.</summary>
    Failed,

    /// <summary>The check did not run for this effect. See <see cref="EffectCheckResult.SkipReason"/>.</summary>
    Skipped,
}

/// <summary>The outcome for one effect.</summary>
/// <param name="Path">The effect path as given.</param>
/// <param name="Status">Passed, failed or skipped.</param>
/// <param name="Profile">The compiler profile used.</param>
/// <param name="Escalations">How many times the DirectX profile level was raised (0 to 2).</param>
/// <param name="Diagnostics">Errors and warnings from the last compile, plus a warning when the effect now needs HiDef.</param>
/// <param name="SkipReason">Why the check did not run, or null.</param>
public sealed record EffectCheckResult(
    string Path,
    EffectCheckStatus Status,
    EffectCompilerProfile Profile,
    int Escalations,
    IReadOnlyList<BuildDiagnostic> Diagnostics,
    string? SkipReason);

/// <summary>
/// Compiles each effect of a migrated copy with <c>mgfxc</c> before the full build, so a shader problem shows up as one error with a
/// file and line instead of a content builder failure. On DirectX it raises the profile level when the shader is too big for level 9.1.
/// Never touches anything outside the migrated copy.
/// </summary>
public sealed partial class EffectBuildCheck
{
    /// <summary>The most times the profile level is raised for one effect.</summary>
    public const int MaxEscalations = 2;

    /// <summary>The code of the warning added when an effect now needs the HiDef graphics profile.</summary>
    public const string HiDefWarningCode = "MGFX0003";

    private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(2);

    private readonly bool _isWindows;

    /// <summary>Creates the check.</summary>
    /// <param name="isWindows">Whether DirectX effects can be compiled here. Defaults to the current operating system.</param>
    public EffectBuildCheck(bool? isWindows = null) => _isWindows = isWindows ?? OperatingSystem.IsWindows();

    /// <summary>True when the diagnostics hold an error that a higher profile level can fix.</summary>
    /// <param name="diagnostics">Diagnostics of one compile.</param>
    public static bool IsLimitError(IReadOnlyList<BuildDiagnostic> diagnostics) =>
        diagnostics.Any(d => d.Severity == BuildDiagnosticSeverity.Error && LimitPattern().IsMatch(d.Code + " " + d.Message));

    [GeneratedRegex(@"X(4505|5608|5426)\b|too many .* slots", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LimitPattern();

    /// <summary>Checks the effects. Results come back in input order.</summary>
    /// <param name="migratedRoot">The migrated copy. It must pass <see cref="MigratedCopyGuard"/>.</param>
    /// <param name="profile">The compiler profile that matches the migrated target.</param>
    /// <param name="effectPaths">Absolute paths of the effect files inside the migrated copy.</param>
    /// <param name="runner">Starts the tool.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    public async Task<IReadOnlyList<EffectCheckResult>> RunAsync(
        string migratedRoot,
        EffectCompilerProfile profile,
        IReadOnlyList<string> effectPaths,
        IProcessRunner runner,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(migratedRoot);
        ArgumentNullException.ThrowIfNull(effectPaths);
        ArgumentNullException.ThrowIfNull(runner);
        if (effectPaths.Count == 0)
        {
            return [];
        }

        var guard = MigratedCopyGuard.Check(migratedRoot);
        if (!guard.Allowed)
        {
            return SkipAll(effectPaths, profile, guard.Message);
        }

        if (profile == EffectCompilerProfile.DirectX11 && !_isWindows)
        {
            return SkipAll(effectPaths, profile, "DirectX effects can only be compiled on Windows (the compiler needs the Direct3D shader compiler).");
        }

        var results = new List<EffectCheckResult>();
        for (var i = 0; i < effectPaths.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await CheckOneAsync(migratedRoot, profile, effectPaths[i], runner, cancellationToken);
            results.Add(result);
            if (result.Status == EffectCheckStatus.Skipped)
            {
                results.AddRange(SkipAll(effectPaths.Skip(i + 1).ToList(), profile, result.SkipReason!));
                break;
            }
        }

        return results;
    }

    private static List<EffectCheckResult> SkipAll(IReadOnlyList<string> paths, EffectCompilerProfile profile, string reason) =>
        paths.Select(p => new EffectCheckResult(p, EffectCheckStatus.Skipped, profile, 0, [], reason)).ToList();

    private static bool ToolMissing(ProcessResult result)
    {
        var text = result.Stdout + "\n" + result.Stderr;
        return result.StartError is not null
            || text.Contains("Cannot find a tool", StringComparison.OrdinalIgnoreCase)
            || text.Contains("dotnet tool restore", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Could not execute because the specified command or file was not found", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Run \"dotnet tool restore\"", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<EffectCheckResult> CheckOneAsync(
        string root,
        EffectCompilerProfile profile,
        string effectPath,
        IProcessRunner runner,
        CancellationToken ct)
    {
        var output = System.IO.Path.Combine(root, "obj", "xnareborn-fx", System.IO.Path.GetFileNameWithoutExtension(effectPath) + ".mgfxo");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output)!);
        var escalations = 0;
        var warnings = new List<BuildDiagnostic>();
        while (true)
        {
            var request = new ProcessRequest(
                "dotnet",
                ["tool", "run", "mgfxc", "--", effectPath, output, "/Profile:" + (profile == EffectCompilerProfile.OpenGL ? "OpenGL" : "DirectX_11")],
                root,
                _timeout);
            var result = await runner.RunAsync(request, null, ct);
            if (ToolMissing(result))
            {
                return new EffectCheckResult(effectPath, EffectCheckStatus.Skipped, profile, escalations, [], result.StartError ?? "The mgfxc tool is not available. Run 'dotnet tool restore' in the migrated copy.");
            }

            if (result.TimedOut || result.Cancelled)
            {
                return new EffectCheckResult(effectPath, EffectCheckStatus.Skipped, profile, escalations, [], result.TimedOut ? "The effect compiler timed out." : "The check was cancelled.");
            }

            if (result.ExitCode == 0)
            {
                return new EffectCheckResult(effectPath, EffectCheckStatus.Passed, profile, escalations, warnings, null);
            }

            var diagnostics = MgfxcOutputParser.Parse(result.Stdout + "\n" + result.Stderr, effectPath).ToList();
            if (diagnostics.Count == 0)
            {
                diagnostics.Add(new BuildDiagnostic(BuildDiagnosticSeverity.Error, "MGFX0000", (result.Stdout + " " + result.Stderr).Trim(), null, null, null, null, effectPath));
            }

            if (profile == EffectCompilerProfile.DirectX11 && escalations < MaxEscalations && IsLimitError(diagnostics) && TryEscalate(effectPath, warnings))
            {
                escalations++;
                continue;
            }

            return new EffectCheckResult(effectPath, EffectCheckStatus.Failed, profile, escalations, [.. warnings, .. diagnostics], null);
        }
    }

    // Edits the migrated effect in place. Returns false when the macro block is missing or already at the top level.
    private static bool TryEscalate(string effectPath, List<BuildDiagnostic> warnings)
    {
        try
        {
            var escalated = ProfileEscalator.Escalate(File.ReadAllText(effectPath));
            if (escalated is null)
            {
                return false;
            }

            File.WriteAllText(effectPath, escalated.Text);
            warnings.RemoveAll(w => w.Code == HiDefWarningCode);
            if (escalated.NeedsHiDef)
            {
                warnings.Add(new BuildDiagnostic(
                    BuildDiagnosticSeverity.Warning,
                    HiDefWarningCode,
                    $"The shader needed profile {escalated.Level}, which needs GraphicsProfile.HiDef at run time.",
                    null,
                    null,
                    null,
                    null,
                    effectPath));
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
