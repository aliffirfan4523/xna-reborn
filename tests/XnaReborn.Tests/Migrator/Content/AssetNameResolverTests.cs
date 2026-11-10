using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Migrator.Content;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator.Content;

/// <summary>Asset names and name conflicts.</summary>
[Trait("Category", "Migrator")]
public class AssetNameResolverTests
{
    private static ContentItem Item(string path, string? name = null) =>
        new(path, name, null, null, new Dictionary<string, string>());

    private static string[] Names(params ContentItem[] items) =>
        AssetNameResolver.Resolve(items, "Content").Names.Select(n => n.AssetName).ToArray();

    [Fact]
    public void Without_override_the_name_is_the_path_without_extension()
    {
        Assert.Equal(["hero", "Textures/hero", "A/B/C/deep"], Names(Item("hero.png"), Item("Textures/hero.png"), Item("A/B/C/deep.tga")));
    }

    [Fact]
    public void Override_replaces_only_the_file_name_part()
    {
        Assert.Equal(["Textures/big", "big", "A/B/big"], Names(Item("Textures/hero.png", "big"), Item("hero.png", "big"), Item("A/B/hero.png", "big")));
    }

    [Fact]
    public void Override_does_not_get_an_extension()
    {
        Assert.Equal(["Fonts/Hud"], Names(Item("Fonts/Hud.spritefont", "Hud")));
    }

    [Fact]
    public void Separators_are_normalized_and_leading_dot_slash_is_removed()
    {
        Assert.Equal(["Textures/hero", "Textures/hero", "Textures/hero"], Names(Item(@"Textures\hero.png"), Item("./Textures/hero.png"), Item(@".\Textures\hero.png")));
    }

    [Fact]
    public void Only_the_last_extension_is_removed_and_a_leading_dot_is_kept()
    {
        Assert.Equal(["a.b", ".hidden", "noext"], Names(Item("a.b.png"), Item(".hidden"), Item("noext")));
    }

    [Fact]
    public void Blank_override_counts_as_no_override()
    {
        Assert.Equal(["Textures/hero"], Names(Item("Textures/hero.png", "  ")));
    }

    [Fact]
    public void Names_keep_input_order_and_the_same_item_instances()
    {
        var a = Item("b.png");
        var b = Item("a.png");

        var result = AssetNameResolver.Resolve([a, b], "Content");

        Assert.Same(a, result.Names[0].Item);
        Assert.Same(b, result.Names[1].Item);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Empty_input_gives_empty_result()
    {
        var result = AssetNameResolver.Resolve([], "Content");

        Assert.Empty(result.Names);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Two_overrides_with_the_same_name_conflict_and_nothing_is_dropped()
    {
        var a = Item("Textures/a.png", "x");
        var b = Item("Textures/b.png", "x");
        var c = Item("Textures/c.png");

        var result = AssetNameResolver.Resolve([a, b, c], "Content");

        Assert.Equal(3, result.Names.Count);
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("Textures/x", conflict.AssetName);
        Assert.Equal([a, b], conflict.Items);
    }

    [Fact]
    public void Case_only_difference_conflicts()
    {
        var a = Item("Textures/Hero.png");
        var b = Item("textures/hero.png");

        var result = AssetNameResolver.Resolve([a, b], "Content");

        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("Textures/Hero", conflict.AssetName);
        Assert.Equal([a, b], conflict.Items);
    }

    [Fact]
    public void Override_colliding_with_a_path_name_conflicts()
    {
        var a = Item("Textures/hero.png");
        var b = Item("Textures/other.png", "hero");

        var result = AssetNameResolver.Resolve([a, b], "Content");

        Assert.Equal([a, b], Assert.Single(result.Conflicts).Items);
    }

    [Fact]
    public void Same_name_with_different_extensions_conflicts()
    {
        var a = Item("Fonts/Hud.spritefont");
        var b = Item("Fonts/Hud.png");

        Assert.Single(AssetNameResolver.Resolve([a, b], "Content").Conflicts);
    }

    [Fact]
    public void Each_conflicting_name_is_reported_once_in_order_of_first_appearance()
    {
        var items = new[] { Item("b1.png", "b"), Item("a1.png", "a"), Item("b2.png", "b"), Item("a2.png", "a"), Item("b3.png", "b") };

        var result = AssetNameResolver.Resolve(items, "Content");

        Assert.Equal(["b", "a"], result.Conflicts.Select(c => c.AssetName));
        Assert.Equal(3, result.Conflicts[0].Items.Count);
        Assert.Equal(2, result.Conflicts[1].Items.Count);
    }

    [Fact]
    public void Content_root_directory_does_not_change_names()
    {
        var items = new[] { Item("Textures/hero.png") };

        Assert.Equal(
            AssetNameResolver.Resolve(items, "Content").Names.Select(n => n.AssetName),
            AssetNameResolver.Resolve(items, "Other/Root").Names.Select(n => n.AssetName));
    }

    [Fact]
    public void Resolving_twice_gives_equal_names()
    {
        var items = new[] { Item("a.png"), Item("b/c.png", "d") };

        Assert.Equal(
            AssetNameResolver.Resolve(items, "Content").Names.Select(n => n.AssetName),
            AssetNameResolver.Resolve(items, "Content").Names.Select(n => n.AssetName));
    }

    [Fact]
    public void Null_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => AssetNameResolver.Resolve(null!, "Content"));
        Assert.Throws<ArgumentNullException>(() => AssetNameResolver.Resolve([], null!));
        Assert.Throws<ArgumentNullException>(() => AssetNameResolver.Resolve([null!], "Content"));
    }

    [Fact]
    public void Custom_content_fixture_names()
    {
        var items = ReadFixture("xna40-custom-content");

        var names = AssetNameResolver.Resolve(items, "Content").Names.Select(n => n.AssetName).ToArray();

        Assert.Equal(["Textures/hero", "Textures/ship", "Audio/Sounds", "Shaders/Glow", "Fonts/Hud"], names);
    }

    [Fact]
    public void Basic_fixture_has_no_conflicts()
    {
        var result = AssetNameResolver.Resolve(ReadFixture("xna40-basic"), "Content");

        Assert.Equal(["Textures/hero", "Fonts/Font"], result.Names.Select(n => n.AssetName));
        Assert.Empty(result.Conflicts);
    }

    internal static IReadOnlyList<ContentItem> ReadFixture(string fixture)
    {
        var path = Path.Combine(FixturePaths.Of(fixture), "Content", "Content.contentproj");
        return new ContentProjectReader(new ReadOnlyFileSystem()).Read(path, FixturePaths.Of(fixture)).Items;
    }
}
