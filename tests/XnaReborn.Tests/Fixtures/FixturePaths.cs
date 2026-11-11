namespace XnaReborn.Tests.Fixtures;

/// <summary>Finds the synthetic fixture projects under <c>tests/fixtures</c>.</summary>
public static class FixturePaths
{
    /// <summary>Names of all fixture folders, in a fixed order.</summary>
    public static readonly IReadOnlyList<string> AllNames =
    [
        "device-windows-plain",
        "device-wp-silverlight",
        "device-wp7-xna",
        "device-xbox360-copy-solution",
        "device-zune-31",
        "empty-folder",
        "mixed-cs-vb-sln",
        "multi-project-sln",
        "not-xna",
        "vb-monogame-already",
        "vb-xna31-handmade",
        "vb-xna40-basic",
        "xna10-clean",
        "xna10-express-basic",
        "xna20-vs2005",
        "xna31-legacy",
        "xna31-rewritable",
        "xna40-audio",
        "xna40-basic",
        "xna40-custom-content",
        "xna40-effects",
        "xna40-gamerservices",
        "xna40-prebuilt",
        "xna40-xact",
        "xna40-xact-compiled",
    ];

    /// <summary>
    /// The MonoGame upgrade fixtures (A to I of the monogame-upgrade design) and the rules each is positive for.
    /// The README table in <c>tests/fixtures/README.md</c> must list the same rules.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> MonoGamePositives = new Dictionary<string, IReadOnlyList<string>>
    {
        ["mg37-windowsdx-pkgconfig"] = ["MG001", "MG002", "MG003", "MG004", "MG005", "MG006", "MG007", "MG020", "MG027"],
        ["mg37-desktopgl-installdir"] = ["MG001", "MG002", "MG003", "MG004", "MG005", "MG006", "MG017", "MG019", "MG023", "MG025", "MG026"],
        ["mg34-windowsgl"] = ["MG003", "MG004", "MG011", "MG018", "MG021", "MG022"],
        ["mg35-win8-wp8-ouya"] = ["MG015", "MG016", "MG024"],
        ["mg37-multiplatform-sln"] = ["MG012", "MG013", "MG014", "MG024"],
        ["mg3x-protobuild"] = ["MG010"],
        ["mg38-old-pin"] = ["MG008", "MG009"],
        ["mg38-current"] = [],
        ["mg37-source-apis"] = ["MG028", "MG029", "MG030", "MG031", "MG032"],
    };

    /// <summary>Names of the MonoGame upgrade fixture folders, in a fixed order.</summary>
    public static IReadOnlyList<string> MonoGameNames { get; } = [.. MonoGamePositives.Keys.Order(StringComparer.Ordinal)];

    /// <summary>Theory data: one row per MonoGame upgrade fixture name.</summary>
    public static IEnumerable<object[]> MonoGameNameRows() => MonoGameNames.Select(n => new object[] { n });

    /// <summary>Names of the SharpDX Toolkit fixture folders, in a fixed order.</summary>
    public static readonly IReadOnlyList<string> SharpDxNames =
    [
        "sdx-basic",
        "sdx-effects",
        "sdx-lowlevel",
        "sdx-mixed",
        "sdx-negative",
        "sdx-prebuilt",
        "sdx-store-app",
    ];

    /// <summary>Theory data: one row per SharpDX Toolkit fixture name.</summary>
    public static IEnumerable<object[]> SharpDxNameRows() => SharpDxNames.Select(n => new object[] { n });

    /// <summary>Theory data: one row per fixture name, XNA, MonoGame and SharpDX Toolkit.</summary>
    public static IEnumerable<object[]> EveryNameRows() => AllNames.Concat(MonoGameNames).Concat(SharpDxNames).Select(n => new object[] { n });

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
