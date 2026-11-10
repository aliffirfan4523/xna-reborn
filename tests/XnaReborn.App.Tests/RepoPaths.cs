namespace XnaReborn.App.Tests;

/// <summary>Finds the repository's fixture projects.</summary>
public static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string Fixture(string name) => Path.Combine(Root, "tests", "fixtures", name);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "XnaReborn.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("XnaReborn.sln not found above " + AppContext.BaseDirectory);
    }
}
