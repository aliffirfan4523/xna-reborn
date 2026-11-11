using XnaReborn.Flash.Analyzer.Resolving;

namespace XnaReborn.Flash.Tests;

/// <summary>Task 10.1: flixel-api.json is generated from the runtime sources and must match them.</summary>
public class FlixelApiMapTests
{
    private static string RepoRoot() => Path.GetFullPath(Path.Combine(FlashFixtures.Root, "..", "..", ".."));

    private static string CheckedInPath() => Path.Combine(RepoRoot(), "src", "XnaReborn.Flash.Analyzer", "Resolving", "flixel-api.json");

    [Fact]
    public void Checked_in_file_matches_the_runtime_sources()
    {
        var generated = FlixelApiMap.FromFolder(Path.Combine(RepoRoot(), "src", "XnaReborn.Flixel")).ToJson();

        // XNAREBORN_UPDATE_API=1 rewrites the checked-in file. Do this after changing a public member of src/XnaReborn.Flixel, then rebuild.
        if (Environment.GetEnvironmentVariable("XNAREBORN_UPDATE_API") == "1")
        {
            File.WriteAllText(CheckedInPath(), generated);
        }

        var checkedIn = File.ReadAllText(CheckedInPath()).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.True(
            checkedIn == generated,
            "flixel-api.json is out of date. Run the tests once with XNAREBORN_UPDATE_API=1, rebuild, and commit the new file.");
    }

    [Fact]
    public void Embedded_map_loads_and_describes_known_members()
    {
        var map = FlixelApiMap.LoadBuiltIn();

        var sprite = Assert.Single(map.Types, t => t.Name == "FlxSprite");
        Assert.Equal("FlxObject", sprite.Base);
        var obj = Assert.Single(map.Types, t => t.Name == "FlxObject");
        Assert.Contains(obj.Members, m => m is { Name: "x", Kind: "field", Type: "Number", Static: false });
    }

    [Fact]
    public void Generator_maps_types_modifiers_and_ignores_non_public_members()
    {
        var map = FlixelApiMap.FromSources(
        [
            """
            namespace org.flixel;
            public class Base { }
            public class Thing : Base, IDisposable
            {
                public float speed;
                public const int Max = 3;
                public static string Name { get; set; }
                public AsVector<int>? Items;
                public bool hit(Thing other, float dt = 0) => true;
                private int secret;
                internal int hidden;
                public void Dispose() { }
                public class Nested { public int deep; }
            }
            internal class Skipped { public int x; }
            """,
        ]);

        Assert.Equal(["Base", "Thing"], map.Types.Select(t => t.Name));
        var thing = map.Types[1];
        Assert.Equal("Base", thing.Base);
        Assert.Contains(thing.Members, m => m is { Name: "speed", Type: "Number", Static: false });
        Assert.Contains(thing.Members, m => m is { Name: "Max", Type: "int", Static: true });
        Assert.Contains(thing.Members, m => m is { Name: "Name", Kind: "property", Type: "String", Static: true });
        Assert.Contains(thing.Members, m => m is { Name: "Items", Type: "Vector.<int>" });
        var hit = Assert.Single(thing.Members, m => m.Name == "hit");
        Assert.Equal("Boolean", hit.Type);
        Assert.Equal(["Thing", "Number"], hit.Parameters!.Select(p => p.Type));
        Assert.DoesNotContain(thing.Members, m => m.Name is "secret" or "hidden" or "deep");
    }

    [Fact]
    public void Output_is_stable_and_round_trips()
    {
        var map = FlixelApiMap.FromFolder(Path.Combine(RepoRoot(), "src", "XnaReborn.Flixel"));

        Assert.Equal(map.ToJson(), FlixelApiMap.Parse(map.ToJson()).ToJson());
        Assert.DoesNotContain("\r", map.ToJson(), StringComparison.Ordinal);
    }
}
