namespace XnaReborn.App.Services;

/// <summary>Holds the user's saved settings.</summary>
public interface ISettingsService
{
    /// <summary>The settings in use now. Never null: a missing or unreadable file gives the defaults.</summary>
    AppSettings Current { get; }

    /// <summary>Why the saved settings could not be used and the defaults are in use, or null when the file was fine or absent.</summary>
    string? LoadProblem { get; }

    /// <summary>Raised after <see cref="Current"/> changed.</summary>
    event EventHandler? Changed;

    /// <summary>Makes <paramref name="settings"/> (normalized) the current settings and saves them.</summary>
    /// <param name="settings">The new settings.</param>
    /// <returns>True when the file was written. False when it could not be: the settings then apply until the app closes.</returns>
    bool Save(AppSettings settings);
}
