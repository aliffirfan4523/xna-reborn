using System.Text.Json;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Shims;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator;

/// <summary>With shims on the migrated copy gets XnaReborn.Compat and the manifest lists every use. With shims off nothing changes.</summary>
[Trait("Category", "Migrator")]
public sealed class ShimMigrationTests
{
    private static readonly string _output = Path.Combine(Path.GetTempPath(), "xr-shim-never-written");

    private static MigrationOptions Options(bool shims) =>
        new(_output, MonoGameTarget.DesktopGL, shims, DryRun: true, new HashSet<string>(StringComparer.Ordinal));

    [Fact]
    public async Task Shims_on_adds_the_library_and_references_it_from_the_game_that_uses_storage()
    {
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna40-gamerservices"), Options(true));

        var paths = result.Changes.Select(c => c.RelativePath).ToList();
        Assert.Contains("XnaReborn.Compat/XnaReborn.Compat.csproj", paths);
        Assert.Contains("XnaReborn.Compat/Storage.cs", paths);
        Assert.Contains("XnaReborn.Compat/GamerServices.cs", paths);

        var csproj = result.Changes.Single(c => c.RelativePath == "Xna40GamerServices.csproj").NewText!;
        Assert.Contains("<ProjectReference Include=\"XnaReborn.Compat\\XnaReborn.Compat.csproj\" />", csproj, StringComparison.Ordinal);
        Assert.Contains("<Compile Remove=\"XnaReborn.Compat\\**\" />", csproj, StringComparison.Ordinal);

        // The game source is not rewritten for the shims: the original namespaces are kept.
        Assert.Equal(ChangeKind.Copied, result.Changes.Single(c => c.RelativePath == "Game1.cs").Kind);
    }

    [Fact]
    public async Task Shims_on_lists_every_use_in_the_manifest_json_and_markdown()
    {
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna40-gamerservices"), Options(true));

        var shims = result.Manifest.Shims;
        Assert.NotNull(shims);
        Assert.Equal("XnaReborn.Compat/XnaReborn.Compat.csproj", shims.Project);
        Assert.Equal(4, shims.Files.Count);
        Assert.Equal(["Xna40GamerServices.csproj"], shims.ReferencedBy);
        Assert.Equal(
            [
                ("Game1.cs", "Microsoft.Xna.Framework.GamerServices", "Gamer,GamerServicesComponent,Guide"),
                ("Game1.cs", "Microsoft.Xna.Framework.Storage", "StorageDevice"),
            ],
            shims.Uses.Select(u => (u.File, u.Namespace, string.Join(',', u.Types))));
        Assert.All(
            result.Manifest.Changes.Where(c => c.Path.StartsWith("XnaReborn.Compat/", StringComparison.Ordinal)),
            c => Assert.StartsWith("Shim:", c.Reason, StringComparison.Ordinal));

        using var json = JsonDocument.Parse(ManifestJsonWriter.Write(result.Manifest));
        Assert.Equal(2, json.RootElement.GetProperty("shims").GetProperty("uses").GetArrayLength());

        var markdown = ManifestMarkdownWriter.Write(result.Manifest, []);
        Assert.Contains("## Shims", markdown, StringComparison.Ordinal);
        Assert.Contains("SHIM:", markdown, StringComparison.Ordinal);
        Assert.Contains("| Game1.cs | Microsoft.Xna.Framework.Storage | StorageDevice |", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shims_off_changes_nothing()
    {
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna40-gamerservices"), Options(false));

        Assert.DoesNotContain(result.Changes, c => c.RelativePath.StartsWith("XnaReborn.Compat", StringComparison.Ordinal));
        Assert.DoesNotContain("Compat", result.Changes.Single(c => c.RelativePath == "Xna40GamerServices.csproj").NewText!, StringComparison.Ordinal);
        Assert.Null(result.Manifest.Shims);
        Assert.DoesNotContain("shims", ManifestJsonWriter.Write(result.Manifest), StringComparison.Ordinal);
        Assert.DoesNotContain("## Shims", ManifestMarkdownWriter.Write(result.Manifest, []), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shims_on_for_a_game_without_storage_use_adds_the_library_but_no_reference()
    {
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna40-basic"), Options(true));

        Assert.Contains(result.Changes, c => c.RelativePath == "XnaReborn.Compat/Storage.cs");
        Assert.Empty(result.Manifest.Shims!.Uses);
        Assert.Empty(result.Manifest.Shims.ReferencedBy);
        Assert.DoesNotContain("ProjectReference", result.Changes.Single(c => c.RelativePath.EndsWith(".csproj", StringComparison.Ordinal) && !c.RelativePath.StartsWith("XnaReborn.Compat", StringComparison.Ordinal)).NewText!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shims_on_adds_the_library_to_the_solution_and_references_only_projects_that_use_it()
    {
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("multi-project-sln"), Options(true));

        var sln = result.Changes.Single(c => c.RelativePath.EndsWith(".sln", StringComparison.Ordinal)).NewText!;
        Assert.Contains("XnaReborn.Compat\\XnaReborn.Compat.csproj", sln, StringComparison.Ordinal);
        Assert.Empty(result.Manifest.Shims!.ReferencedBy);

        var off = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("multi-project-sln"), Options(false));
        Assert.DoesNotContain("Compat", off.Changes.Single(c => c.RelativePath.EndsWith(".sln", StringComparison.Ordinal)).NewText!, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_options_keep_shims_off()
    {
        Assert.False(MigrationTestSupport.Options(_output).EnableShims);
        Assert.Equal("XnaReborn.Compat", ShimProjectGenerator.ProjectName);
    }
}
