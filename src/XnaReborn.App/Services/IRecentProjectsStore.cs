namespace XnaReborn.App.Services;

/// <summary>A project the user opened before.</summary>
/// <param name="Path">The folder, .sln or .csproj path.</param>
/// <param name="LastOpened">When it was last opened.</param>
/// <param name="LastScore">The compatibility score of its last analysis, or null when unknown.</param>
public sealed record RecentProject(string Path, DateTimeOffset LastOpened, int? LastScore = null)
{
    /// <summary>A short name for cards: the file name without extension, or the folder name.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Name
    {
        get
        {
            var trimmed = Path.TrimEnd('/', '\\');
            var name = System.IO.Path.GetFileName(trimmed);
            var ext = System.IO.Path.GetExtension(name);
            if (ext.Equals(".sln", StringComparison.OrdinalIgnoreCase) || ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                name = System.IO.Path.GetFileNameWithoutExtension(name);
            }

            return string.IsNullOrEmpty(name) ? Path : name;
        }
    }

    /// <summary>True when <see cref="LastScore"/> is known.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasScore => LastScore is not null;

    /// <summary>Text for the score chip, such as "Score 72" or "Not scored yet".</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ScoreText => LastScore is { } s ? $"Score {s}" : "Not scored yet";
}

/// <summary>Remembers the most recently opened projects.</summary>
public interface IRecentProjectsStore
{
    /// <summary>Returns the remembered projects, most recent first. Empty when nothing is stored or the store is unreadable.</summary>
    IReadOnlyList<RecentProject> Load();

    /// <summary>Remembers <paramref name="path"/> as the most recent project.</summary>
    /// <param name="path">The folder, .sln or .csproj path.</param>
    void Add(string path);

    /// <summary>Remembers <paramref name="path"/> as the most recent project, with the score of its analysis.</summary>
    /// <param name="path">The folder, .sln or .csproj path.</param>
    /// <param name="score">The compatibility score, or null when unknown.</param>
    void Add(string path, int? score);

    /// <summary>Forgets <paramref name="path"/>. Does nothing when it is not stored. Never touches the project on disk.</summary>
    /// <param name="path">The path to forget.</param>
    void Remove(string path);

    /// <summary>Forgets every remembered project. Never touches a project on disk.</summary>
    void Clear();
}
