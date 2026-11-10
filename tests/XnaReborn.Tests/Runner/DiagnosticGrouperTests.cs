using XnaReborn.Runner.Diagnostics;
using XnaReborn.Runner.Hints;

namespace XnaReborn.Tests.Runner;

/// <summary>Task 5.3: de-duplication and grouping of diagnostics.</summary>
[Trait("Category", "Runner")]
public sealed class DiagnosticGrouperTests
{
    private static readonly FixHint _hintA = new("H001", "A", "Do A.", "XNA009");
    private static readonly FixHint _hintB = new("H002", "B", "Do B.", null);

    private static BuildDiagnostic Error(string code, string message = "m", string? file = "A.cs", int? line = 1) =>
        new(BuildDiagnosticSeverity.Error, code, message, file, line, 1, "G.csproj", null);

    private static BuildDiagnostic Warning(string code, string message = "m") =>
        new(BuildDiagnosticSeverity.Warning, code, message, "A.cs", 1, 1, "G.csproj", null);

    private static FixHint? NoHints(BuildDiagnostic _) => null;

    [Fact]
    public void Empty_input_gives_no_groups()
    {
        Assert.Empty(DiagnosticGrouper.Group([], NoHints));
    }

    [Fact]
    public void Exact_repeats_are_removed()
    {
        var groups = DiagnosticGrouper.Group([Error("CS0246"), Error("CS0246"), Error("CS0246")], NoHints);

        var group = Assert.Single(groups);
        Assert.Equal(1, group.Count);
    }

    [Fact]
    public void Diagnostics_that_differ_in_any_field_are_kept()
    {
        var baseline = Error("CS0246");
        var variants = new[]
        {
            baseline,
            baseline with { Severity = BuildDiagnosticSeverity.Warning },
            baseline with { Message = "other" },
            baseline with { File = "B.cs" },
            baseline with { Line = 2 },
            baseline with { Column = 2 },
            baseline with { Project = "H.csproj" },
            baseline with { AssetPath = "a.png" },
        };

        var group = Assert.Single(DiagnosticGrouper.Group(variants, NoHints));

        Assert.Equal(variants.Length, group.Count);
    }

    [Fact]
    public void Diagnostics_with_the_same_hint_share_a_group_even_with_different_codes()
    {
        var groups = DiagnosticGrouper.Group(
            [Error("CS0234", "GamerServices a"), Error("CS0246", "GamerServices b"), Error("CS1061", "other")],
            d => d.Message.StartsWith("GamerServices", StringComparison.Ordinal) ? _hintA : _hintB);

        Assert.Equal(2, groups.Count);
        Assert.Equal(_hintA, groups[0].Hint);
        Assert.Equal(["CS0234", "CS0246"], groups[0].Items.Select(d => d.Code));
        Assert.Equal(_hintB, groups[1].Hint);
    }

    [Fact]
    public void Diagnostics_without_a_hint_form_one_group_per_code()
    {
        var groups = DiagnosticGrouper.Group(
            [Error("CS0246", "a"), Error("CS1061", "b"), Error("CS0246", "c", file: "B.cs")],
            NoHints);

        Assert.Equal(2, groups.Count);
        Assert.All(groups, g => Assert.Null(g.Hint));
        Assert.Equal(["CS0246", "CS0246"], groups[0].Items.Select(d => d.Code));
        Assert.Equal(["CS1061"], groups[1].Items.Select(d => d.Code));
    }

    [Fact]
    public void Hinted_and_unhinted_diagnostics_with_the_same_code_stay_apart()
    {
        var groups = DiagnosticGrouper.Group(
            [Error("CS0246", "known"), Error("CS0246", "unknown")],
            d => d.Message == "known" ? _hintA : null);

        Assert.Equal(2, groups.Count);
        Assert.Equal(_hintA, groups[0].Hint);
        Assert.Null(groups[1].Hint);
    }

    [Fact]
    public void Groups_with_errors_come_before_groups_with_only_warnings()
    {
        var groups = DiagnosticGrouper.Group(
            [Warning("CS0168"), Warning("CS0219"), Error("CS1061"), Error("CS0246")],
            NoHints);

        Assert.Equal(["CS1061", "CS0246", "CS0168", "CS0219"], groups.Select(g => g.Items[0].Code));
    }

    [Fact]
    public void Inside_a_group_errors_come_before_warnings()
    {
        var groups = DiagnosticGrouper.Group(
            [Warning("CS0618", "w1"), Error("CS0246", "e1"), Warning("CS0618", "w2"), Error("CS0246", "e2")],
            _ => _hintA);

        var group = Assert.Single(groups);
        Assert.Equal(["e1", "e2", "w1", "w2"], group.Items.Select(d => d.Message));
    }

    [Fact]
    public void A_group_with_one_error_and_warnings_ranks_with_the_errors()
    {
        var groups = DiagnosticGrouper.Group(
            [Warning("CS0168"), Warning("CS0618", "w"), Error("CS0246", "e")],
            d => d.Code == "CS0168" ? null : _hintA);

        Assert.Equal(_hintA, groups[0].Hint);
        Assert.Null(groups[1].Hint);
    }

    [Fact]
    public void Order_is_stable_for_equal_rank()
    {
        var input = new[] { Error("CS3"), Error("CS1"), Error("CS2"), Error("CS1", "again") };

        var codes = DiagnosticGrouper.Group(input, NoHints).Select(g => g.Items[0].Code).ToList();

        Assert.Equal(["CS3", "CS1", "CS2"], codes);
        Assert.Equal(codes, DiagnosticGrouper.Group(input, NoHints).Select(g => g.Items[0].Code));
    }

    [Fact]
    public void The_matcher_is_called_once_per_distinct_diagnostic()
    {
        var calls = 0;

        DiagnosticGrouper.Group([Error("CS1"), Error("CS1"), Error("CS2")], d =>
        {
            calls++;
            return null;
        });

        Assert.Equal(2, calls);
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => DiagnosticGrouper.Group(null!, NoHints));
        Assert.Throws<ArgumentNullException>(() => DiagnosticGrouper.Group([], null!));
    }

    [Fact]
    public void Real_sample_output_collapses_msbuild_repeats()
    {
        var raw = File.ReadAllText(Path.Combine(
            Path.GetFullPath(Path.Combine(XnaReborn.Tests.Fixtures.FixturePaths.Root, "..", "XnaReborn.Tests", "Runner", "Samples")),
            "build-cs-errors.txt"));

        var parsed = MsBuildOutputParser.Parse(raw);
        var groups = DiagnosticGrouper.Group(parsed, NoHints);

        Assert.Equal(2, parsed.Count);
        var group = Assert.Single(groups);
        Assert.Equal(1, group.Count);
        Assert.Equal("CS0246", group.Items[0].Code);
    }
}
