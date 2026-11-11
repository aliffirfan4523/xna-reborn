using System.Text.Json;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Analyzer.Rules.Matchers;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Safety;

namespace XnaReborn.Tests.Rules;

/// <summary>The fileContent match type: schema, loader, matcher.</summary>
[Trait("Category", "Rules")]
public sealed class FileContentMatcherTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xnareborn-filecontent-" + Guid.NewGuid().ToString("N"));

    public FileContentMatcherTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static IReadOnlyList<string> Run(string directory, string glob, string regex, int maxBytes = FileContentMatch.DefaultMaxBytes) =>
        FileContentMatcher.Match(new FileContentMatch(glob, regex, maxBytes), new ReadOnlyFileSystem(), directory, CancellationToken.None);

    private string Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private static XnaReborn.Analyzer.Reading.ProjectReadResult EmptyProject() =>
        new(new XnaReborn.Core.Models.ProjectInfo("Game.csproj", "Game", true, XnaReborn.Core.Models.XnaVersion.V40, XnaReborn.Core.Models.XnaPlatform.Windows, null, null, [], [], [], [], []), new Dictionary<string, string>(), [], []);

    private static string RuleJson(string match) =>
        $$"""
        {"version":1,"name":"t","rules":[{"id":"XNA900","title":"t","severity":"Warning","classification":"Manual",
         "match":{{match}},"explanation":"e","resolution":"r"}]}
        """;

    /// <summary>A regex that matches the effect fixtures reports each file with its line.</summary>
    [Fact]
    public void Match_regex_found_in_effect_fixture_reports_file_and_line()
    {
        var found = Run(FixturePaths.Of("xna40-effects"), "**/*.fx", @"compile\s+vs_2_0");

        Assert.Equal(["Content/Sm2.fx:31", "Content/WithInclude.fx:17"], found);
    }

    /// <summary>A regex that is absent from the fixtures finds nothing.</summary>
    [Fact]
    public void Match_regex_absent_from_fixture_finds_nothing() =>
        Assert.Empty(Run(FixturePaths.Of("xna40-effects"), "**/*.fx", "ThisTextIsNotInAnyEffect"));

    /// <summary>A match that appears only inside a comment is ignored for .fx files.</summary>
    [Fact]
    public void Match_ignores_text_inside_comments_in_fx_files()
    {
        Write("a.fx", "// compile vs_2_0\n/* compile vs_2_0\n   compile vs_2_0 */\nfloat x;\n");

        Assert.Empty(Run(_root, "*.fx", "compile"));
    }

    /// <summary>Comments are only blanked for effect files, so the same text matches in a .xap file.</summary>
    [Fact]
    public void Match_keeps_comment_text_in_other_files()
    {
        Write("a.xap", "// Streaming = 1;\n");

        Assert.Equal(["a.xap:1"], Run(_root, "*.xap", "Streaming"));
    }

    /// <summary>Comment blanking keeps string literals and line numbers.</summary>
    [Fact]
    public void Match_keeps_include_strings_and_line_numbers()
    {
        Write("a.fx", "/* one\ntwo */\n#include \"//not-a-comment.fxh\"\n");

        Assert.Equal(["a.fx:3"], Run(_root, "*.fx", "not-a-comment"));
    }

    /// <summary>A file over maxBytes is skipped even when it would match.</summary>
    [Fact]
    public void Match_skips_files_over_maxBytes()
    {
        Write("big.fx", "compile " + new string(' ', 200));
        Write("small.fx", "compile");

        Assert.Equal(["small.fx:1"], Run(_root, "*.fx", "compile", maxBytes: 100));
    }

    /// <summary>A file with a NUL byte is treated as binary and skipped.</summary>
    [Fact]
    public void Match_skips_binary_files()
    {
        File.WriteAllBytes(Path.Combine(_root, "bin.fx"), [(byte)'c', (byte)'o', 0, (byte)'m']);

        Assert.Empty(Run(_root, "*.fx", "c"));
    }

    /// <summary>At most three lines per file are reported, each line once.</summary>
    [Fact]
    public void Match_reports_first_three_lines_per_file()
    {
        Write("a.fx", "x x\nx\nx\nx\n");

        Assert.Equal(["a.fx:1", "a.fx:2", "a.fx:3"], Run(_root, "*.fx", "x"));
    }

    /// <summary>The matcher leaves the fixture folder byte for byte unchanged.</summary>
    [Fact]
    public void Match_leaves_fixture_files_unchanged()
    {
        var root = FixturePaths.Of("xna40-effects");
        var before = FolderState.Capture(root);

        Run(root, "**/*", ".");

        Assert.Equal(before, FolderState.Capture(root));
    }

    /// <summary>The loader accepts a fileContent rule, fills in the default size limit and runs it through the engine.</summary>
    [Fact]
    public void Loader_accepts_fileContent_rule_and_engine_evaluates_it()
    {
        var catalog = RuleCatalogLoader.Parse(RuleJson("""{"type":"fileContent","glob":"**/*.fx","regex":"VPOS"}"""));
        var match = Assert.IsType<FileContentMatch>(Assert.Single(catalog.Rules).Match);
        Assert.Equal(FileContentMatch.DefaultMaxBytes, match.MaxBytes);

        var engine = new RuleEngine(catalog, new ReadOnlyFileSystem());
        var project = EmptyProject();
        var hit = engine.EvaluateProject(project, FixturePaths.Of("xna40-effects"), "p.csproj", CancellationToken.None);
        var miss = engine.EvaluateProject(project, FixturePaths.Of("xna40-basic"), "p.csproj", CancellationToken.None);

        var finding = Assert.Single(hit);
        Assert.Equal("Content/Vpos.fx:1", finding.Snippet);
        Assert.Empty(miss);
    }

    /// <summary>A fileContent rule without a regex is rejected, and so is a regex that does not compile.</summary>
    [Theory]
    [InlineData("""{"type":"fileContent","glob":"**/*.fx"}""", "regex")]
    [InlineData("""{"type":"fileContent","regex":"x"}""", "glob")]
    [InlineData("""{"type":"fileContent","glob":"*.fx","regex":"("}""", "not a valid regular expression")]
    [InlineData("""{"type":"fileContent","glob":"*.fx","regex":"x","maxBytes":0}""", "maxBytes")]
    public void Loader_rejects_invalid_fileContent_rules(string match, string expected)
    {
        var ex = Assert.Throws<RuleCatalogException>(() => RuleCatalogLoader.Parse(RuleJson(match)));

        Assert.Contains(ex.Errors, e => e.Contains(expected, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A fileContent match survives a JSON round trip.</summary>
    [Fact]
    public void Match_round_trips_through_json()
    {
        RuleMatch original = new FileContentMatch("**/*.fx", "VPOS", 2048);

        var json = JsonSerializer.Serialize(original, RuleCatalogJson.Options);
        var back = JsonSerializer.Deserialize<RuleMatch>(json, RuleCatalogJson.Options);

        Assert.Equal(original, back);
        Assert.Contains("\"type\":\"fileContent\"", json, StringComparison.Ordinal);
    }
}
