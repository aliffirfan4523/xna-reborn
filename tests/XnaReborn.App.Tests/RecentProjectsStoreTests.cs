using XnaReborn.App.Services;

namespace XnaReborn.App.Tests;

public sealed class RecentProjectsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "xnareborn-recent-" + Guid.NewGuid().ToString("N"));

    private string StoreFile => Path.Combine(_folder, "nested", "recent-projects.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void A_missing_file_gives_an_empty_list()
    {
        Assert.Empty(new RecentProjectsStore(StoreFile).Load());
    }

    [Fact]
    public void Add_puts_the_newest_first()
    {
        var store = new RecentProjectsStore(StoreFile);

        store.Add(@"C:\a");
        store.Add(@"C:\b");

        Assert.Equal([@"C:\b", @"C:\a"], store.Load().Select(r => r.Path));
    }

    [Fact]
    public void Adding_the_same_path_again_ignoring_case_moves_it_to_the_front_without_a_duplicate()
    {
        var store = new RecentProjectsStore(StoreFile);
        store.Add(@"C:\a");
        store.Add(@"C:\b");

        store.Add(@"c:\A");

        Assert.Equal([@"c:\A", @"C:\b"], store.Load().Select(r => r.Path));
    }

    [Fact]
    public void Only_the_ten_most_recent_are_kept()
    {
        var store = new RecentProjectsStore(StoreFile);

        for (var i = 1; i <= 12; i++)
        {
            store.Add(@"C:\p" + i);
        }

        var paths = store.Load().Select(r => r.Path).ToList();
        Assert.Equal(10, paths.Count);
        Assert.Equal(@"C:\p12", paths[0]);
        Assert.Equal(@"C:\p3", paths[^1]);
    }

    [Fact]
    public void The_list_survives_a_new_store_instance()
    {
        new RecentProjectsStore(StoreFile).Add(@"C:\kept");

        var loaded = new RecentProjectsStore(StoreFile).Load();

        Assert.Equal(@"C:\kept", Assert.Single(loaded).Path);
        Assert.True(loaded[0].LastOpened > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void A_corrupt_file_gives_an_empty_list_and_is_replaced_by_the_next_add()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StoreFile)!);
        File.WriteAllText(StoreFile, "{ not json");
        var store = new RecentProjectsStore(StoreFile);

        Assert.Empty(store.Load());
        store.Add(@"C:\fresh");

        Assert.Equal(@"C:\fresh", Assert.Single(store.Load()).Path);
    }

    [Fact]
    public void Add_refuses_an_empty_path()
    {
        Assert.Throws<ArgumentException>(() => new RecentProjectsStore(StoreFile).Add(" "));
    }
}
