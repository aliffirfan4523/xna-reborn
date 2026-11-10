using System.Diagnostics;

namespace XnaReborn.Runner.Run;

/// <summary>
/// Starts a built game after the caller confirms. The confirmation is asked once per game path for the life of
/// the launcher instance (one "session").
/// </summary>
public sealed class GameLauncher
{
    private static readonly TimeSpan _defaultWindow = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _earlyCrashWindow;
    private readonly HashSet<string> _approved = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private readonly object _approvedGate = new();

    /// <summary>Creates a launcher.</summary>
    /// <param name="earlyCrashWindow">A non-zero exit inside this time counts as a startup failure. Defaults to 10 seconds.</param>
    public GameLauncher(TimeSpan? earlyCrashWindow = null)
    {
        _earlyCrashWindow = earlyCrashWindow ?? _defaultWindow;
    }

    /// <summary>Asks for confirmation (once per path) and starts the game.</summary>
    /// <param name="gameExecutablePath">An executable, or a .dll that is started with the dotnet host.</param>
    /// <param name="workingDirectory">Working directory of the game.</param>
    /// <param name="confirm">Asks the user. Returns true to allow the launch.</param>
    /// <param name="cancellationToken">Cancels the confirmation wait. It does not stop a game that already started.</param>
    /// <param name="arguments">Extra command-line arguments for the game, passed one by one (no shell quoting), or null.</param>
    /// <returns>A handle to the running game, or null when the user declined.</returns>
    /// <exception cref="FileNotFoundException">The game file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">The working directory does not exist.</exception>
    /// <exception cref="InvalidOperationException">The operating system could not start the game.</exception>
    public async Task<RunHandle?> LaunchAsync(
        string gameExecutablePath,
        string workingDirectory,
        Func<LaunchConfirmation, Task<bool>> confirm,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? arguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(confirm);

        var gamePath = Path.GetFullPath(gameExecutablePath);
        if (!File.Exists(gamePath))
        {
            throw new FileNotFoundException($"The game was not found at {gamePath}. Build the project first.", gamePath);
        }

        var workDir = Path.GetFullPath(workingDirectory);
        if (!Directory.Exists(workDir))
        {
            throw new DirectoryNotFoundException($"The working folder {workDir} does not exist.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        bool known;
        lock (_approvedGate)
        {
            known = _approved.Contains(gamePath);
        }

        if (!known)
        {
            var message =
                $"You are about to run {gamePath}. The game's code will run on this computer with your normal user rights, " +
                "so it can read and change the files you can. Only continue if you trust the project.";
            var allowed = await confirm(new LaunchConfirmation(gamePath, message)).ConfigureAwait(false);
            if (!allowed)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            lock (_approvedGate)
            {
                _approved.Add(gamePath);
            }
        }

        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            WorkingDirectory = workDir,
        };

        if (string.Equals(Path.GetExtension(gamePath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = "dotnet";
            startInfo.ArgumentList.Add(gamePath);
        }
        else
        {
            startInfo.FileName = gamePath;
        }

        foreach (var argument in arguments ?? Array.Empty<string>())
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException($"The game could not be started: {gamePath}.");
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            process.Dispose();
            throw new InvalidOperationException($"The game could not be started: {ex.Message}", ex);
        }

        return new RunHandle(process, _earlyCrashWindow);
    }
}
