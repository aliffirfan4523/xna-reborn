using XnaReborn.Core.Models;
using XnaReborn.Migrator.Source;

namespace XnaReborn.Tests.Device;

/// <summary>Device-to-desktop task 6: folding of platform #if groups (rule XNA053).</summary>
[Trait("Category", "Migrator")]
public sealed class PlatformDirectiveRewriterTests
{
    private const string _chain = "class C\n{\n#if XBOX\n    int a;\n#elif WINDOWS_PHONE\n    int b;\n#else\n    int c;\n#endif\n}\n";

    private static SourceRewriteResult Fold(string text, XnaPlatform platform) =>
        new PlatformDirectiveRewriter().Rewrite(new SourceRewriteContext("a.cs", text, [], platform));

    // ---------- condition parser and three-valued evaluation ----------
    [Theory]
    [InlineData("WINDOWS", true)]
    [InlineData("XBOX", false)]
    [InlineData("!WINDOWS", false)]
    [InlineData("!XBOX", true)]
    [InlineData("WINDOWS || XBOX", true)]
    [InlineData("XBOX || ZUNE", false)]
    [InlineData("(WINDOWS && !XBOX)", true)]
    [InlineData("XBOX && DEBUG", false)]
    [InlineData("DEBUG && XBOX", false)]
    [InlineData("DEBUG || WINDOWS", true)]
    [InlineData("(XBOX || ZUNE) && DEBUG", false)]
    [InlineData("true", true)]
    [InlineData("false || XBOX", false)]
    [InlineData("WINDOWS && DEBUG", null)]
    [InlineData("DEBUG", null)]
    [InlineData("XBOX || DEBUG", null)]
    [InlineData("!DEBUG", null)]
    public void Windows_set_evaluates_conditions_with_three_valued_logic(string condition, bool? expected)
    {
        var result = PlatformCondition.Evaluate(condition, PlatformSymbolSet.For(XnaPlatform.Windows)!);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("WINDOWS == true")]
    [InlineData("WINDOWS !=")]
    [InlineData("WINDOWS &&")]
    [InlineData("(WINDOWS")]
    [InlineData("WINDOWS XBOX")]
    [InlineData("")]
    [InlineData("1")]
    public void Unreadable_conditions_give_null(string condition)
    {
        Assert.Null(PlatformCondition.Evaluate(condition, PlatformSymbolSet.For(XnaPlatform.Windows)!));
    }

    [Fact]
    public void Condition_result_reports_platform_mentions()
    {
        var symbols = PlatformSymbolSet.For(XnaPlatform.Xbox360)!;

        Assert.Equal(new PlatformConditionResult(true, true, true), PlatformCondition.Evaluate("XBOX360 || WINDOWS", symbols));
        Assert.Equal(new PlatformConditionResult(null, false, false), PlatformCondition.Evaluate("DEBUG", symbols));
        Assert.Equal(new PlatformConditionResult(null, true, false), PlatformCondition.Evaluate("XBOX && DEBUG", symbols));
    }

    [Theory]
    [InlineData(XnaPlatform.Windows, "WINDOWS")]
    [InlineData(XnaPlatform.Xbox360, "XBOX, XBOX360")]
    [InlineData(XnaPlatform.Zune, "ZUNE")]
    [InlineData(XnaPlatform.WindowsPhone, "WINDOWS_PHONE")]
    public void Symbol_set_makes_only_the_symbols_of_its_platform_true(XnaPlatform platform, string expected)
    {
        var set = PlatformSymbolSet.For(platform)!;

        Assert.Equal(expected, string.Join(", ", set.TrueSymbols));
        Assert.Null(set.Value("DEBUG"));
        Assert.Equal(platform == XnaPlatform.Windows, set.Value("WINDOWS"));
        Assert.Null(PlatformSymbolSet.For(XnaPlatform.Unknown));
    }

    // ---------- before / after for each symbol set ----------
    [Theory]
    [InlineData(XnaPlatform.Windows, "    int c;\n")]
    [InlineData(XnaPlatform.Xbox360, "    int a;\n")]
    [InlineData(XnaPlatform.WindowsPhone, "    int b;\n")]
    [InlineData(XnaPlatform.Zune, "    int c;\n")]
    public void Each_symbol_set_keeps_its_own_branch_of_an_if_elif_else_chain(XnaPlatform platform, string kept)
    {
        var result = Fold(_chain, platform);

        Assert.Equal("class C\n{\n" + kept + "}\n", result.Text);
        Assert.Single(result.Notes, n => !n.NeedsManualWork);
    }

    [Fact]
    public void Without_an_else_a_false_group_disappears()
    {
        const string code = "a\n#if XBOX\nb\n#endif\nc\n";

        Assert.Equal("a\nc\n", Fold(code, XnaPlatform.Windows).Text);
        Assert.Equal("a\nb\nc\n", Fold(code, XnaPlatform.Xbox360).Text);
    }

    [Fact]
    public void Nested_groups_are_folded_inside_out()
    {
        const string code = "#if WINDOWS\na\n#if XBOX\nb\n#else\nc\n#endif\n#endif\n";

        Assert.Equal("a\nc\n", Fold(code, XnaPlatform.Windows).Text);
        Assert.Equal(string.Empty, Fold(code, XnaPlatform.Xbox360).Text);
    }

    [Fact]
    public void Inactive_looking_branches_are_removed_even_when_they_hold_other_directives()
    {
        const string code = "#if XBOX\n#if DEBUG\nx\n#endif\n#endif\ny\n";

        Assert.Equal("y\n", Fold(code, XnaPlatform.Windows).Text);
    }

    [Fact]
    public void Line_endings_and_indentation_of_kept_lines_are_preserved()
    {
        const string code = "class C\r\n{\r\n\t#if XBOX\r\n\tint a;\r\n\t#else\r\n\tint c;  \r\n\t#endif\r\n}\r\n";

        Assert.Equal("class C\r\n{\r\n\tint c;  \r\n}\r\n", Fold(code, XnaPlatform.Windows).Text);
    }

    [Fact]
    public void Folding_is_deterministic_and_a_second_run_changes_nothing()
    {
        var first = Fold(_chain, XnaPlatform.WindowsPhone);
        var again = Fold(_chain, XnaPlatform.WindowsPhone);
        var second = Fold(first.Text, XnaPlatform.WindowsPhone);

        Assert.Equal(first.Text, again.Text);
        Assert.False(second.Changed(new SourceRewriteContext("a.cs", first.Text, [], XnaPlatform.WindowsPhone)));
    }

    // ---------- conditions that cannot be decided ----------
    [Fact]
    public void A_condition_that_also_needs_debug_is_left_alone_with_a_marker()
    {
        const string code = "class C\n{\n    #if WINDOWS && DEBUG\n    int a;\n    #endif\n}\n";

        var result = Fold(code, XnaPlatform.Windows);

        Assert.Equal(
            "class C\n{\n    // XNAREBORN: XNA053: This #if depends on a platform symbol and on another symbol, so it was left as it is. "
            + "Migrated platform symbols: WINDOWS. Check it by hand.\n    #if WINDOWS && DEBUG\n    int a;\n    #endif\n}\n",
            result.Text);
        Assert.Single(result.Notes, n => n.NeedsManualWork);
    }

    [Fact]
    public void A_marked_group_is_not_marked_twice()
    {
        const string code = "#if WINDOWS && DEBUG\nx\n#endif\n";
        var marked = Fold(code, XnaPlatform.Windows);

        var again = Fold(marked.Text, XnaPlatform.Windows);

        Assert.Equal(marked.Text, again.Text);
    }

    [Fact]
    public void An_undecided_group_still_folds_decided_groups_nested_inside_it()
    {
        const string code = "#if DEBUG && WINDOWS\n#if XBOX\nx\n#else\ny\n#endif\n#endif\n";

        var result = Fold(code, XnaPlatform.Windows);

        Assert.Contains("#if DEBUG && WINDOWS\ny\n#endif\n", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("#if XBOX", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_condition_with_a_platform_symbol_gets_a_marker()
    {
        const string code = "#if WINDOWS == true\nx\n#endif\n";

        var result = Fold(code, XnaPlatform.Windows);

        Assert.StartsWith("// XNAREBORN: XNA053: This #if could not be read", result.Text, StringComparison.Ordinal);
        Assert.EndsWith("#if WINDOWS == true\nx\n#endif\n", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Unbalanced_directives_leave_the_file_unchanged_with_one_marker()
    {
        const string code = "class C\n{\n#if XBOX\n    int a;\n}\n";

        var result = Fold(code, XnaPlatform.Windows);

        Assert.Equal(1, result.Text.Split("XNAREBORN").Length - 1);
        Assert.EndsWith("#if XBOX\n    int a;\n}\n", result.Text, StringComparison.Ordinal);
        Assert.Single(result.Notes, n => n.NeedsManualWork);
    }

    [Theory]
    [InlineData("#endif\n")]
    [InlineData("#else\nx\n")]
    public void Stray_directives_without_a_platform_symbol_change_nothing(string code)
    {
        var result = Fold(code, XnaPlatform.Windows);

        Assert.Equal(code, result.Text);
        Assert.Empty(result.Notes);
    }

    // ---------- text that must stay as it is ----------
    [Theory]
    [InlineData("class C { }\n")]
    [InlineData("#if DEBUG\nx\n#endif\n")]
    [InlineData("#if false\nx\n#endif\n")]
    [InlineData("#region Fields\nint a;\n#endregion\n")]
    [InlineData("// #if XBOX\nint a;\n")]
    [InlineData("/*\n#if XBOX\nx\n#endif\n*/\nint a;\n")]
    [InlineData("string s = @\"\n#if XBOX\nx\n#endif\n\";\n")]
    [InlineData("string s = \"#if XBOX\";\n")]
    [InlineData("#if WINDOWS_PHONE_7\nx\n#endif\n")]
    public void Text_without_a_platform_group_keeps_its_bytes(string code)
    {
        var result = Fold(code, XnaPlatform.Windows);

        Assert.Equal(code, result.Text);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void An_unknown_platform_folds_nothing()
    {
        Assert.Equal(_chain, Fold(_chain, XnaPlatform.Unknown).Text);
    }

    // ---------- the entry point ----------
    private const string _entry = "namespace N\n{\n#if WINDOWS || XBOX\n    static class Program\n    {\n        static void Main(string[] args) { }\n    }\n#endif\n}\n";

    private const string _entryUnwrapped = "namespace N\n{\n    static class Program\n    {\n        static void Main(string[] args) { }\n    }\n}\n";

    [Theory]
    [InlineData(XnaPlatform.Windows)]
    [InlineData(XnaPlatform.Xbox360)]
    [InlineData(XnaPlatform.WindowsPhone)]
    [InlineData(XnaPlatform.Zune)]
    public void Main_inside_a_platform_only_group_always_stays(XnaPlatform platform)
    {
        Assert.Equal(_entryUnwrapped, Fold(_entry, platform).Text);
    }

    [Theory]
    [InlineData(XnaPlatform.Windows)]
    [InlineData(XnaPlatform.WindowsPhone)]
    public void The_engine_runs_folding_before_the_entry_point_rewriter(XnaPlatform platform)
    {
        var registry = DefaultRewriters.Create();
        var engine = new SourceRewriteEngine(registry);

        var result = engine.Rewrite("Program.cs", _entry, ["WINDOWS"], new HashSet<string> { "ENTRYPOINT", "XNA053" }, platform);

        Assert.Equal(_entryUnwrapped, result.Text);
        Assert.Equal(["XNA053"], result.AppliedRuleIds);
    }

    [Fact]
    public void The_entry_point_rewriter_alone_still_gives_the_old_result()
    {
        var engine = new SourceRewriteEngine(DefaultRewriters.Create());

        var result = engine.Rewrite("Program.cs", _entry, ["WINDOWS"], new HashSet<string> { "ENTRYPOINT" }, XnaPlatform.Windows);

        Assert.Equal(_entryUnwrapped, result.Text);
        Assert.Equal(["ENTRYPOINT"], result.AppliedRuleIds);
    }

    [Fact]
    public void A_main_group_with_an_else_branch_is_evaluated_normally()
    {
        const string code = "#if XBOX\nstatic void Main() { }\n#else\nstatic void Main(string[] a) { }\n#endif\n";

        Assert.Equal("static void Main(string[] a) { }\n", Fold(code, XnaPlatform.Windows).Text);
    }
}
