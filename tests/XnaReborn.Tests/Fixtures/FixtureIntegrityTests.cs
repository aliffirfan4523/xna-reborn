using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace XnaReborn.Tests.Fixtures;

/// <summary>Checks that the fixture projects are well formed, self-consistent, and properly attributed.</summary>
public partial class FixtureIntegrityTests
{
    private static readonly string[] _xmlExtensions = [".csproj", ".contentproj", ".spritefont"];

    [GeneratedRegex(@"^Project\(""\{[^}]+\}""\) = ""[^""]+"", ""(?<path>[^""]+)"", ""\{(?<guid>[^}]+)\}""", RegexOptions.Multiline)]
    private static partial Regex SolutionProjectLine();

    public static IEnumerable<object[]> NameRows() => FixturePaths.NameRows();

    private static IEnumerable<string> Files(string name) =>
        Directory.EnumerateFiles(FixturePaths.Of(name), "*", SearchOption.AllDirectories);

    private static IEnumerable<string> ProjectFiles(string name) =>
        Files(name).Where(f => f.EndsWith(".csproj", StringComparison.Ordinal) || f.EndsWith(".contentproj", StringComparison.Ordinal));

    private static string Read(string name, string relative) =>
        File.ReadAllText(Path.Combine(FixturePaths.Of(name), relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string ResolveFromProject(string projectFile, string include) =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFile)!, include.Replace('\\', Path.DirectorySeparatorChar)));

    [Fact]
    public void Fixture_list_matches_folders_on_disk()
    {
        var onDisk = Directory.GetDirectories(FixturePaths.Root).Select(d => Path.GetFileName(d)!).Order(StringComparer.Ordinal);
        Assert.Equal(FixturePaths.AllNames.Order(StringComparer.Ordinal), onDisk);
    }

    [Theory]
    [MemberData(nameof(NameRows))]
    public void Readme_states_origin_and_license(string name)
    {
        var readme = Read(name, "README.md");

        Assert.Contains("original", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("synthetic", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("third-party", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("license", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(NameRows))]
    public void Files_are_text_without_nul_bytes(string name)
    {
        foreach (var file in Files(name))
        {
            Assert.DoesNotContain((byte)0, File.ReadAllBytes(file));
        }
    }

    [Theory]
    [MemberData(nameof(NameRows))]
    public void Xml_files_are_well_formed(string name)
    {
        foreach (var file in Files(name).Where(f => _xmlExtensions.Contains(Path.GetExtension(f))))
        {
            var ex = Record.Exception(() => XDocument.Load(file));
            Assert.True(ex is null, $"{file}: {ex?.Message}");
        }
    }

    [Theory]
    [MemberData(nameof(NameRows))]
    public void Compile_and_project_items_point_to_files_that_exist(string name)
    {
        foreach (var project in ProjectFiles(name))
        {
            var doc = XDocument.Load(project);
            var items = doc.Descendants()
                .Where(e => e.Name.LocalName is "Compile" or "NestedContentProject" or "ProjectReference")
                .Select(e => (string?)e.Attribute("Include"))
                .OfType<string>();

            foreach (var include in items)
            {
                var target = ResolveFromProject(project, include);
                Assert.True(File.Exists(target), $"{project} includes missing file {include}");
            }
        }
    }

    [Fact]
    public void Solution_lists_three_existing_projects_and_one_missing_with_matching_guids()
    {
        var root = FixturePaths.Of("multi-project-sln");
        var text = Read("multi-project-sln", "MultiProject.sln");
        var entries = SolutionProjectLine().Matches(text)
            .Select(m => (Path: m.Groups["path"].Value, Guid: m.Groups["guid"].Value))
            .ToList();

        Assert.Equal(4, entries.Count);

        var missing = Assert.Single(entries, e => !File.Exists(Path.Combine(root, e.Path.Replace('\\', Path.DirectorySeparatorChar))));
        Assert.Equal(@"Missing\Missing.csproj", missing.Path);

        foreach (var entry in entries.Where(e => e != missing))
        {
            var projectFile = Path.Combine(root, entry.Path.Replace('\\', Path.DirectorySeparatorChar));
            var guid = XDocument.Load(projectFile).Descendants().Single(e => e.Name.LocalName == "ProjectGuid").Value;
            Assert.Equal("{" + entry.Guid + "}", guid, ignoreCase: true);
        }
    }

    [Theory]
    [InlineData("xna40-basic", "v4.0")]
    [InlineData("xna40-gamerservices", "v4.0")]
    [InlineData("xna40-custom-content", "v4.0")]
    [InlineData("xna31-legacy", "v3.1")]
    public void Xna_fixtures_declare_their_framework_version(string name, string version)
    {
        var project = ProjectFiles(name).Single(f => f.EndsWith(".csproj", StringComparison.Ordinal) && !f.Contains("Content" + Path.DirectorySeparatorChar));
        var declared = XDocument.Load(project).Descendants().First(e => e.Name.LocalName == "XnaFrameworkVersion").Value;

        Assert.Equal(version, declared);
    }

    [Fact]
    public void Gamerservices_fixture_references_gamerservices()
    {
        var csproj = Read("xna40-gamerservices", "Xna40GamerServices.csproj");
        Assert.Contains("Microsoft.Xna.Framework.GamerServices", csproj);

        var code = Read("xna40-gamerservices", "Game1.cs");
        Assert.Contains("Guide.ShowSignIn", code);
    }

    [Fact]
    public void Custom_content_fixture_has_custom_processor_xact_and_xna_only_font()
    {
        var content = Read("xna40-custom-content", "Content/Content.contentproj");
        Assert.Contains("OutlineProcessor", content);
        Assert.Contains("Sounds.xap", content);
        Assert.Contains("MyPipeline.dll", content);
        Assert.Contains("Pericles", Read("xna40-custom-content", "Content/Fonts/Hud.spritefont"));
    }

    [Fact]
    public void Basic_fixture_has_no_gamerservices_or_legacy_apis()
    {
        var code = Read("xna40-basic", "Game1.cs");
        Assert.DoesNotContain("GamerServices", code);
        Assert.DoesNotContain("SpriteBlendMode", code);
        Assert.DoesNotContain("RenderState", code);
    }

    [Fact]
    public void Not_xna_fixture_has_no_xna_text_in_project_or_code()
    {
        Assert.DoesNotContain("Xna.Framework", Read("not-xna", "NotXna.csproj"));
        Assert.DoesNotContain("Xna.Framework", Read("not-xna", "Program.cs"));
    }

    [Fact]
    public void Empty_folder_fixture_has_no_project_or_solution_files()
    {
        var names = Files("empty-folder").Select(Path.GetFileName).ToList();

        Assert.DoesNotContain(names, n => n!.EndsWith(".csproj", StringComparison.Ordinal) || n.EndsWith(".sln", StringComparison.Ordinal));
        Assert.Contains(".gitkeep", names);
    }
}
