using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Analyzer.Rules.Matchers;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Rules;

/// <summary>The spriteFontFamily match type: reads FontName from .spritefont files.</summary>
[Trait("Category", "Rules")]
public sealed class SpriteFontMatcherTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> _noMetadata = new Dictionary<string, string>();

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-sf-" + Guid.NewGuid().ToString("N"));

    public SpriteFontMatcherTests() => Directory.CreateDirectory(_temp);

    public void Dispose() => Directory.Delete(_temp, true);

    private static string FontXml(string name) =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <XnaContent xmlns:Graphics="Microsoft.Xna.Framework.Content.Pipeline.Graphics">
          <Asset Type="Graphics:FontDescription">
            <FontName>{name}</FontName>
            <Size>14</Size>
          </Asset>
        </XnaContent>
        """;

    private static ProjectReadResult Project(params ProjectItem[] items)
    {
        var info = new ProjectInfo("Game.csproj", "Game", true, XnaVersion.V40, XnaPlatform.Windows, null, null, [], [], [], [], []);
        return new ProjectReadResult(info, new Dictionary<string, string>(), items, []);
    }

    private static IReadOnlyList<string> Run(string directory, ProjectReadResult project, CancellationToken token = default, params string[] families) =>
        SpriteFontMatcher.Match(new SpriteFontFamilyMatch(families), project, new ReadOnlyFileSystem(), directory, token);

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_temp, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static async Task<ProjectReadResult> ReadFixture(string fixture, string projectFile)
    {
        var root = FixturePaths.Of(fixture);
        return await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(root, projectFile), root, CancellationToken.None);
    }

    [Fact]
    public async Task Pericles_in_custom_content_fixture_is_found()
    {
        var project = await ReadFixture("xna40-custom-content", "CustomContent.csproj");

        var result = Run(FixturePaths.Of("xna40-custom-content"), project, default, "Pericles", "Pescadero");

        Assert.Equal(["Content/Fonts/Hud.spritefont: Pericles"], result);
    }

    [Fact]
    public async Task Arial_in_basic_fixture_does_not_match()
    {
        var project = await ReadFixture("xna40-basic", "Xna40Basic.csproj");

        Assert.Empty(Run(FixturePaths.Of("xna40-basic"), project, default, "Pericles", "Pescadero"));
        Assert.Single(Run(FixturePaths.Of("xna40-basic"), project, default, "Arial"));
    }

    [Fact]
    public async Task Engine_gives_one_finding_for_a_spriteFontFamily_rule()
    {
        var project = await ReadFixture("xna40-custom-content", "CustomContent.csproj");
        var rule = new Rule("XNA900", "t", Severity.Warning, Classification.Manual, null, null, new SpriteFontFamilyMatch(["Pericles"]), "why", "how", null);
        var engine = new RuleEngine(new RuleCatalog(1, "test", null, [rule]), new ReadOnlyFileSystem());

        var finding = Assert.Single(engine.EvaluateProject(project, FixturePaths.Of("xna40-custom-content"), "p.csproj", CancellationToken.None));

        Assert.Equal("Content/Fonts/Hud.spritefont: Pericles", finding.Snippet);
        Assert.Empty(engine.SourceRules);
    }

    [Fact]
    public void Family_comparison_ignores_case_and_trims_the_font_name()
    {
        Write("A.spritefont", FontXml("  segoe ui MONO  "));

        Assert.Equal(["A.spritefont: segoe ui MONO"], Run(_temp, Project(), default, "Segoe UI Mono"));
    }

    [Fact]
    public void Any_of_several_families_matches()
    {
        Write("A.spritefont", FontXml("Pescadero"));
        Write("B.spritefont", FontXml("Arial"));
        Write("Sub/C.spritefont", FontXml("Pericles"));

        var result = Run(_temp, Project(), default, "Pericles", "Pescadero");

        Assert.Equal(["A.spritefont: Pescadero", "Sub/C.spritefont: Pericles"], result);
    }

    [Fact]
    public void Content_project_outside_the_game_folder_is_scanned()
    {
        var game = Path.Combine(_temp, "Game");
        Directory.CreateDirectory(game);
        Write(Path.Combine("SharedContent", "Fonts", "Hud.spritefont"), FontXml("Pescadero"));
        var project = Project(new ProjectItem("NestedContentProject", @"..\SharedContent\SharedContent.contentproj", _noMetadata));

        var result = Run(game, project, default, "Pescadero");

        Assert.Equal(["../SharedContent/Fonts/Hud.spritefont: Pescadero"], result);
    }

    [Fact]
    public void Content_project_inside_the_game_folder_does_not_report_files_twice()
    {
        Write(Path.Combine("Game", "Content", "Hud.spritefont"), FontXml("Pescadero"));
        var game = Path.Combine(_temp, "Game");
        var project = Project(
            new ProjectItem("NestedContentProject", @"Content\Content.contentproj", _noMetadata),
            new ProjectItem("NestedContentProject", @"Content\Other.contentproj", _noMetadata));

        Assert.Equal(["Content/Hud.spritefont: Pescadero"], Run(game, project, default, "Pescadero"));
    }

    [Fact]
    public void Bin_obj_git_and_vs_folders_are_skipped()
    {
        foreach (var folder in new[] { "bin", "obj", ".git", ".vs", "Sub/bin" })
        {
            Write(Path.Combine(folder, "X.spritefont"), FontXml("Pescadero"));
        }

        Assert.Empty(Run(_temp, Project(), default, "Pescadero"));
    }

    [Fact]
    public void Invalid_xml_and_missing_font_name_are_skipped()
    {
        Write("Bad.spritefont", "<XnaContent><FontName>Pescadero");
        Write("Empty.spritefont", string.Empty);
        Write("NoName.spritefont", "<XnaContent><Size>1</Size></XnaContent>");
        Write("Good.spritefont", FontXml("Pescadero"));

        Assert.Equal(["Good.spritefont: Pescadero"], Run(_temp, Project(), default, "Pescadero"));
    }

    [Fact]
    public void Missing_content_folder_is_skipped()
    {
        var project = Project(new ProjectItem("NestedContentProject", @"Nope\Content.contentproj", _noMetadata));

        Assert.Empty(Run(_temp, project, default, "Pescadero"));
    }

    [Fact]
    public void File_deleted_after_listing_is_skipped()
    {
        Write("Good.spritefont", FontXml("Pescadero"));
        var fileSystem = new VanishingFileSystem(Path.Combine(_temp, "Ghost.spritefont"));

        var result = SpriteFontMatcher.Match(new SpriteFontFamilyMatch(["Pescadero"]), Project(), fileSystem, _temp, CancellationToken.None);

        Assert.Equal(["Good.spritefont: Pescadero"], result);
    }

    [Fact]
    public void Snippet_lists_three_in_path_order_then_counts_the_rest()
    {
        foreach (var name in new[] { "e", "b", "a", "d", "c" })
        {
            Write(name + ".spritefont", FontXml("Pescadero"));
        }

        var rule = new Rule("XNA900", "t", Severity.Warning, Classification.Manual, null, null, new SpriteFontFamilyMatch(["Pescadero"]), "why", "how", null);
        var engine = new RuleEngine(new RuleCatalog(1, "test", null, [rule]), new ReadOnlyFileSystem());

        var finding = Assert.Single(engine.EvaluateProject(Project(), _temp, "p.csproj", CancellationToken.None));

        Assert.Equal("a.spritefont: Pescadero, b.spritefont: Pescadero, c.spritefont: Pescadero +2 more", finding.Snippet);
    }

    [Fact]
    public void Cancelled_token_stops_the_scan()
    {
        Write("A.spritefont", FontXml("Pescadero"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() => Run(_temp, Project(), source.Token, "Pescadero"));
    }

    /// <summary>Lists one extra file that cannot be opened, as when a file is deleted after the listing.</summary>
    private sealed class VanishingFileSystem(string ghost) : IReadOnlyFileSystem
    {
        private readonly ReadOnlyFileSystem _inner = new();

        public bool FileExists(string path) => _inner.FileExists(path);

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern)
        {
            var files = _inner.EnumerateFiles(directory, searchPattern).ToList();
            if (string.Equals(Path.GetDirectoryName(ghost), directory, StringComparison.OrdinalIgnoreCase))
            {
                files.Add(ghost);
            }

            return files;
        }

        public IReadOnlyList<string> EnumerateDirectories(string directory) => _inner.EnumerateDirectories(directory);

        public Stream OpenRead(string path) => _inner.OpenRead(path);

        public string ReadAllText(string path) => _inner.ReadAllText(path);
    }
}
