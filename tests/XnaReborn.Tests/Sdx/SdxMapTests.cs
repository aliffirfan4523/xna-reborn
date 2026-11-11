using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Core.Toolkit;
using XnaReborn.Tests.Device;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Sdx;

/// <summary>Task 5.1: the rename map loads, matches the catalog, and every MonoGame name in it exists in MonoGame 3.8.6.1.</summary>
[Trait("Category", "Sdx")]
public sealed class SdxMapTests
{
    private static readonly Lazy<RuleCatalog> _catalog = new(() =>
        RuleCatalogLoader.LoadAsync(Path.Combine(FixturePaths.Root, "..", "..", "rules", "sharpdx-toolkit-to-monogame.json"), new ReadOnlyFileSystem(), CancellationToken.None)
            .GetAwaiter().GetResult());

    private static Rule Rule(string id) => _catalog.Value.Rules.Single(r => r.Id == id);

    private static string[] Names(string id) => ((MemberMatch)Rule(id).Match).AllNames.ToArray();

    [Fact]
    public void The_shipped_map_loads_and_lists_its_sources()
    {
        var map = ToolkitMap.Default;

        Assert.NotEmpty(map.Sources);
        Assert.All(map.Sources, s => Assert.StartsWith("https://", s, StringComparison.Ordinal));
        Assert.Equal(["SDX020", "SDX021", "SDX022", "SDX023", "SDX024"], map.Namespaces.Select(n => n.RuleId));
        Assert.Equal(9, map.MatrixFactories.Count);
        Assert.Equal(9, map.MathUtilMembers.Count);
    }

    [Fact]
    public void The_map_file_in_the_repository_equals_the_embedded_copy()
    {
        var path = Path.Combine(FixturePaths.Root, "..", "..", "rules", "sharpdx-toolkit-map.json");

        var onDisk = ToolkitMap.Parse(File.ReadAllText(path));

        Assert.Equal(ToolkitMap.Default.MatrixFactories, onDisk.MatrixFactories);
        Assert.Equal(ToolkitMap.Default.StateCollections.Select(c => (c.Property, c.Type)), onDisk.StateCollections.Select(c => (c.Property, c.Type)));
    }

    [Fact]
    public void The_map_agrees_with_the_catalog_rules_it_serves()
    {
        var map = ToolkitMap.Default;

        Assert.Equal(Names("SDX030").Order(), map.MatrixFactories.Select(r => r.From).Order());
        Assert.Equal(Names("SDX031").Order(), map.MathUtilMembers.Select(r => r.From).Order());
        Assert.All(map.Namespaces, n =>
        {
            var match = Assert.IsType<NamespaceMatch>(Rule(n.RuleId).Match);
            Assert.Equal(n.From, match.Namespace);
            Assert.False(match.IncludeChildren);
            Assert.Equal(Classification.AutoFixable, Rule(n.RuleId).Classification);
        });
        Assert.All(map.StateCollections, c => Assert.Contains(c.Property, Names("SDX040")));
        Assert.All(map.StateSetters, s => Assert.Contains(s.Call, Names("SDX041")));
        Assert.Contains("BackBuffer", Names("SDX044"));
    }

    [Fact]
    public void Names_the_map_leaves_out_are_the_ones_the_design_says_are_marked()
    {
        var map = ToolkitMap.Default;

        Assert.DoesNotContain(map.StateCollections, c => c.Property == "RasterizerStates");
        Assert.DoesNotContain("Default", map.StateCollections.Single(c => c.Property == "BlendStates").Members);
        Assert.DoesNotContain("Default", map.StateCollections.Single(c => c.Property == "SamplerStates").Members);
        Assert.DoesNotContain(map.MatrixFactories, r => r.From.EndsWith("LH", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"version":2}""")]
    [InlineData("""{"version":1,"sources":[]}""")]
    [InlineData("""{"version":1,"sources":["https://x"],"namespaces":[{"ruleId":"SDX020","from":"A"}]}""")]
    [InlineData("""{"version":1,"sources":["https://x"],"namespaces":[],"matrixFactories":[{"from":"","to":"B"}]}""")]
    public void A_map_with_the_wrong_shape_is_refused(string json)
    {
        Assert.Throws<FormatException>(() => ToolkitMap.Parse(json));
    }

    [MonoGameFact]
    public void Every_monogame_name_in_the_map_exists_in_monogame_3_8_6_1()
    {
        var map = ToolkitMap.Default;
        var matrix = DeviceCompat.MonoGameType("Microsoft.Xna.Framework.Matrix");
        var mathHelper = DeviceCompat.MonoGameType("Microsoft.Xna.Framework.MathHelper");
        var presentation = DeviceCompat.MonoGameType("Microsoft.Xna.Framework.Graphics.PresentationParameters");
        var device = DeviceCompat.MonoGameType("Microsoft.Xna.Framework.Graphics.GraphicsDevice");
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;

        Assert.All(map.MatrixFactories, r => Assert.True(matrix.GetMethods(flags).Any(m => m.Name == r.To), "Matrix." + r.To));
        Assert.All(map.MathUtilMembers, r => Assert.True(Has(mathHelper, r.To), "MathHelper." + r.To));
        Assert.All(map.BackBuffer, r => Assert.True(presentation.GetProperty(r.To, flags) is not null, "PresentationParameters." + r.To));
        Assert.All(map.StateSetters, s => Assert.True(device.GetProperty(s.Property, flags) is not null, "GraphicsDevice." + s.Property));
        Assert.Contains(device.GetMethods(flags), m => m.Name == "SetRenderTarget");
        foreach (var collection in map.StateCollections)
        {
            var type = DeviceCompat.MonoGameType("Microsoft.Xna.Framework.Graphics." + collection.Type);
            Assert.All(collection.Members, m => Assert.True(Has(type, m), collection.Type + "." + m));
        }

        Assert.All(map.Namespaces, n =>
        {
            Assert.NotNull(DeviceCompat.MonoGame.GetTypes().FirstOrDefault(t => t.Namespace == n.To));
            Assert.All(n.QualifiedTypes, q => Assert.True(DeviceCompat.MonoGame.GetType(n.To + "." + q) is not null, n.To + "." + q));
        });
    }

    private static bool Has(Type type, string name)
    {
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
        return type.GetField(name, flags) is not null || type.GetProperty(name, flags) is not null || type.GetMethods(flags).Any(m => m.Name == name);
    }
}
