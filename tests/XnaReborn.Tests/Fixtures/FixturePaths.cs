namespace XnaReborn.Tests.Fixtures;

/// <summary>Finds the synthetic fixture projects under <c>tests/fixtures</c>.</summary>
public static class FixturePaths
{
    /// <summary>Names of all fixture folders, in a fixed order.</summary>
    public static readonly IReadOnlyList<string> AllNames =
    [
        "empty-folder",
        "multi-project-sln",
        "not-xna",
        "xna31-legacy",
        "xna31-rewritable",
        "xna40-audio",
        "xna40-basic",
        "xna40-custom-content",
        "xna40-gamerservices",
        "xna40-prebuilt",
    ];

    /// <summary>Absolute path of <c>tests/fixtures</c>.</summary>
    public static string Root { get; } = Path.Combine(FindRepoRoot(), "tests", "fixtures");

    /// <summary>Absolute path of one fixture folder.</summary>
    /// <param name="name">Fixture folder name.</param>
    public static string Of(string name) => Path.Combine(Root, name);

    /// <summary>Theory data: one row per fixture name.</summary>
    public static IEnumerable<object[]> NameRows() => AllNames.Select(n => new object[] { n });

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
