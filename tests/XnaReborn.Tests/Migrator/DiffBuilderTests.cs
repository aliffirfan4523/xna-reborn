using System.Text;
using XnaReborn.Migrator.Source;

namespace XnaReborn.Tests.Migrator;

/// <summary>Unified and side-by-side diffs of rewritten files.</summary>
[Trait("Category", "Migrator")]
public class DiffBuilderTests
{
    private static string Lines(params string[] lines) => string.Join('\n', lines) + "\n";

    private static string Numbered(int count) => Lines(Enumerable.Range(1, count).Select(i => $"line {i}").ToArray());

    [Fact]
    public void Identical_texts_give_empty_unified_diff_and_no_changes()
    {
        var text = Lines("a", "b");

        Assert.Equal(string.Empty, DiffBuilder.BuildUnified("A.cs", text, text));
        var diff = DiffBuilder.BuildSideBySide("A.cs", text, text);
        Assert.Equal(0, diff.Added + diff.Removed + diff.Modified);
        Assert.All(diff.OldLines, l => Assert.Equal(DiffLineKind.Unchanged, l.Kind));
    }

    [Fact]
    public void Added_line_is_reported()
    {
        var unified = DiffBuilder.BuildUnified("A.cs", Lines("a", "c"), Lines("a", "b", "c"));

        Assert.Equal("--- a/A.cs\n+++ b/A.cs\n@@ -1,2 +1,3 @@\n a\n+b\n c\n", unified);
    }

    [Fact]
    public void Removed_line_is_reported()
    {
        var unified = DiffBuilder.BuildUnified("A.cs", Lines("a", "b", "c"), Lines("a", "c"));

        Assert.Equal("--- a/A.cs\n+++ b/A.cs\n@@ -1,3 +1,2 @@\n a\n-b\n c\n", unified);
    }

    [Fact]
    public void Changed_line_is_reported()
    {
        var unified = DiffBuilder.BuildUnified("A.cs", Lines("a", "b", "c"), Lines("a", "B", "c"));

        Assert.Equal("--- a/A.cs\n+++ b/A.cs\n@@ -1,3 +1,3 @@\n a\n-b\n+B\n c\n", unified);
    }

    [Fact]
    public void Distant_changes_make_separate_hunks_with_context()
    {
        var original = Numbered(30);
        var updated = original.Replace("line 3\n", "THREE\n").Replace("line 27\n", "TWENTY-SEVEN\n");

        var unified = DiffBuilder.BuildUnified("A.cs", original, updated);

        Assert.Equal(2, unified.Split('\n').Count(l => l.StartsWith("@@", StringComparison.Ordinal)));
        Assert.Contains("@@ -1,6 +1,6 @@\n", unified);
        Assert.Contains("@@ -24,7 +24,7 @@\n", unified);
    }

    [Fact]
    public void Close_changes_share_one_hunk()
    {
        var original = Numbered(12);
        var updated = original.Replace("line 3\n", "THREE\n").Replace("line 8\n", "EIGHT\n");

        var unified = DiffBuilder.BuildUnified("A.cs", original, updated);

        Assert.Single(unified.Split('\n'), l => l.StartsWith("@@", StringComparison.Ordinal));
    }

    [Fact]
    public void Context_lines_argument_is_honoured()
    {
        var original = Numbered(10);
        var updated = original.Replace("line 5\n", "FIVE\n");

        var unified = DiffBuilder.BuildUnified("A.cs", original, updated, contextLines: 1);

        Assert.Equal("--- a/A.cs\n+++ b/A.cs\n@@ -4,3 +4,3 @@\n line 4\n-line 5\n+FIVE\n line 6\n", unified);
    }

    [Fact]
    public void Change_at_start_and_end_of_file()
    {
        var original = Numbered(10);
        var updated = original.Replace("line 1\n", "ONE\n").Replace("line 10\n", "TEN\n");

        var unified = DiffBuilder.BuildUnified("A.cs", original, updated);

        Assert.Contains("@@ -1,4 +1,4 @@\n-line 1\n+ONE\n", unified);
        Assert.Contains("@@ -7,4 +7,4 @@\n", unified);
        Assert.EndsWith("-line 10\n+TEN\n", unified);
    }

    [Fact]
    public void Crlf_on_both_sides_causes_no_spurious_differences_and_no_cr_in_output()
    {
        var original = Lines("a", "b", "c").Replace("\n", "\r\n");
        var updated = Lines("a", "B", "c").Replace("\n", "\r\n");

        var unified = DiffBuilder.BuildUnified("A.cs", original, updated);

        Assert.Equal("--- a/A.cs\n+++ b/A.cs\n@@ -1,3 +1,3 @@\n a\n-b\n+B\n c\n", unified);
        Assert.Equal(string.Empty, DiffBuilder.BuildUnified("A.cs", original, original));
    }

    [Fact]
    public void Line_ending_change_alone_is_reported()
    {
        var lf = Lines("a", "b");
        var crlf = lf.Replace("\n", "\r\n");

        var diff = DiffBuilder.BuildSideBySide("A.cs", lf, crlf);

        Assert.NotEqual(string.Empty, DiffBuilder.BuildUnified("A.cs", lf, crlf));
        Assert.Equal(2, diff.Modified);
    }

    [Fact]
    public void Empty_original_is_a_new_file()
    {
        var unified = DiffBuilder.BuildUnified("A.cs", string.Empty, Lines("a", "b"));
        var diff = DiffBuilder.BuildSideBySide("A.cs", string.Empty, Lines("a", "b"));

        Assert.Equal("--- a/A.cs\n+++ b/A.cs\n@@ -0,0 +1,2 @@\n+a\n+b\n", unified);
        Assert.Equal(2, diff.Added);
        Assert.All(diff.OldLines, l => Assert.Equal(DiffLineKind.Imaginary, l.Kind));
    }

    [Fact]
    public void Empty_updated_removes_everything()
    {
        var unified = DiffBuilder.BuildUnified("A.cs", Lines("a", "b"), string.Empty);
        var diff = DiffBuilder.BuildSideBySide("A.cs", Lines("a", "b"), string.Empty);

        Assert.Equal("--- a/A.cs\n+++ b/A.cs\n@@ -1,2 +0,0 @@\n-a\n-b\n", unified);
        Assert.Equal(2, diff.Removed);
        Assert.All(diff.NewLines, l => Assert.Equal(DiffLineKind.Imaginary, l.Kind));
    }

    [Fact]
    public void Blank_lines_are_kept_as_lines()
    {
        var unified = DiffBuilder.BuildUnified("A.cs", Lines("a", string.Empty, "b"), Lines("a", string.Empty, "c"));

        Assert.Equal("--- a/A.cs\n+++ b/A.cs\n@@ -1,3 +1,3 @@\n a\n \n-b\n+c\n", unified);
    }

    [Fact]
    public void Manual_marker_is_detected_on_new_lines_only()
    {
        var marked = Lines("a", "// XNAREBORN: R1: fix me", "b");

        Assert.True(DiffBuilder.BuildSideBySide("A.cs", Lines("a", "b"), marked).ContainsManualMarker);
        Assert.False(DiffBuilder.BuildSideBySide("A.cs", marked, Lines("a", "b")).ContainsManualMarker);
        Assert.False(DiffBuilder.BuildSideBySide("A.cs", Lines("a"), Lines("a", "b")).ContainsManualMarker);
    }

    [Fact]
    public void Counts_cover_added_removed_and_modified()
    {
        var original = Lines("a", "b", "c", "d", "e", "f", "g", "h", "i", "j");
        var updated = Lines("a", "B", "c", "d", "e", "f", "g", "h", "i", "j", "k");

        var diff = DiffBuilder.BuildSideBySide("A.cs", original, updated);

        Assert.Equal((1, 0, 1), (diff.Added, diff.Removed, diff.Modified));

        var diff2 = DiffBuilder.BuildSideBySide("A.cs", Lines("a", "b", "c"), Lines("x"));
        Assert.Equal((0, 2, 1), (diff2.Added, diff2.Removed, diff2.Modified));
    }

    [Fact]
    public void Sides_have_equal_length_and_padding_has_no_number()
    {
        var diff = DiffBuilder.BuildSideBySide("A.cs", Lines("a", "b", "c"), Lines("a", "x", "y", "z", "c"));

        Assert.Equal(diff.OldLines.Count, diff.NewLines.Count);
        Assert.Equal(5, diff.OldLines.Count);
        var padded = diff.OldLines.Where(l => l.Kind == DiffLineKind.Imaginary).ToList();
        Assert.Equal(2, padded.Count);
        Assert.All(padded, l => Assert.Null(l.Number));
        Assert.Equal(DiffLineKind.Modified, diff.OldLines[1].Kind);
        Assert.Equal(DiffLineKind.Added, diff.NewLines[2].Kind);
    }

    [Theory]
    [InlineData("a\nb\nc\nd\n", "a\nX\nc\nd\ne\n")]
    [InlineData("", "a\n")]
    [InlineData("a\nb\n", "b\na\n")]
    [InlineData("a\r\nb\r\n", "a\r\nc\r\nb\r\n")]
    public void Line_numbers_match_the_texts(string original, string updated)
    {
        var diff = DiffBuilder.BuildSideBySide("A.cs", original, updated);

        AssertNumbers(diff.OldLines, original);
        AssertNumbers(diff.NewLines, updated);
    }

    private static void AssertNumbers(IReadOnlyList<DiffLine> side, string text)
    {
        var texts = text.Length == 0 ? [] : text.TrimEnd('\n').Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var real = side.Where(l => l.Number is not null).ToList();
        Assert.Equal(texts.Length, real.Count);
        for (var i = 0; i < real.Count; i++)
        {
            Assert.Equal(i + 1, real[i].Number);
            Assert.Equal(texts[i], real[i].Text);
        }
    }

    [Fact]
    public void Output_is_deterministic()
    {
        var a = DiffBuilder.BuildUnified("A.cs", Numbered(20), Numbered(20).Replace("line 9\n", "x\n"));
        var b = DiffBuilder.BuildUnified("A.cs", Numbered(20), Numbered(20).Replace("line 9\n", "x\n"));

        Assert.Equal(a, b);
    }

    [Fact]
    public void Null_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => DiffBuilder.BuildUnified(null!, "", ""));
        Assert.Throws<ArgumentNullException>(() => DiffBuilder.BuildUnified("A", null!, ""));
        Assert.Throws<ArgumentNullException>(() => DiffBuilder.BuildUnified("A", "", null!));
        Assert.Throws<ArgumentNullException>(() => DiffBuilder.BuildSideBySide(null!, "", ""));
        Assert.Throws<ArgumentNullException>(() => DiffBuilder.BuildSideBySide("A", null!, ""));
        Assert.Throws<ArgumentNullException>(() => DiffBuilder.BuildSideBySide("A", "", null!));
    }

    private const string _sample =
        "using System;\n\nnamespace Game\n{\n    class Program\n    {\n        static void Main()\n        {\n" +
        "            Console.WriteLine(\"hi\");\n        }\n    }\n}\n";

    private const string _sampleUpdated =
        "using System;\n\nnamespace Game\n{\n    class Program\n    {\n        // XNAREBORN: R1: check this\n" +
        "        static void Main(string[] args)\n        {\n            Console.WriteLine(\"hi\");\n            Run();\n" +
        "        }\n    }\n}\n";

    [Fact]
    public Task Unified_diff_matches_snapshot() =>
        Verify(DiffBuilder.BuildUnified("Game/Program.cs", _sample, _sampleUpdated), "diff");

    [Fact]
    public Task Side_by_side_listing_matches_snapshot()
    {
        var diff = DiffBuilder.BuildSideBySide("Game/Program.cs", _sample, _sampleUpdated);
        var sb = new StringBuilder();
        sb.Append(diff.Path).Append(" +").Append(diff.Added).Append(" -").Append(diff.Removed)
            .Append(" ~").Append(diff.Modified).Append(" marker=").Append(diff.ContainsManualMarker).Append('\n');
        for (var i = 0; i < diff.OldLines.Count; i++)
        {
            var l = diff.OldLines[i];
            var r = diff.NewLines[i];
            sb.Append($"{l.Number,3} {l.Kind,-9}| {l.Text}\n");
            sb.Append($"{r.Number,3} {r.Kind,-9}| {r.Text}\n");
        }

        return Verify(sb.ToString(), "diff");
    }
}
