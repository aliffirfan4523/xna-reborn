using Avalonia;
using Avalonia.Styling;
using XnaReborn.App.Services;

namespace XnaReborn.App.Controls;

/// <summary>Applies the theme and the reduce motion choice of the settings to the running app.</summary>
internal static class AppearanceApplier
{
    /// <summary>Sets the theme variant of the application and the motion mode. Does nothing for the theme when there is no application.</summary>
    /// <param name="settings">The settings to apply.</param>
    public static void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        MotionPreference.Mode = settings.ReduceMotion;
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = settings.Theme switch
            {
                ThemeChoice.Light => ThemeVariant.Light,
                ThemeChoice.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }
}
