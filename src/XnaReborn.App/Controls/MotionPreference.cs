using System.Runtime.InteropServices;

using XnaReborn.App.Services;

namespace XnaReborn.App.Controls;

/// <summary>Tells whether the user wants less motion. Transitions check this before they play.</summary>
internal static class MotionPreference
{
    private const uint _getClientAreaAnimation = 0x1042;

    /// <summary>
    /// The choice from the Settings page. <see cref="MotionChoice.On"/> always reduces motion, <see cref="MotionChoice.Off"/> never does,
    /// and <see cref="MotionChoice.System"/> asks the operating system. Set by <see cref="AppearanceApplier"/>.
    /// </summary>
    public static MotionChoice Mode { get; set; } = MotionChoice.System;

    /// <summary>
    /// True when animations should be skipped: the environment variable <c>XNAREBORN_REDUCE_MOTION</c> is <c>1</c>, or the setting
    /// is On, or the setting is System and Windows has "Show animations" off. shortcut: only Windows is read; macOS and Linux play the (short) fade, upgrade when those targets ship.
    /// </summary>
    public static bool Reduce()
    {
        if (Environment.GetEnvironmentVariable("XNAREBORN_REDUCE_MOTION") == "1")
        {
            return true;
        }

        if (Mode != MotionChoice.System)
        {
            return Mode == MotionChoice.On;
        }

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return SystemParametersInfo(_getClientAreaAnimation, 0, out var enabled, 0) && !enabled;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = false)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint winIni);
}
