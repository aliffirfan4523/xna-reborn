namespace XnaReborn.App.Services;

/// <summary>A project the user opened before.</summary>
/// <param name="Path">The folder, .sln or .csproj path.</param>
/// <param name="LastOpened">When it was last opened.</param>
public sealed record RecentProject(string Path, DateTimeOffset LastOpened);

/// <summary>Remembers the most recently opened projects.</summary>
public interface IRecentProjectsStore
{
    /// <summary>Returns the remembered projects, most recent first. Empty when nothing is stored or the store is unreadable.</summary>
    IReadOnlyList<RecentProject> Load();

    /// <summary>Remembers <paramref name="path"/> as the most recent project.</summary>
    /// <param name="path">The folder, .sln or .csproj path.</param>
    void Add(string path);
}
