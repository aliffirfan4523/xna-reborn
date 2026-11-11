using XnaReborn.App.Services;

namespace XnaReborn.App.Tests.Fakes;

/// <summary>Keeps recent projects in memory with the same ordering, distinct and size rules as the real store.</summary>
public sealed class FakeRecentProjectsStore : IRecentProjectsStore
{
    /// <summary>The stored projects, most recent first. Tests may fill this directly.</summary>
    public List<RecentProject> Items { get; } = [];

    public IReadOnlyList<RecentProject> Load() => Items.ToList();

    public void Add(string path) => Add(path, null);

    public void Remove(string path) => Items.RemoveAll(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));

    public void Clear() => Items.Clear();

    public void Add(string path, int? score)
    {
        Items.RemoveAll(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
        Items.Insert(0, new RecentProject(path, DateTimeOffset.UtcNow, score));
        if (Items.Count > RecentProjectsStore.MaxItems)
        {
            Items.RemoveRange(RecentProjectsStore.MaxItems, Items.Count - RecentProjectsStore.MaxItems);
        }
    }
}
