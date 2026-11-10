using VerifyXunit;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Projects;

namespace XnaReborn.Tests.Migrator;

/// <summary>The solution generator writes a deterministic classic .sln.</summary>
[Trait("Category", "Migrator")]
public class SolutionGeneratorTests
{
    private static readonly string[] _two = ["Library/Library.csproj", "Game/Game.csproj"];
    private readonly SolutionGenerator _generator = new();

    [Fact]
    public void Change_is_a_created_sln_named_after_the_solution()
    {
        var change = _generator.Generate("MyGame", _two);

        Assert.Equal("MyGame.sln", change.RelativePath);
        Assert.Equal(ChangeKind.Created, change.Kind);
        Assert.Null(change.OriginalText);
    }

    [Fact]
    public void Text_uses_crlf_only_and_has_no_bom()
    {
        var text = _generator.Generate("S", _two).NewText!;

        Assert.False(text.StartsWith('\uFEFF'));
        Assert.EndsWith("\r\n", text);
        Assert.Equal(text.Count(c => c == '\n'), text.Count(c => c == '\r'));
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void Projects_are_sorted_by_path_and_named_after_the_file()
    {
        var text = _generator.Generate("S", _two).NewText!;

        var game = text.IndexOf("\"Game\", \"Game\\Game.csproj\"", StringComparison.Ordinal);
        var library = text.IndexOf("\"Library\", \"Library\\Library.csproj\"", StringComparison.Ordinal);
        Assert.True(game >= 0 && library > game);
        Assert.DoesNotContain("/", text);
    }

    [Fact]
    public void Backslash_input_is_accepted_and_gives_the_same_text()
    {
        var a = _generator.Generate("S", ["Game/Game.csproj"]).NewText;
        var b = _generator.Generate("S", ["Game\\Game.csproj"]).NewText;

        Assert.Equal(a, b);
    }

    [Fact]
    public void Only_any_cpu_debug_and_release_are_written()
    {
        var text = _generator.Generate("S", _two).NewText!;

        Assert.Contains("Debug|Any CPU = Debug|Any CPU", text);
        Assert.Contains("Release|Any CPU = Release|Any CPU", text);
        Assert.DoesNotContain("x86", text);
        Assert.DoesNotContain("x64", text);
        Assert.Contains("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", text);
    }

    [Fact]
    public void Guids_are_uppercase_stable_and_unique()
    {
        var first = SolutionGenerator.ProjectGuid("Game/Game.csproj");

        Assert.Equal(first, SolutionGenerator.ProjectGuid("Game/Game.csproj"));
        Assert.Equal(first, SolutionGenerator.ProjectGuid("game\\GAME.csproj"));
        Assert.NotEqual(first, SolutionGenerator.ProjectGuid("Library/Library.csproj"));
        Assert.Equal(first.ToUpperInvariant(), first);
        Assert.Matches("^\\{[0-9A-F]{8}-[0-9A-F]{4}-5[0-9A-F]{3}-[89AB][0-9A-F]{3}-[0-9A-F]{12}\\}$", first);
    }

    [Fact]
    public void Guids_do_not_depend_on_project_order()
    {
        var a = _generator.Generate("S", ["A/A.csproj", "B/B.csproj"]).NewText;
        var b = _generator.Generate("S", ["B/B.csproj", "A/A.csproj"]).NewText;

        Assert.Equal(a, b);
    }

    [Fact]
    public void Two_calls_give_equal_results()
    {
        Assert.Equal(_generator.Generate("S", _two), _generator.Generate("S", _two));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Empty_name_throws(string name) =>
        Assert.Throws<ArgumentException>(() => _generator.Generate(name, _two));

    [Fact]
    public void Empty_project_list_throws() =>
        Assert.Throws<ArgumentException>(() => _generator.Generate("S", []));

    [Fact]
    public async Task Two_projects_match_snapshot() =>
        await Verifier.Verify(_generator.Generate("MultiProject", _two).NewText!, "sln");

    [Fact]
    public async Task Single_project_matches_snapshot() =>
        await Verifier.Verify(_generator.Generate("Basic", ["Basic.csproj"]).NewText!, "sln");
}
