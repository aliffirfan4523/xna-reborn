namespace XnaReborn.Flash.Tests;

/// <summary>Finds the Flash fixture projects under <c>tests/fixtures/flash</c>.</summary>
public static class FlashFixtures
{
    /// <summary>Absolute path of <c>tests/fixtures/flash</c>.</summary>
    public static string Root { get; } = Path.Combine(FindRepoRoot(), "tests", "fixtures", "flash");

    /// <summary>Names of all fixture folders.</summary>
    public static IReadOnlyList<string> Names { get; } =
        [.. Directory.GetDirectories(Root).Select(d => Path.GetFileName(d)!).Order(StringComparer.Ordinal)];

    /// <summary>Absolute path of one fixture.</summary>
    /// <param name="name">Fixture folder name.</param>
    public static string Of(string name) => Path.Combine(Root, name);

    /// <summary>Theory data: one row per fixture.</summary>
    public static IEnumerable<object[]> NameRows() => Names.Select(n => new object[] { n });

    /// <summary>Every .as file in every fixture.</summary>
    public static IEnumerable<object[]> AsFileRows() =>
        Directory.EnumerateFiles(Root, "*.as", SearchOption.AllDirectories)
            .Select(f => new object[] { Path.GetRelativePath(Root, f).Replace('\\', '/') });

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "XnaReborn.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("XnaReborn.sln not found above " + AppContext.BaseDirectory);
    }
}
