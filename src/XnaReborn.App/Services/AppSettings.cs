using XnaReborn.Migrator;
using XnaReborn.Runner.Publish;

namespace XnaReborn.App.Services;

/// <summary>The color theme the user prefers.</summary>
public enum ThemeChoice
{
    /// <summary>Follow the operating system.</summary>
    System,

    /// <summary>Always light.</summary>
    Light,

    /// <summary>Always dark.</summary>
    Dark,
}

/// <summary>Whether the app should skip its short animations.</summary>
public enum MotionChoice
{
    /// <summary>Follow the operating system.</summary>
    System,

    /// <summary>Reduce motion: skip animations.</summary>
    On,

    /// <summary>Always play the animations.</summary>
    Off,
}

/// <summary>
/// What the user can set once and the app remembers. Defaults only: a choice on a page for one run still wins.
/// There is deliberately no setting that skips a confirmation, overwrites an output folder without asking, or uploads anything.
/// </summary>
public sealed record AppSettings
{
    /// <summary>The version this build of the app writes. A file with a higher version is ignored, because its meaning is unknown.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The most recent projects that can be kept. Equals <see cref="RecentProjectsStore.MaxItems"/>.</summary>
    public const int MaxRecentLimit = RecentProjectsStore.MaxItems;

    /// <summary>The runtime identifier the publish picker starts on when nothing else is set.</summary>
    public const string DefaultPublishRid = "win-x64";

    /// <summary>The version of the file format.</summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>The color theme.</summary>
    public ThemeChoice Theme { get; init; } = ThemeChoice.System;

    /// <summary>Reduce motion.</summary>
    public MotionChoice ReduceMotion { get; init; } = MotionChoice.System;

    /// <summary>The MonoGame platform the migration setup starts on. A SharpDX Toolkit project still starts on WindowsDX.</summary>
    public MonoGameTarget DefaultTarget { get; init; } = MonoGameTarget.DesktopGL;

    /// <summary>Whether the migration setup starts with the compatibility shims on.</summary>
    public bool EnableShims { get; init; }

    /// <summary>Whether the migration setup starts with font substitution on.</summary>
    public bool SubstituteFonts { get; init; }

    /// <summary>Whether the migration setup starts with a resizable window.</summary>
    public bool AllowWindowResize { get; init; }

    /// <summary>Whether the migration setup starts with the fullscreen toggle on.</summary>
    public bool FullscreenToggle { get; init; }

    /// <summary>Whether the migration setup starts with high-DPI awareness on.</summary>
    public bool HighDpi { get; init; }

    /// <summary>The folder in which new migrated copies are placed, or null to place them next to the original.</summary>
    public string? OutputParentFolder { get; init; }

    /// <summary>The platform (runtime identifier) the publish picker starts on, such as <c>linux-x64</c>.</summary>
    public string PublishRid { get; init; } = DefaultPublishRid;

    /// <summary>Whether opened projects are remembered.</summary>
    public bool KeepRecentProjects { get; init; } = true;

    /// <summary>How many recent projects are shown and kept, from 1 to <see cref="MaxRecentLimit"/>.</summary>
    public int MaxRecentProjects { get; init; } = MaxRecentLimit;

    /// <summary>Whether the build results show the fix hint of each problem group.</summary>
    public bool ShowHints { get; init; } = true;

    /// <summary>Whether the build log follows the newest line.</summary>
    public bool AutoFollowLog { get; init; } = true;

    /// <summary>Whether Build and run checks the .NET SDK as soon as it opens.</summary>
    public bool CheckSdkOnStart { get; init; } = true;

    /// <summary>Returns a copy with every value inside its allowed range, so a hand-edited file cannot break a page.</summary>
    public AppSettings Normalize() => this with
    {
        Version = CurrentVersion,
        Theme = Enum.IsDefined(Theme) ? Theme : ThemeChoice.System,
        ReduceMotion = Enum.IsDefined(ReduceMotion) ? ReduceMotion : MotionChoice.System,
        DefaultTarget = Enum.IsDefined(DefaultTarget) ? DefaultTarget : MonoGameTarget.DesktopGL,
        OutputParentFolder = string.IsNullOrWhiteSpace(OutputParentFolder) ? null : OutputParentFolder.Trim(),
        PublishRid = PublishTarget.Find(PublishRid)?.Rid ?? DefaultPublishRid,
        MaxRecentProjects = Math.Clamp(MaxRecentProjects, 1, MaxRecentLimit),
    };
}
