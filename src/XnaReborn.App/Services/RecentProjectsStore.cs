using System.Text.Json;

namespace XnaReborn.App.Services;

/// <summary>Stores recent projects as JSON, by default under <c>%APPDATA%/XnaReborn/recent-projects.json</c>.</summary>
public sealed class RecentProjectsStore : IRecentProjectsStore
{
    /// <summary>The most projects that are kept.</summary>
    public const int MaxItems = 10;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _filePath;

    /// <summary>Creates a store.</summary>
    /// <param name="filePath">Where to keep the list, or null for the per-user default.</param>
    public RecentProjectsStore(string? filePath = null)
    {
        _filePath = filePath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XnaReborn", "recent-projects.json");
    }

    /// <inheritdoc />
    public IReadOnlyList<RecentProject> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            var items = JsonSerializer.Deserialize<List<RecentProject>>(File.ReadAllText(_filePath), _json);
            return items?.Where(i => !string.IsNullOrWhiteSpace(i.Path)).Take(MaxItems).ToList() ?? [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    /// <inheritdoc />
    public void Add(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var items = Load()
            .Where(i => !string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase))
            .Prepend(new RecentProject(path, DateTimeOffset.UtcNow))
            .Take(MaxItems)
            .ToList();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(items, _json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A list that cannot be saved must not stop the user from opening a project.
        }
    }
}
