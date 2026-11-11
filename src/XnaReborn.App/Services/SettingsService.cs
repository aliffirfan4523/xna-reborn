using System.Text.Json;
using System.Text.Json.Serialization;

namespace XnaReborn.App.Services;

/// <summary>Keeps <see cref="AppSettings"/> in a JSON file, by default <c>%APPDATA%/XnaReborn/settings.json</c>.</summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    /// <summary>Reads the file once. A missing, unreadable, corrupt or newer-version file gives the defaults.</summary>
    /// <param name="filePath">Where to keep the settings, or null for the per-user default.</param>
    public SettingsService(string? filePath = null)
    {
        _filePath = filePath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XnaReborn", "settings.json");
        (Current, LoadProblem) = Load();
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public AppSettings Current { get; private set; }

    /// <inheritdoc />
    public string? LoadProblem { get; private set; }

    /// <inheritdoc />
    public bool Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var next = settings.Normalize();
        var changed = next != Current;
        Current = next;
        LoadProblem = null;
        var written = Write(next);
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return written;
    }

    private (AppSettings Settings, string? Problem) Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return (new AppSettings(), null);
            }

            var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), _json);
            if (loaded is null)
            {
                return (new AppSettings(), "The settings file was empty, so the defaults are in use.");
            }

            return loaded.Version > AppSettings.CurrentVersion
                ? (new AppSettings(), "The settings file was written by a newer version of the app, so the defaults are in use.")
                : (loaded.Normalize(), null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return (new AppSettings(), "The settings file could not be read, so the defaults are in use.");
        }
    }

    private bool Write(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            // Write beside the file and swap, so a crash never leaves a half-written settings file.
            var temp = _filePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, _json));
            File.Move(temp, _filePath, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
