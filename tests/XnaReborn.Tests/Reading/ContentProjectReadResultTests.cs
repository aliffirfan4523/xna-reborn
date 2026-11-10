using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Reading;

/// <summary>The computed members of <see cref="ContentProjectReadResult"/>.</summary>
public sealed class ContentProjectReadResultTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xnareborn-crr-" + Guid.NewGuid().ToString("N"));

    public ContentProjectReadResultTests() => Directory.CreateDirectory(_temp);

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private ContentProjectReadResult ReadXml(string xml)
    {
        var path = Path.Combine(_temp, "Content.contentproj");
        File.WriteAllText(path, xml);
        return new ContentProjectReader(new ReadOnlyFileSystem()).Read(path, _temp);
    }

    private static ContentProjectReadResult ReadFixture(string name)
    {
        var root = FixturePaths.Of(name);
        return new ContentProjectReader(new ReadOnlyFileSystem()).Read(Path.Combine(root, "Content", "Content.contentproj"), root);
    }

    [Fact]
    public void Custom_fixture_exposes_profile_root_and_the_custom_pipeline_reference()
    {
        var result = ReadFixture("xna40-custom-content");

        Assert.Equal("HiDef", result.Profile);
        Assert.Equal("Content", result.ContentRootDirectory);
        var custom = Assert.Single(result.PipelineReferences);
        Assert.Equal("MyPipeline", custom.Include);
        Assert.Equal("..\\Pipeline\\MyPipeline.dll", custom.Metadata["HintPath"]);
    }

    [Fact]
    public void Basic_fixture_exposes_profile_and_has_no_custom_references()
    {
        var result = ReadFixture("xna40-basic");

        Assert.Equal("HiDef", result.Profile);
        Assert.Equal("Content", result.ContentRootDirectory);
        Assert.Empty(result.PipelineReferences);
    }

    [Theory]
    [InlineData("<ContentRootDirectory> Assets\\Art\\ </ContentRootDirectory>", "Assets/Art")]
    [InlineData("<ContentRootDirectory>Assets/</ContentRootDirectory>", "Assets")]
    [InlineData("<ContentRootDirectory>  </ContentRootDirectory>", "Content")]
    [InlineData("", "Content")]
    public void Content_root_is_trimmed_normalized_and_defaults_to_Content(string property, string expected)
    {
        var result = ReadXml($"<Project><PropertyGroup>{property}</PropertyGroup></Project>");

        Assert.Equal(expected, result.ContentRootDirectory);
    }

    [Theory]
    [InlineData("<XnaProfile> Reach </XnaProfile>", "Reach")]
    [InlineData("<XnaProfile></XnaProfile>", null)]
    [InlineData("", null)]
    public void Profile_is_trimmed_or_null(string property, string? expected)
    {
        var result = ReadXml($"<Project><PropertyGroup>{property}</PropertyGroup></Project>");

        Assert.Equal(expected, result.Profile);
    }

    [Fact]
    public void Pipeline_references_exclude_stock_assemblies_by_simple_name()
    {
        var result = ReadXml("""
            <Project><ItemGroup>
              <Reference Include="Microsoft.Xna.Framework.Content.Pipeline.TextureImporter, Version=4.0.0.0, Culture=neutral" />
              <Reference Include="microsoft.xna.framework.content.pipeline.EffectImporter" />
              <Reference Include="Custom.Importers, Version=1.0.0.0"><HintPath>libs\Custom.Importers.dll</HintPath></Reference>
              <Reference Include="Microsoft.Xna.Framework.Content.PipelineExtras" />
            </ItemGroup></Project>
            """);

        Assert.Equal(["Custom.Importers, Version=1.0.0.0"], result.PipelineReferences.Select(r => r.Include).ToArray());
        Assert.Equal("libs\\Custom.Importers.dll", result.PipelineReferences[0].Metadata["HintPath"]);
    }

    [Fact]
    public void Failed_read_gives_defaults()
    {
        var result = new ContentProjectReader(new ReadOnlyFileSystem()).Read(Path.Combine(_temp, "missing.contentproj"), _temp);

        Assert.Equal("Content", result.ContentRootDirectory);
        Assert.Null(result.Profile);
        Assert.Empty(result.PipelineReferences);
    }
}
