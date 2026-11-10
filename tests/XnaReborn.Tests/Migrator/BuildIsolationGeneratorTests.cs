using System.Xml.Linq;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Projects;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator;

/// <summary>The isolation files stop a parent folder's MSBuild settings from reaching the migrated copy.</summary>
[Trait("Category", "Migrator")]
public sealed class BuildIsolationGeneratorTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-isolation-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    [Fact]
    public void Generates_three_created_files_at_the_root()
    {
        var changes = new BuildIsolationGenerator().Generate();

        Assert.Equal(["Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"], changes.Select(c => c.RelativePath));
        Assert.All(changes, c =>
        {
            Assert.Equal(ChangeKind.Created, c.Kind);
            Assert.Null(c.OriginalText);
            Assert.False(string.IsNullOrWhiteSpace(c.Reason));
        });
    }

    [Theory]
    [InlineData("Directory.Build.props")]
    [InlineData("Directory.Build.targets")]
    public void Build_files_are_an_empty_project_with_a_comment(string path)
    {
        var text = Get(path).NewText!;
        var root = XDocument.Parse(text, LoadOptions.PreserveWhitespace).Root!;

        Assert.Equal("Project", root.Name.LocalName);
        Assert.Empty(root.Elements());
        Assert.Contains(root.Nodes(), n => n is XComment c && c.Value.Contains("parent folder", StringComparison.Ordinal));
        Assert.Equal("<Project>\n  <!-- Stops MSBuild from importing the same file from a parent folder into this migrated project. -->\n</Project>\n", text);
    }

    [Fact]
    public void Packages_file_turns_central_package_management_off()
    {
        var text = Get("Directory.Packages.props").NewText!;
        var root = XDocument.Parse(text).Root!;

        var property = Assert.Single(root.Elements("PropertyGroup").Elements());
        Assert.Equal("ManagePackageVersionsCentrally", property.Name.LocalName);
        Assert.Equal("false", property.Value);
        Assert.Contains(root.Nodes(), n => n is XComment);
    }

    [Fact]
    public void Files_use_lf_one_trailing_newline_and_no_bom()
    {
        foreach (var change in new BuildIsolationGenerator().Generate())
        {
            var text = change.NewText!;
            Assert.DoesNotContain('\r', text);
            Assert.DoesNotContain('\uFEFF', text);
            Assert.EndsWith("</Project>\n", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\n\n", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Generation_is_deterministic()
    {
        var generator = new BuildIsolationGenerator();
        Assert.Equal(generator.Generate(), generator.Generate());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migrator_includes_the_files_also_in_a_dry_run(bool dryRun)
    {
        var output = Path.Combine(_temp, "out");
        var result = await MigrationTestSupport.MigrateAsync(FixturePaths.Of("xna31-rewritable"), MigrationTestSupport.Options(output, dryRun));

        foreach (var path in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" })
        {
            var change = Assert.Single(result.Changes, c => c.RelativePath == path);
            Assert.Equal(ChangeKind.Created, change.Kind);
        }

        Assert.Equal(!dryRun, File.Exists(Path.Combine(output, "Directory.Packages.props")));
    }

    [Fact]
    public async Task A_same_named_file_in_the_source_root_never_replaces_the_generated_one()
    {
        // The generated files claim their paths first, so a source file with the same name cannot win.
        var source = Path.Combine(_temp, "src");
        MigrationTestSupport.CopyFolder(FixturePaths.Of("xna31-rewritable"), source);
        File.WriteAllText(Path.Combine(source, "Directory.Build.props"), "<Project><PropertyGroup><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");

        var output = Path.Combine(_temp, "out");
        var result = await MigrationTestSupport.MigrateAsync(source, MigrationTestSupport.Options(output));

        var change = Assert.Single(result.Changes, c => c.RelativePath == "Directory.Build.props");
        Assert.Equal(ChangeKind.Created, change.Kind);
        Assert.DoesNotContain("TreatWarningsAsErrors", File.ReadAllText(Path.Combine(output, "Directory.Build.props")), StringComparison.Ordinal);
    }

    private static XnaReborn.Migrator.Output.FileChange Get(string path) =>
        new BuildIsolationGenerator().Generate().Single(c => c.RelativePath == path);
}
