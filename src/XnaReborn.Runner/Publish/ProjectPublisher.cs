using System.Xml.Linq;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Run;
using XnaReborn.Runner.Safety;

namespace XnaReborn.Runner.Publish;

/// <summary>
/// Publishes a migrated copy as a self-contained single-file program with <c>dotnet publish</c>. The output goes into
/// <c>&lt;migrated copy&gt;-publish/&lt;rid&gt;</c> next to the migrated copy, never into the original project. An output folder that already
/// has content is only used after the caller confirms. Never uses a shell and never asks for elevation.
/// </summary>
/// <remarks>
/// shortcut: native libraries (SDL, OpenAL) stay as files next to the program, because MonoGame looks for them there. Upgrade to
/// IncludeNativeLibrariesForSelfExtract only after checking that a real game still starts from the single file.
/// </remarks>
public sealed class ProjectPublisher
{
    /// <summary>The time a publish may take before it is stopped: 15 minutes. The first publish for a RID downloads its runtime pack.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(15);

    private static readonly IReadOnlyDictionary<string, string> _environment = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DOTNET_NOLOGO"] = "1",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
    };

    private readonly IProcessRunner _runner;

    /// <summary>Creates a publisher.</summary>
    /// <param name="runner">Runs the dotnet process. Defaults to the real process runner.</param>
    public ProjectPublisher(IProcessRunner? runner = null)
    {
        _runner = runner ?? new ProcessRunner();
    }

    /// <summary>Where a publish for this RID goes: <c>&lt;parent&gt;/&lt;name&gt;-publish/&lt;rid&gt;</c>.</summary>
    /// <param name="migratedFolder">The migrated copy.</param>
    /// <param name="rid">A runtime identifier from <see cref="PublishTarget.All"/>.</param>
    /// <returns>The full path of the output folder.</returns>
    /// <exception cref="ArgumentException">The folder has no parent folder or the RID is not a supported one.</exception>
    public static string OutputFolderFor(string migratedFolder, string rid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(migratedFolder);
        if (PublishTarget.Find(rid) is null)
        {
            throw new ArgumentException($"'{rid}' is not a supported runtime identifier.", nameof(rid));
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(migratedFolder));
        var parent = Path.GetDirectoryName(root);
        if (string.IsNullOrEmpty(parent))
        {
            throw new ArgumentException("The migrated copy must not be a drive root.", nameof(migratedFolder));
        }

        return Path.Combine(parent, Path.GetFileName(root) + "-publish", rid);
    }

    /// <summary>The <c>dotnet</c> arguments of a one-file, self-contained publish.</summary>
    /// <param name="projectPath">The game project file.</param>
    /// <param name="rid">The runtime identifier.</param>
    /// <param name="outputFolder">The folder to publish into.</param>
    /// <returns>The arguments, one per item, in a fixed order. Passed without a shell, so no quoting is needed.</returns>
    public static IReadOnlyList<string> BuildArguments(string projectPath, string rid, string outputFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
        return
        [
            "publish", projectPath,
            "-c", "Release",
            "-r", rid,
            "--self-contained", "true",
            "-p:PublishSingleFile=true",
            "-o", outputFolder,
            "-nologo", "-v:minimal",
        ];
    }

    /// <summary>Publishes the migrated copy.</summary>
    /// <param name="migratedFolder">The migrated copy.</param>
    /// <param name="rid">A runtime identifier from <see cref="PublishTarget.All"/>.</param>
    /// <param name="overwriteConfirmed">True when the user agreed to publish into an output folder that already has content.</param>
    /// <param name="lines">Receives each output line while the publish runs, or null.</param>
    /// <param name="timeout">Stops the publish after this long. Defaults to <see cref="DefaultTimeout"/>.</param>
    /// <param name="cancellationToken">Stops the publish and its process tree. The result then has <see cref="PublishStatus.Cancelled"/>.</param>
    /// <returns>The result. Never throws for a refusal, a failed publish, a timeout, a cancel or a missing dotnet command.</returns>
    public async Task<PublishResult> PublishAsync(
        string migratedFolder,
        string rid,
        bool overwriteConfirmed,
        IProgress<OutputLine>? lines,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(migratedFolder);

        // Guards first: nothing is created or run until all of them pass.
        var target = PublishTarget.Find(rid);
        if (target is null)
        {
            return Refuse($"'{rid}' is not a supported platform. Choose one from the list.", null, null);
        }

        var guard = MigratedCopyGuard.Check(migratedFolder);
        if (!guard.Allowed)
        {
            return Refuse(guard.Message, null, rid);
        }

        var game = GameLocator.FindProject(migratedFolder);
        if (game.ProjectPath is null)
        {
            return Refuse(game.Problem!, null, rid);
        }

        if (!target.IsWindows && !UsesDesktopGL(game.ProjectPath))
        {
            return Refuse(
                $"This copy was migrated for WindowsDX, which runs on Windows only. Migrate again with the DesktopGL target to publish for {target.DisplayName}.",
                null,
                rid);
        }

        var output = OutputFolderFor(migratedFolder, rid);
        var existed = Directory.Exists(output);
        if (existed && HasContent(output) && !overwriteConfirmed)
        {
            return new PublishResult(
                PublishStatus.NeedsConfirmation,
                $"The folder '{output}' already has content. Publishing again replaces files with the same names. Confirm to continue.",
                output,
                rid,
                null,
                [],
                TimeSpan.Zero);
        }

        var request = new ProcessRequest(
            "dotnet",
            BuildArguments(game.ProjectPath, rid, output),
            Path.GetFullPath(migratedFolder),
            timeout ?? DefaultTimeout,
            _environment);
        var run = await _runner.RunAsync(request, lines, cancellationToken).ConfigureAwait(false);

        if (run.StartError is not null)
        {
            return Fail($"The dotnet command could not be started: {run.StartError} Install the .NET 8 SDK from https://dotnet.microsoft.com/download and try again.", output, rid, run.Duration, existed);
        }

        if (run.Cancelled)
        {
            return Fail("The publish was cancelled.", output, rid, run.Duration, existed, PublishStatus.Cancelled);
        }

        if (run.TimedOut)
        {
            return Fail($"The publish did not finish within {(timeout ?? DefaultTimeout).TotalMinutes:0.##} minutes and was stopped.", output, rid, run.Duration, existed, PublishStatus.TimedOut);
        }

        if (run.ExitCode is not 0)
        {
            return Fail($"The publish failed (exit code {run.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}). Read the log above.", output, rid, run.Duration, existed);
        }

        var program = Path.Combine(output, game.AssemblyName + (target.IsWindows ? ".exe" : string.Empty));
        if (!File.Exists(program))
        {
            return Fail($"The publish finished but '{program}' was not created.", output, rid, run.Duration, existed);
        }

        var files = Directory.GetFiles(output).Select(f => Path.GetFileName(f)!).Order(StringComparer.Ordinal).ToList();
        return new PublishResult(PublishStatus.Published, $"Published {target.DisplayName} to '{output}'.", output, rid, program, files, run.Duration);
    }

    private static bool UsesDesktopGL(string projectPath)
    {
        try
        {
            return XDocument.Load(projectPath)
                .Descendants()
                .Where(e => e.Name.LocalName == "PackageReference")
                .Any(e => string.Equals((string?)e.Attribute("Include"), "MonoGame.Framework.DesktopGL", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasContent(string folder) => Directory.EnumerateFileSystemEntries(folder).Any();

    private static PublishResult Refuse(string message, string? output, string? rid) =>
        new(PublishStatus.Refused, message, output, rid, null, [], TimeSpan.Zero);

    // A folder this run created is removed when the publish did not finish, so no half-written game is left behind.
    // A folder that was already there (and confirmed) is left as it is.
    private static PublishResult Fail(string message, string output, string rid, TimeSpan duration, bool folderExisted, PublishStatus status = PublishStatus.Failed)
    {
        if (!folderExisted)
        {
            try
            {
                if (Directory.Exists(output))
                {
                    Directory.Delete(output, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                message += " A partly written folder could not be removed: " + output;
            }
        }

        return new PublishResult(status, message, output, rid, null, [], duration);
    }
}
