namespace XnaReborn.Migrator.Modern;

/// <summary>Which opt-in window extras the user switched on. All off by default, which leaves the migrated game unchanged.</summary>
/// <param name="AllowWindowResize">The window can be resized; the game keeps its own resolution, scaled to fit.</param>
/// <param name="FullscreenToggle">Alt+Enter switches between a window and borderless fullscreen; the game keeps its own resolution, scaled to fit.</param>
/// <param name="HighDpi">The process asks Windows for per-monitor DPI awareness before the window is created.</param>
public sealed record ModernExtrasChoice(bool AllowWindowResize, bool FullscreenToggle, bool HighDpi)
{
    /// <summary>True when at least one extra is on.</summary>
    public bool Any => AllowWindowResize || FullscreenToggle || HighDpi;

    /// <summary>True when the helper must draw the game at its own resolution and scale it to the window.</summary>
    public bool NeedsScaling => AllowWindowResize || FullscreenToggle;

    /// <summary>Reads the choice from the migration options.</summary>
    /// <param name="options">The options of one run.</param>
    /// <returns>The choice. <see cref="Any"/> is false for the defaults.</returns>
    public static ModernExtrasChoice From(MigrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new ModernExtrasChoice(options.AllowWindowResize, options.FullscreenToggle, options.HighDpi);
    }

    /// <summary>The names of the extras that are on, in a fixed order.</summary>
    public IReadOnlyList<string> FeatureNames()
    {
        var names = new List<string>();
        if (AllowWindowResize)
        {
            names.Add("AllowWindowResize");
        }

        if (FullscreenToggle)
        {
            names.Add("FullscreenToggle");
        }

        if (HighDpi)
        {
            names.Add("HighDpi");
        }

        return names;
    }
}
