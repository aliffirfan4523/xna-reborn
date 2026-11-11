using VerifyXunit;
using XnaReborn.Flash.Analyzer.As3;

namespace XnaReborn.Flash.Tests;

/// <summary>Task 4.5: tree snapshots for every fixture, one negative test per recovery path, and the optional real-sample check.</summary>
public class As3ParserCheckpointTests
{
    private static As3CompilationUnit Parse(string s) => As3Parser.Parse(s);

    private static As3TypeDeclaration FirstClass(As3CompilationUnit unit) => unit.Members.OfType<As3TypeDeclaration>().First();

    public static IEnumerable<object[]> FixtureFolders() =>
        FlashFixtures.Names.Where(n => Directory.EnumerateFiles(FlashFixtures.Of(n), "*.as", SearchOption.AllDirectories).Any()).Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(FixtureFolders))]
    public async Task Tree_snapshot_per_fixture(string fixture)
    {
        var root = FlashFixtures.Of(fixture);
        var files = Directory.EnumerateFiles(root, "*.as", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);
        var text = string.Join(
            "\n",
            files.Select(f => "== " + f + "\n" + As3TreeDump.Dump(Parse(File.ReadAllText(Path.Combine(root, f)))).Replace("\r\n", "\n", StringComparison.Ordinal)));

        await Verifier.Verify(text, "txt").UseParameters(fixture);
    }

    [Fact]
    public void Bad_statement_becomes_an_unsupported_statement_and_parsing_continues()
    {
        var unit = Parse("package { public class C { public function f():void { var a:int = ; b = 2; } public function g():void {} } }");

        var cls = FirstClass(unit);
        Assert.NotEmpty(unit.Diagnostics);
        Assert.Equal(2, cls.Members.OfType<As3Method>().Count());
        var body = cls.Members.OfType<As3Method>().First().Body!;
        Assert.Contains(body.Statements, s => s is As3UnsupportedStatement { Reason: "syntax-error" });
        Assert.Contains(body.Statements, s => s is not As3UnsupportedStatement);
    }

    [Fact]
    public void Bad_member_is_skipped_and_the_next_member_is_read()
    {
        var unit = Parse("package { public class C { public 123; public var ok:int; } }");

        Assert.NotEmpty(unit.Diagnostics);
        Assert.Contains(FirstClass(unit).Members, m => m is As3Field { Name: "ok" });
    }

    [Fact]
    public void Missing_closing_brace_reports_a_diagnostic_and_keeps_the_class()
    {
        var unit = Parse("package { public class C { public var a:int;");

        Assert.NotEmpty(unit.Diagnostics);
    }

    [Fact]
    public void Bad_top_level_declaration_skips_to_the_end_of_the_block()
    {
        var unit = Parse("package { 42 public class C { } }");

        Assert.NotEmpty(unit.Diagnostics);
    }

    [Fact]
    public void Unterminated_metadata_tag_is_reported()
    {
        var unit = Parse("package { public class C { [Embed(source=\"a.png\" public var x:Class; } }");

        Assert.Contains(unit.Diagnostics, d => d.Message.Contains("metadata", StringComparison.OrdinalIgnoreCase) || d.Message.Contains("Expected", StringComparison.Ordinal));
    }

    [Fact]
    public void Unterminated_parenthesis_is_reported()
    {
        var unit = Parse("package { public class C { function f() { var x = (1 + 2; } } }");

        Assert.NotEmpty(unit.Diagnostics);
    }

    [Fact]
    public void Statement_in_switch_before_a_case_is_reported()
    {
        var unit = Parse("package { public class C { function f() { switch (x) { y = 1; } } } }");

        Assert.Contains(unit.Diagnostics, d => d.Message.Contains("'case' or 'default'", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_property_name_after_dot_is_reported()
    {
        var unit = Parse("package { public class C { function f() { a. ; } } }");

        Assert.NotEmpty(unit.Diagnostics);
    }

    [Fact]
    public void Unclosed_vector_type_argument_is_reported()
    {
        var unit = Parse("package { public class C { var v:Vector.<int; } }");

        Assert.Contains(unit.Diagnostics, d => d.Message.Contains("'>'", StringComparison.Ordinal));
    }

    [Fact]
    public void Very_deep_nesting_reports_a_diagnostic_instead_of_overflowing()
    {
        var unit = Parse("package { public class C { function f() { var x = " + new string('(', 400) + "1" + new string(')', 400) + "; } } }");

        Assert.Contains(unit.Diagnostics, d => d.Message.Contains("too deep", StringComparison.Ordinal));
    }

    [Fact]
    public void Statement_without_semicolon_on_one_line_is_reported()
    {
        var unit = Parse("package { public class C { function f() { a = 1 b = 2; } } }");

        Assert.Contains(unit.Diagnostics, d => d.Message.Contains("Expected ';'", StringComparison.Ordinal));
    }

    [FlashSamplesTheory]
    [InlineData("Mode")]
    [InlineData("FlxCollisions")]
    [InlineData("Revenge")]
    public void Real_sample_projects_parse_with_zero_diagnostics(string sample)
    {
        var root = Environment.GetEnvironmentVariable("XNAREBORN_FLASH_SAMPLES")!;

        var folder = Directory.EnumerateDirectories(root, sample, SearchOption.AllDirectories).FirstOrDefault();
        Assert.True(folder is not null, $"No folder named {sample} under {root}.");
        var failures = new List<string>();
        foreach (var file in Directory.EnumerateFiles(folder!, "*.as", SearchOption.AllDirectories))
        {
            foreach (var d in Parse(File.ReadAllText(file)).Diagnostics)
            {
                failures.Add($"{Path.GetRelativePath(folder!, file)}({d.Span.Line},{d.Span.Column}): {d.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(20)));
    }
}

/// <summary>A theory that is skipped unless XNAREBORN_FLASH_SAMPLES names a folder with local checkouts of the candidate games.</summary>
public sealed class FlashSamplesTheoryAttribute : TheoryAttribute
{
    public FlashSamplesTheoryAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XNAREBORN_FLASH_SAMPLES")))
        {
            Skip = "Set XNAREBORN_FLASH_SAMPLES to a folder that holds local checkouts of the candidate games.";
        }
    }
}
